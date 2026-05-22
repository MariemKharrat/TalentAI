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
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
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

app.UseHttpsRedirection();
app.UseCors("ReactFrontend");
app.UseAuthorization();
app.MapControllers();

app.Run();
