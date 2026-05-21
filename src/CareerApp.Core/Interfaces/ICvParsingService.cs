using CareerApp.Core.Models;

namespace CareerApp.Core.Interfaces;

public interface ICvParsingService
{
    Task<Candidate> ParseCvAsync(Stream fileStream, string fileName, CvParsingMethod method = CvParsingMethod.ContentUnderstanding, CancellationToken cancellationToken = default);
}
