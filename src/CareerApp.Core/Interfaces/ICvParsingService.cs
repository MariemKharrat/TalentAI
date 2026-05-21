using CareerApp.Core.Models;

namespace CareerApp.Core.Interfaces;

public interface ICvParsingService
{
    Task<Candidate> ParseCvAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
}
