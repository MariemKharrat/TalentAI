using CareerApp.Core.Interfaces;
using CareerApp.Core.Models;
using CareerApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerApp.Infrastructure.Repositories;

public class JobRepository : IJobRepository
{
    private readonly AppDbContext _dbContext;

    public JobRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Jobs
            .AsNoTracking()
            .SingleOrDefaultAsync(job => job.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<Job>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Jobs
            .AsNoTracking()
            .OrderByDescending(job => job.UpdatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<Job>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Jobs
            .AsNoTracking()
            .Where(job => job.IsActive)
            .OrderByDescending(job => job.UpdatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Job> AddAsync(Job job, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        job.CreatedAtUtc = job.CreatedAtUtc == default ? utcNow : job.CreatedAtUtc;
        job.UpdatedAtUtc = utcNow;

        await _dbContext.Jobs.AddAsync(job, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return job;
    }

    public async Task<Job?> UpdateAsync(Job job, CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.Jobs.AnyAsync(item => item.Id == job.Id, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            return null;
        }

        job.UpdatedAtUtc = DateTime.UtcNow;
        _dbContext.Jobs.Update(job);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return job;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.Jobs.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return false;
        }

        _dbContext.Jobs.Remove(job);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
