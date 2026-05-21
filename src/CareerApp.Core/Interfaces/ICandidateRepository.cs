using CareerApp.Core.Models;

namespace CareerApp.Core.Interfaces;

public interface ICandidateRepository
{
    Task<IReadOnlyCollection<Candidate>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Candidate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Candidate> AddAsync(Candidate candidate, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
