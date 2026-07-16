using CareerApp.Core.Interfaces;
using CareerApp.Infrastructure.Configuration;
using CareerApp.Infrastructure.Data;
using CareerApp.Infrastructure.Repositories;
using CareerApp.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<AzureAIOptions>(builder.Configuration.GetSection(AzureAIOptions.SectionName));
builder.Services.Configure<CosmosDbOptions>(builder.Configuration.GetSection(CosmosDbOptions.SectionName));
builder.Services.Configure<BlobStorageOptions>(builder.Configuration.GetSection(BlobStorageOptions.SectionName));

// Cosmos DB
builder.Services.AddSingleton<CosmosDbService>();

// Blob Storage
builder.Services.AddSingleton<BlobStorageService>();

// Repositories
builder.Services.AddScoped<ICandidateRepository, CandidateRepository>();
builder.Services.AddScoped<IJobRepository, JobRepository>();

// AI Services
builder.Services.AddHttpClient<ContentUnderstandingCvParser>();
builder.Services.AddScoped<ContentUnderstandingCvParser>();
builder.Services.AddScoped<ICvParsingService, CvParsingService>();
builder.Services.AddScoped<IJobMatchingService, JobMatchingService>();
builder.Services.AddScoped<IJobDescriptionGenerator, JobDescriptionGeneratorService>();

builder.Services.AddControllers();

// Allowed CORS origins are configurable via the "Cors:AllowedOrigins" setting
// (e.g. env var Cors__AllowedOrigins="https://talentai-web...azurecontainerapps.io").
// Falls back to the local React dev server when not configured.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>();

if (allowedOrigins is null || allowedOrigins.Length == 0)
{
    allowedOrigins = new[] { "http://localhost:3000" };
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddOpenApi();

var app = builder.Build();

// Initialize Cosmos DB containers
using (var scope = app.Services.CreateScope())
{
    var cosmosDb = scope.ServiceProvider.GetRequiredService<CosmosDbService>();
    await cosmosDb.InitializeAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
    // TLS is terminated by the Container Apps ingress in production, so HTTPS
    // redirection only runs locally to avoid redirect loops behind the proxy.
    app.UseHttpsRedirection();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { message = "An unexpected error occurred." });
        });
    });
}

app.UseCors("ReactFrontend");
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
