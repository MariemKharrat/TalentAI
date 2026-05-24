using CareerApp.Core.DTOs;
using CareerApp.Core.Interfaces;
using CareerApp.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace CareerApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class JobsController(
    IJobRepository jobRepository,
    IJobDescriptionGenerator jobDescriptionGenerator,
    IJobMatchingService jobMatchingService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<Job>>> GetJobsAsync([FromQuery] bool? active, CancellationToken cancellationToken)
    {
        var jobs = active switch
        {
            true => await jobRepository.GetAllActiveAsync(cancellationToken),
            false => (await jobRepository.GetAllAsync(cancellationToken)).Where(j => !j.IsActive).ToList(),
            _ => await jobRepository.GetAllAsync(cancellationToken)
        };
        return Ok(jobs);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Job>> GetJobByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetByIdAsync(id, cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpPost]
    public async Task<ActionResult<Job>> CreateJobAsync([FromBody] JobUpsertRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { message = "A job title is required." });
        }

        var job = new Job
        {
            Title = request.Title.Trim(),
            Department = request.Department,
            Description = request.Description,
            Requirements = request.Requirements,
            RequiredSkills = request.RequiredSkills ?? [],
            PreferredSkills = request.PreferredSkills ?? [],
            Location = request.Location,
            ExperienceLevel = request.ExperienceLevel,
            IsActive = request.IsActive
        };

        var savedJob = await jobRepository.AddAsync(job, cancellationToken);
        return Created($"/api/jobs/{savedJob.Id}", savedJob);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Job>> UpdateJobAsync(Guid id, [FromBody] JobUpsertRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { message = "A job title is required." });
        }

        var existingJob = await jobRepository.GetByIdAsync(id, cancellationToken);
        if (existingJob is null)
        {
            return NotFound();
        }

        existingJob.Title = request.Title.Trim();
        existingJob.Department = request.Department;
        existingJob.Description = request.Description;
        existingJob.Requirements = request.Requirements;
        existingJob.RequiredSkills = request.RequiredSkills ?? existingJob.RequiredSkills;
        existingJob.PreferredSkills = request.PreferredSkills ?? existingJob.PreferredSkills;
        existingJob.Location = request.Location;
        existingJob.ExperienceLevel = request.ExperienceLevel;
        existingJob.IsActive = request.IsActive;
        existingJob.UpdatedAtUtc = DateTime.UtcNow;

        var updatedJob = await jobRepository.UpdateAsync(existingJob, cancellationToken);
        return updatedJob is null ? NotFound() : Ok(updatedJob);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteJobAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await jobRepository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        // Cascade delete: remove all match results for this job
        await jobMatchingService.DeleteMatchesForJobAsync(id, cancellationToken);

        return NoContent();
    }

    [HttpPost("generate-description")]
    public async Task<ActionResult<JobDescriptionResponse>> GenerateDescriptionAsync([FromBody] GenerateJobDescriptionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var description = await jobDescriptionGenerator.GenerateJobDescriptionAsync(new CareerApp.Core.Models.JobDescriptionRequest
            {
                Title = request.Title,
                Department = request.Department ?? string.Empty,
                Location = request.Location ?? string.Empty,
                ExperienceLevel = request.ExperienceLevel ?? string.Empty,
                EmploymentType = string.IsNullOrWhiteSpace(request.EmploymentType) ? "Full-time" : request.EmploymentType,
                RequiredSkills = request.RequiredSkills ?? [],
                PreferredSkills = request.PreferredSkills ?? [],
                Responsibilities = request.Responsibilities ?? request.Summary ?? string.Empty,
                Requirements = request.Requirements ?? string.Empty,
                TeamSize = request.TeamSize ?? string.Empty,
                ReportingTo = request.ReportingTo ?? string.Empty,
                SalaryRange = request.SalaryRange ?? string.Empty,
                Benefits = request.Benefits ?? string.Empty,
                PolicyContext = request.PolicyContext ?? string.Empty,
                Tone = string.IsNullOrWhiteSpace(request.Tone) ? "Professional and inclusive" : request.Tone
            });
            return Ok(new JobDescriptionResponse { Description = description });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to generate the job description.", detail = exception.Message });
        }
    }

    [HttpGet("{id:guid}/candidates")]
    public async Task<ActionResult<IReadOnlyCollection<MatchResult>>> GetMatchedCandidatesAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetByIdAsync(id, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        var matches = await jobMatchingService.GetMatchesForJobAsync(id, cancellationToken);
        return Ok(matches);
    }
}
