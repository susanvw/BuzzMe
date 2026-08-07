using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;

namespace BuzzMe.Infrastructure.Persistence.Mongo;

/// <summary>Backs the `/health/ready` endpoint's Mongo dependency check (DEVELOPMENT_GUIDE.md §2).</summary>
public sealed class MongoHealthCheck(MongoContext mongoContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await mongoContext.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy("MongoDB is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MongoDB is not reachable.", ex);
        }
    }
}
