using CareerApp.Core.Models;

namespace CareerApp.Core.Interfaces;

public interface IJobDescriptionGenerator
{
    Task<string> GenerateJobDescriptionAsync(JobDescriptionRequest request);
}
