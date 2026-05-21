using CareerApp.Core.Interfaces;
using CareerApp.Core.Models;
using CareerApp.Infrastructure.Data;
using Microsoft.Azure.Cosmos;

namespace CareerApp.Infrastructure.Repositories;

public class JobRepository : IJobRepository
{
    private readonly CosmosDbService _cosmosDb;

    public JobRepository(CosmosDbService cosmosDb)
    {
        _cosmosDb = cosmosDb;
    }

    public async Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _cosmosDb.Jobs.ReadItemAsync<Job>(
                id.ToString(), new PartitionKey(id.ToString()), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyCollection<Job>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c ORDER BY c.updatedAtUtc DESC");
        var iterator = _cosmosDb.Jobs.GetItemQueryIterator<Job>(query);

        var results = new List<Job>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<IReadOnlyCollection<Job>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.isActive = true ORDER BY c.updatedAtUtc DESC");
        var iterator = _cosmosDb.Jobs.GetItemQueryIterator<Job>(query);

        var results = new List<Job>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<Job> AddAsync(Job job, CancellationToken cancellationToken = default)
    {
        if (job.Id == Guid.Empty)
        {
            job.Id = Guid.NewGuid();
        }

        var utcNow = DateTime.UtcNow;
        job.CreatedAtUtc = job.CreatedAtUtc == default ? utcNow : job.CreatedAtUtc;
        job.UpdatedAtUtc = utcNow;

        var response = await _cosmosDb.Jobs.CreateItemAsync(
            job, new PartitionKey(job.Id.ToString()), cancellationToken: cancellationToken);
        return response.Resource;
    }

    public async Task<Job?> UpdateAsync(Job job, CancellationToken cancellationToken = default)
    {
        try
        {
            job.UpdatedAtUtc = DateTime.UtcNow;
            var response = await _cosmosDb.Jobs.ReplaceItemAsync(
                job, job.Id.ToString(), new PartitionKey(job.Id.ToString()), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cosmosDb.Jobs.DeleteItemAsync<Job>(
                id.ToString(), new PartitionKey(id.ToString()), cancellationToken: cancellationToken);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
