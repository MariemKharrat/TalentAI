using CareerApp.Core.Interfaces;
using CareerApp.Core.Models;
using CareerApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerApp.Infrastructure.Repositories;

public class CandidateRepository : ICandidateRepository
{
    private readonly AppDbContext _dbContext;

    public CandidateRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Candidate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Candidates
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<Candidate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Candidates
            .AsNoTracking()
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Candidate> AddAsync(Candidate candidate, CancellationToken cancellationToken = default)
    {
        candidate.CreatedAtUtc = candidate.CreatedAtUtc == default ? DateTime.UtcNow : candidate.CreatedAtUtc;

        await _dbContext.Candidates.AddAsync(candidate, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return candidate;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var candidate = await _dbContext.Candidates.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (candidate is null)
        {
            return false;
        }

        _dbContext.Candidates.Remove(candidate);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
