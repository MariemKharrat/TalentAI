using CareerApp.Core.Models;

namespace CareerApp.Core.Interfaces;

public interface IJobRepository
{
    Task<IReadOnlyCollection<Job>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Job>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Job> AddAsync(Job job, CancellationToken cancellationToken = default);
    Task<Job?> UpdateAsync(Job job, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
