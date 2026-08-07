using BuzzMe.Application.Buzzes;
using BuzzMe.Infrastructure.DependencyInjection;
using BuzzMe.Infrastructure.Persistence.Migrations;
using BuzzMe.Workers.Jobs;

var builder = Host.CreateApplicationBuilder(args);

// Same Infrastructure registration as BuzzMe.Api — Mongo, Clock, IdGenerator, messaging
// adapters, outbox, migrations. Workers has no HTTP surface and no Contracts reference
// (DEVELOPMENT_GUIDE.md §2) — it exists purely to host background work.
builder.Services.AddBuzzMeInfrastructure(builder.Configuration);

// Application services jobs depend on — same registration as BuzzMe.Api's composition root.
builder.Services.AddScoped<BuzzApplicationService>();

var host = builder.Build();

// Same startup-sequence rule as BuzzMe.Api: migrations run once before any job starts.
using (var scope = host.Services.CreateScope())
{
    var migrationRunner = scope.ServiceProvider.GetRequiredService<MongoMigrationRunner>();
    await migrationRunner.RunAsync(CancellationToken.None);
}

// Jobs (BuzzMe.Workers/Jobs/*) are registered here as `AddHostedService<TJob>()`, one per
// row of DEVELOPMENT_GUIDE.md §7's process table, as each is implemented. See
// REPOSITORY_BOOTSTRAP.md's "First Implementation Order."
builder.Services.AddHostedService<BuzzDeliveryWorker>();

host.Run();
