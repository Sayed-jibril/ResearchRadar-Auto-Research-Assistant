# Use the official .NET 8 runtime as the base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Use the .NET 8 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["ScholarLens.Api/ScholarLens.Api.csproj", "ScholarLens.Api/"]
RUN dotnet restore "ScholarLens.Api/ScholarLens.Api.csproj"

# Copy the entire source code
COPY . .

# Build the application
WORKDIR "/src/ScholarLens.Api"
RUN dotnet build "ScholarLens.Api.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "ScholarLens.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Create the runtime image
FROM base AS final
WORKDIR /app

# Create directories for file storage
RUN mkdir -p /app/reports /app/temp

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=publish /app/publish .

# Create a non-root user
RUN addgroup --system --gid 1001 dotnet \
    && adduser --system --uid 1001 --ingroup dotnet --shell /bin/false dotnet \
    && chown -R dotnet:dotnet /app

USER dotnet

ENTRYPOINT ["dotnet", "ScholarLens.Api.dll"]
