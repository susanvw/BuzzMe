using BuzzMe.Application.Abstractions;
using BuzzMe.Domain.Auth;
using BuzzMe.Domain.Boards;
using BuzzMe.Domain.Buzzes;
using BuzzMe.Domain.Invitations;
using BuzzMe.Domain.Occurrences;
using BuzzMe.Domain.Reminders;
using BuzzMe.Domain.SeedWork;
using BuzzMe.Domain.Users;
using BuzzMe.Infrastructure.Ids;
using BuzzMe.Infrastructure.Messaging.Email;
using BuzzMe.Infrastructure.Messaging.Notifications;
using BuzzMe.Infrastructure.Messaging.Push;
using BuzzMe.Infrastructure.Messaging.Sms;
using BuzzMe.Infrastructure.Persistence.Migrations;
using BuzzMe.Infrastructure.Persistence.Migrations.Steps;
using BuzzMe.Infrastructure.Persistence.Mongo;
using BuzzMe.Infrastructure.Persistence.Mongo.Auth;
using BuzzMe.Infrastructure.Persistence.Mongo.Boards;
using BuzzMe.Infrastructure.Persistence.Mongo.Buzzes;
using BuzzMe.Infrastructure.Persistence.Mongo.Invitations;
using BuzzMe.Infrastructure.Persistence.Mongo.Occurrences;
using BuzzMe.Infrastructure.Persistence.Mongo.Reminders;
using BuzzMe.Infrastructure.Persistence.Mongo.Users;
using BuzzMe.Infrastructure.Persistence.Outbox;
using BuzzMe.Infrastructure.Security;
using BuzzMe.Infrastructure.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuzzMe.Infrastructure.DependencyInjection;

/// <summary>
/// The one place Infrastructure registers itself. Called only from the composition root
/// of BuzzMe.Api and BuzzMe.Workers (DEVELOPMENT_GUIDE.md §2/§4) — never referenced from
/// inside an endpoint, job, or Application Service.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBuzzMeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoOptions>(configuration.GetSection(MongoOptions.SectionName));
        services.AddSingleton<MongoContext>();
        services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongodb", tags: ["ready"]);

        services.Configure<JwtIssuerOptions>(configuration.GetSection(JwtIssuerOptions.SectionName));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IIdGenerator, TimeSortableIdGenerator>();
        services.AddSingleton<IInvitationTokenGenerator, SecureInvitationTokenGenerator>();
        services.AddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<IVerificationCodeGenerator, NumericVerificationCodeGenerator>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        services.AddSingleton<IOutboxWriter, MongoOutboxWriter>();
        services.AddSingleton<MongoMigrationRunner>();
        services.AddSingleton<IMongoMigration, CreateBoardIndexes>();
        services.AddSingleton<IMongoMigration, CreateReminderIndexes>();
        services.AddSingleton<IMongoMigration, CreateOccurrenceIndexes>();
        services.AddSingleton<IMongoMigration, CreateBuzzIndexes>();
        services.AddSingleton<IMongoMigration, CreateInvitationIndexes>();
        services.AddSingleton<IMongoMigration, CreateUserIndexes>();
        services.AddSingleton<IMongoMigration, CreateUserPasswordResetIndex>();
        services.AddSingleton<IMongoMigration, CreateRefreshTokenIndexes>();

        // Default to logging instead of delivering until a real provider is wired up —
        // see NullPushNotificationSender/NullEmailSender/NullSmsSender for why this is
        // deliberate, not an oversight.
        services.AddSingleton<IPushNotificationSender, NullPushNotificationSender>();
        services.AddSingleton<IEmailSender, NullEmailSender>();
        services.AddSingleton<ISmsSender, NullSmsSender>();

        // Temporary — see LoggingNotificationDispatcher's own doc comment and
        // SPRINT_6_REPORT.md §5 for the plan to replace this with real dispatch.
        services.AddSingleton<INotificationDispatcher, LoggingNotificationDispatcher>();

        services.AddRepositories();

        return services;
    }

    /// <summary>
    /// The designated home for every `I{Aggregate}Repository` registration
    /// (DEVELOPMENT_GUIDE.md §2's Infrastructure entry).
    /// </summary>
    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IBoardRepository, BoardRepository>();
        services.AddScoped<IReminderRepository, ReminderRepository>();
        services.AddScoped<IOccurrenceRepository, OccurrenceRepository>();
        services.AddScoped<IBuzzRepository, BuzzRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        return services;
    }
}
