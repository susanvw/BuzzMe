using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuzzMe.Api.Configuration;
using BuzzMe.Api.Endpoints;
using BuzzMe.Api.Identity;
using BuzzMe.Api.Middleware;
using BuzzMe.Application.Abstractions;
using BuzzMe.Application.Auth;
using BuzzMe.Application.Boards;
using BuzzMe.Application.Invitations;
using BuzzMe.Application.Reminders;
using BuzzMe.Application.Users;
using BuzzMe.Infrastructure.DependencyInjection;
using BuzzMe.Infrastructure.Persistence.Migrations;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration -----------------------------------------------------------------
// Strongly-typed Options only — no scattered IConfiguration["..."] reads (DEVELOPMENT_GUIDE.md §9).
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

// --- Serialization -------------------------------------------------------------------
// camelCase JSON, enums as strings — DEVELOPMENT_GUIDE.md §9.
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
builder.Services.AddSingleton(jsonOptions);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

// --- Identity ---------------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();

// --- Authentication / Authorization --------------------------------------------------
// API_CONTRACT.md §2 — Bearer access tokens; everything except the endpoints explicitly
// listed there as unauthenticated requires one.
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["SigningKey"] ?? string.Empty)),
            ValidateLifetime = true,
        };
    });
builder.Services.AddAuthorization();

// --- Infrastructure (Mongo, Clock, IdGenerator, messaging adapters, outbox, migrations) ---
builder.Services.AddBuzzMeInfrastructure(builder.Configuration);

// --- Application services + request validation ---------------------------------------
// One Application Service per bounded-context area (DEVELOPMENT_GUIDE.md §3) — registered
// here as each is implemented.
builder.Services.AddScoped<BoardApplicationService>();
builder.Services.AddScoped<ReminderApplicationService>();
builder.Services.AddScoped<InvitationApplicationService>();
builder.Services.AddScoped<UserApplicationService>();
builder.Services.AddScoped<AuthApplicationService>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Liveness is Api-specific (it only asks "is the process up"), so it's registered here
// rather than in Infrastructure's AddBuzzMeInfrastructure, which already registers the
// "ready" (Mongo-dependent) check.
builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

var app = builder.Build();

// --- Startup sequence ---------------------------------------------------------------
// Migrations run once, at startup, before the app accepts traffic (DEVELOPMENT_GUIDE.md §6).
using (var scope = app.Services.CreateScope())
{
    var migrationRunner = scope.ServiceProvider.GetRequiredService<MongoMigrationRunner>();
    await migrationRunner.RunAsync(app.Lifetime.ApplicationStopping);
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

// Feature endpoints are mapped here as each is implemented, one MapXEndpoints() extension
// per resource area, matching API_CONTRACT.md §5 exactly.
app.MapBoardEndpoints();
app.MapReminderEndpoints();
app.MapInvitationEndpoints();
app.MapUserEndpoints();
app.MapAuthEndpoints();

app.Run();

// Exposed for BuzzMe.Api.IntegrationTests' WebApplicationFactory<Program> (DEVELOPMENT_GUIDE.md §8).
public partial class Program;
