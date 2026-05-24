using CareerApp.Core.Interfaces;
using CareerApp.Core.Models;
using CareerApp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace CareerApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CandidatesController(
    ICandidateRepository candidateRepository,
    ICvParsingService cvParsingService,
    IJobMatchingService jobMatchingService,
    BlobStorageService blobStorageService) : ControllerBase
{
    [HttpPost("upload-cv")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<Candidate>> UploadCvAsync(
        IFormFile file,
        [FromQuery] string method = "ContentUnderstanding",
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "A CV file is required." });
        }

        var parsingMethod = method.Equals("DocumentIntelligence", StringComparison.OrdinalIgnoreCase)
            ? CvParsingMethod.DocumentIntelligence
            : CvParsingMethod.ContentUnderstanding;

        try
        {
            await using var uploadStream = file.OpenReadStream();
            var blobUrl = await blobStorageService.UploadCvAsync(uploadStream, file.FileName, cancellationToken);

            await using var parseStream = file.OpenReadStream();
            var candidate = await cvParsingService.ParseCvAsync(parseStream, file.FileName, parsingMethod, cancellationToken);
            candidate.CvFileName ??= file.FileName;
            candidate.CvBlobUrl = blobUrl;
            candidate.CvContent ??= string.Empty;

            var savedCandidate = await candidateRepository.AddAsync(candidate, cancellationToken);

            // Save file locally for CV viewer when blob storage is not configured
            if (!blobStorageService.IsConfigured)
            {
                var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", savedCandidate.Id.ToString());
                Directory.CreateDirectory(uploadDir);
                var localPath = Path.Combine(uploadDir, file.FileName);
                await using var localStream = file.OpenReadStream();
                await using var fileWriter = System.IO.File.Create(localPath);
                await localStream.CopyToAsync(fileWriter, cancellationToken);
            }

            return Created($"/api/candidates/{savedCandidate.Id}", savedCandidate);
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (Exception exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to upload and parse the CV.", detail = exception.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<Candidate>>> GetCandidatesAsync(CancellationToken cancellationToken)
    {
        var candidates = await candidateRepository.GetAllAsync(cancellationToken);
        return Ok(candidates);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Candidate>> GetCandidateByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var candidate = await candidateRepository.GetByIdAsync(id, cancellationToken);
        return candidate is null ? NotFound() : Ok(candidate);
    }

    [HttpGet("{id:guid}/matches")]
    public async Task<ActionResult<IReadOnlyCollection<MatchResult>>> GetCandidateMatchesAsync(Guid id, CancellationToken cancellationToken)
    {
        var candidate = await candidateRepository.GetByIdAsync(id, cancellationToken);
        if (candidate is null)
        {
            return NotFound();
        }

        var matches = await jobMatchingService.GetMatchesForCandidateAsync(id, cancellationToken);
        return Ok(matches);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCandidateAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await candidateRepository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        // Cascade delete: remove all match results for this candidate
        await jobMatchingService.DeleteMatchesForCandidateAsync(id, cancellationToken);

        // Clean up local CV files if any
        var localDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", id.ToString());
        if (Directory.Exists(localDir))
        {
            Directory.Delete(localDir, recursive: true);
        }

        return NoContent();
    }

    [HttpGet("{id:guid}/cv")]
    public async Task<IActionResult> DownloadCvAsync(Guid id, CancellationToken cancellationToken)
    {
        var candidate = await candidateRepository.GetByIdAsync(id, cancellationToken);
        if (candidate is null)
        {
            return NotFound();
        }

        // Try blob storage first
        if (blobStorageService.IsConfigured && !string.IsNullOrWhiteSpace(candidate.CvBlobUrl) && !candidate.CvBlobUrl.StartsWith("local://"))
        {
            var stream = await blobStorageService.DownloadCvAsync(candidate.CvBlobUrl, cancellationToken);
            if (stream is not null)
            {
                var contentType = GetContentType(candidate.CvFileName ?? "file.pdf");
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{candidate.CvFileName}\"";
                return File(stream, contentType);
            }
        }

        // Try local file storage
        var localPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", id.ToString(), candidate.CvFileName ?? "cv");
        if (System.IO.File.Exists(localPath))
        {
            var stream = System.IO.File.OpenRead(localPath);
            var contentType = GetContentType(candidate.CvFileName ?? "file.pdf");
            Response.Headers["Content-Disposition"] = $"inline; filename=\"{candidate.CvFileName}\"";
            return File(stream, contentType);
        }

        return NotFound(new { message = "CV file not found." });
    }

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            _ => "application/octet-stream"
        };
    }
}
