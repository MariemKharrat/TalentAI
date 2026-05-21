using System.Text.Json;
using CareerApp.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerApp.Infrastructure.Data;

public class AppDbContext : DbContext
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly ValueComparer<List<string>> StringListComparer = new(
        (left, right) => SerializeStringList(left).Equals(SerializeStringList(right), StringComparison.Ordinal),
        value => SerializeStringList(value).GetHashCode(StringComparison.Ordinal),
        value => DeserializeStringList(SerializeStringList(value)));

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Candidate> Candidates => Set<Candidate>();

    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<MatchResult> MatchResults => Set<MatchResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureCandidate(modelBuilder.Entity<Candidate>());
        ConfigureJob(modelBuilder.Entity<Job>());
        ConfigureMatchResult(modelBuilder.Entity<MatchResult>());
    }

    private static void ConfigureCandidate(EntityTypeBuilder<Candidate> candidate)
    {
        candidate.ToTable("Candidates");
        candidate.HasKey(entity => entity.Id);
        candidate.Ignore(entity => entity.JobMatches);

        candidate.Property(entity => entity.FullName)
            .HasMaxLength(200)
            .IsRequired();

        candidate.Property(entity => entity.Email)
            .HasMaxLength(320);

        candidate.Property(entity => entity.Skills)
            .HasMaxLength(4000);

        candidate.Property(entity => entity.Summary)
            .HasMaxLength(4000);

        candidate.Property(entity => entity.CvFileName)
            .HasMaxLength(260);

        candidate.Property(entity => entity.CvContent)
            .HasColumnType("nvarchar(max)");

        candidate.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        candidate.HasMany<MatchResult>()
            .WithOne()
            .HasForeignKey(entity => entity.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureJob(EntityTypeBuilder<Job> job)
    {
        job.ToTable("Jobs");
        job.HasKey(entity => entity.Id);
        job.Ignore(entity => entity.CandidateMatches);

        job.Property(entity => entity.Title)
            .HasMaxLength(200)
            .IsRequired();

        job.Property(entity => entity.Department)
            .HasMaxLength(200);

        job.Property(entity => entity.Description)
            .HasColumnType("nvarchar(max)");

        job.Property(entity => entity.Requirements)
            .HasColumnType("nvarchar(max)");

        job.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        job.Property(entity => entity.UpdatedAtUtc)
            .IsRequired();

        job.HasMany<MatchResult>()
            .WithOne()
            .HasForeignKey(entity => entity.JobId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureMatchResult(EntityTypeBuilder<MatchResult> matchResult)
    {
        matchResult.ToTable("MatchResults");
        matchResult.HasKey(entity => entity.Id);

        matchResult.Property(entity => entity.Score)
            .HasPrecision(5, 2);

        matchResult.Property(entity => entity.MatchLevel)
            .HasConversion<string>()
            .HasMaxLength(20);

        matchResult.Property(entity => entity.Explanation)
            .HasMaxLength(4000)
            .IsRequired();

        matchResult.Property(entity => entity.SkillMatches)
            .HasConversion(
                value => SerializeStringList(value),
                value => DeserializeStringList(value))
            .Metadata.SetValueComparer(StringListComparer);

        matchResult.Property(entity => entity.SkillGaps)
            .HasConversion(
                value => SerializeStringList(value),
                value => DeserializeStringList(value))
            .Metadata.SetValueComparer(StringListComparer);

        matchResult.HasIndex(entity => new { entity.CandidateId, entity.JobId, entity.CreatedAt });
    }

    private static string SerializeStringList(List<string>? value)
    {
        return JsonSerializer.Serialize(value ?? [], SerializerOptions);
    }

    private static List<string> DeserializeStringList(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<string>>(json, SerializerOptions) ?? [];
    }
}
