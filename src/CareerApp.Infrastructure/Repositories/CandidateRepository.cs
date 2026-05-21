using CareerApp.Core.Interfaces;
using CareerApp.Core.Models;
using CareerApp.Infrastructure.Data;
using Microsoft.Azure.Cosmos;

namespace CareerApp.Infrastructure.Repositories;

public class CandidateRepository : ICandidateRepository
{
    private readonly CosmosDbService _cosmosDb;

    public CandidateRepository(CosmosDbService cosmosDb)
    {
        _cosmosDb = cosmosDb;
    }

    public async Task<Candidate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _cosmosDb.Candidates.ReadItemAsync<Candidate>(
                id.ToString(), new PartitionKey(id.ToString()), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyCollection<Candidate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c ORDER BY c.createdAtUtc DESC");
        var iterator = _cosmosDb.Candidates.GetItemQueryIterator<Candidate>(query);

        var results = new List<Candidate>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<Candidate> AddAsync(Candidate candidate, CancellationToken cancellationToken = default)
    {
        if (candidate.Id == Guid.Empty)
        {
            candidate.Id = Guid.NewGuid();
        }

        candidate.CreatedAtUtc = candidate.CreatedAtUtc == default ? DateTime.UtcNow : candidate.CreatedAtUtc;

        var response = await _cosmosDb.Candidates.CreateItemAsync(
            candidate, new PartitionKey(candidate.Id.ToString()), cancellationToken: cancellationToken);
        return response.Resource;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cosmosDb.Candidates.DeleteItemAsync<Candidate>(
                id.ToString(), new PartitionKey(id.ToString()), cancellationToken: cancellationToken);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
