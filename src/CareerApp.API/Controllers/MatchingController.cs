using CareerApp.Core.Interfaces;
using CareerApp.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace CareerApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class MatchingController(
    IJobMatchingService jobMatchingService,
    ICandidateRepository candidateRepository,
    IJobRepository jobRepository) : ControllerBase
{
    [HttpPost("candidate/{candidateId:guid}/job/{jobId:guid}")]
    public async Task<ActionResult<MatchResult>> MatchCandidateToJobAsync(Guid candidateId, Guid jobId, CancellationToken cancellationToken)
    {
        if (await candidateRepository.GetByIdAsync(candidateId, cancellationToken) is null ||
            await jobRepository.GetByIdAsync(jobId, cancellationToken) is null)
        {
            return NotFound();
        }

        var match = await jobMatchingService.MatchCandidateToJobAsync(candidateId, jobId, cancellationToken);
        return Ok(match);
    }

    [HttpPost("candidate/{candidateId:guid}/all-jobs")]
    public async Task<ActionResult<IReadOnlyCollection<MatchResult>>> MatchCandidateToAllJobsAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        if (await candidateRepository.GetByIdAsync(candidateId, cancellationToken) is null)
        {
            return NotFound();
        }

        var matches = await jobMatchingService.MatchCandidateToAllJobsAsync(candidateId, cancellationToken);
        return Ok(matches);
    }

    [HttpPost("job/{jobId:guid}/all-candidates")]
    public async Task<ActionResult<IReadOnlyCollection<MatchResult>>> MatchJobToAllCandidatesAsync(Guid jobId, CancellationToken cancellationToken)
    {
        if (await jobRepository.GetByIdAsync(jobId, cancellationToken) is null)
        {
            return NotFound();
        }

        var matches = await jobMatchingService.MatchJobToCandidatesAsync(jobId, cancellationToken);
        return Ok(matches);
    }
}
