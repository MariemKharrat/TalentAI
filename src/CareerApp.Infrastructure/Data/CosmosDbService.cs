using CareerApp.Infrastructure.Configuration;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace CareerApp.Infrastructure.Data;

public sealed class CosmosDbService : IAsyncDisposable
{
    private readonly CosmosClient _client;
    private readonly Database _database;

    public Container Candidates { get; }
    public Container Jobs { get; }
    public Container MatchResults { get; }

    public CosmosDbService(IOptions<CosmosDbOptions> options)
    {
        var config = options.Value;
        _client = new CosmosClient(config.Endpoint, config.Key, new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        });
        _database = _client.GetDatabase(config.DatabaseName);
        Candidates = _database.GetContainer("Candidates");
        Jobs = _database.GetContainer("Jobs");
        MatchResults = _database.GetContainer("MatchResults");
    }

    public async Task InitializeAsync()
    {
        var dbResponse = await _client.CreateDatabaseIfNotExistsAsync(_database.Id);
        var db = dbResponse.Database;
        await db.CreateContainerIfNotExistsAsync("Candidates", "/id");
        await db.CreateContainerIfNotExistsAsync("Jobs", "/id");
        await db.CreateContainerIfNotExistsAsync("MatchResults", "/candidateId");
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
