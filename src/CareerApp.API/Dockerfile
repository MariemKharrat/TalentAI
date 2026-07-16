# Build context must be the "src" directory (contains all three projects).
# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (layer caching) using only the project files.
COPY CareerApp.API/CareerApp.API.csproj CareerApp.API/
COPY CareerApp.Core/CareerApp.Core.csproj CareerApp.Core/
COPY CareerApp.Infrastructure/CareerApp.Infrastructure.csproj CareerApp.Infrastructure/
RUN dotnet restore CareerApp.API/CareerApp.API.csproj

# Copy the remaining source and publish.
COPY . .
RUN dotnet publish CareerApp.API/CareerApp.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "CareerApp.API.dll"]
