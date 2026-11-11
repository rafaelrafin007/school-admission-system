# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for dependency resolution
COPY ["SchoolAdmission.sln", "./"]
COPY ["src/SchoolAdmission.Domain/SchoolAdmission.Domain.csproj", "src/SchoolAdmission.Domain/"]
COPY ["src/SchoolAdmission.Application/SchoolAdmission.Application.csproj", "src/SchoolAdmission.Application/"]
COPY ["src/SchoolAdmission.Infrastructure/SchoolAdmission.Infrastructure.csproj", "src/SchoolAdmission.Infrastructure/"]
COPY ["src/SchoolAdmission.Api/SchoolAdmission.Api.csproj", "src/SchoolAdmission.Api/"]
COPY ["tests/SchoolAdmission.UnitTests/SchoolAdmission.UnitTests.csproj", "tests/SchoolAdmission.UnitTests/"]
COPY ["tests/SchoolAdmission.IntegrationTests/SchoolAdmission.IntegrationTests.csproj", "tests/SchoolAdmission.IntegrationTests/"]

RUN dotnet restore "SchoolAdmission.sln"

# Copy source code
COPY . .

# Publish the API
RUN dotnet publish "src/SchoolAdmission.Api/SchoolAdmission.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copy published application
COPY --from=build /app/publish .

# Expose port 8080
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

USER appuser

ENTRYPOINT ["dotnet", "SchoolAdmission.Api.dll"]
