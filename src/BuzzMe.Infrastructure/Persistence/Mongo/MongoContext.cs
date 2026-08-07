using BuzzMe.Infrastructure.Persistence.Outbox;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace BuzzMe.Infrastructure.Persistence.Mongo;

/// <summary>
/// The one place a Mongo connection is established. Repository implementations depend on
/// this, never on MongoClient directly — DEVELOPMENT_GUIDE.md §2's Infrastructure entry.
/// Collection accessors are added here as each aggregate's repository is implemented
/// (DEVELOPMENT_GUIDE.md §6's collection table).
/// </summary>
public sealed class MongoContext
{
    static MongoContext()
    {
        // The driver no longer assumes a Guid representation by default — every BoardId,
        // UserId, etc. is a Guid, so this must be set once, globally, before any document
        // touches the wire, rather than annotated on every single property.
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        BsonSerializer.RegisterSerializer(new NullableSerializer<Guid>(new GuidSerializer(GuidRepresentation.Standard)));
    }

    public MongoContext(IOptions<MongoOptions> options)
    {
        var settings = options.Value;
        var client = new MongoClient(settings.ConnectionString);
        Database = client.GetDatabase(settings.DatabaseName);
    }

    public IMongoDatabase Database { get; }

    /// <summary>Outbox is the one collection that exists at the foundation stage — it has no aggregate of its own (§7).</summary>
    public IMongoCollection<OutboxMessage> Outbox => Database.GetCollection<OutboxMessage>("outbox");
}
