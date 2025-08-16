# Fish-Smart Containerization - Optimized for ONNX Runtime and Cloud Deployment
# This Dockerfile creates a production-ready container with full ONNX Runtime support

#==============================================================================
# Stage 1: Build Environment
#==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for dependency resolution
COPY Fish-Smart.sln ./
COPY Members/Members.csproj Members/

# Restore dependencies
RUN dotnet restore Fish-Smart.sln

# Copy all source code
COPY . .

# Build the application
WORKDIR /src/Members
RUN dotnet build Members.csproj -c Release -o /app/build

#==============================================================================
# Stage 2: Publish
#==============================================================================
FROM build AS publish
RUN dotnet publish Members.csproj -c Release -o /app/publish /p:UseAppHost=false

#==============================================================================
# Stage 3: Production Runtime with ONNX Runtime Support
#==============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base

# Install system dependencies for ONNX Runtime
RUN apt-get update && apt-get install -y \
    # Core system libraries for ONNX Runtime
    libc6-dev \
    libstdc++6 \
    libgomp1 \
    # Additional libraries that might be needed
    libgcc1 \
    libssl3 \
    # Image processing libraries (for ImageSharp)
    libjpeg62-turbo \
    libpng16-16 \
    # Memory and performance optimization
    libnuma1 \
    # Clean up to reduce image size
    && rm -rf /var/lib/apt/lists/* \
    && apt-get clean

# Set environment variables for optimal performance
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
ENV ASPNETCORE_URLS=http://+:8080

# Create non-root user for security
RUN groupadd -r fishsmart && useradd -r -g fishsmart fishsmart

# Set working directory
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Create directories for application data with proper permissions
RUN mkdir -p /app/wwwroot/Galleries /app/wwwroot/Images /app/wwwroot/Models /app/ProtectedFiles \
    && chown -R fishsmart:fishsmart /app

# Switch to non-root user
USER fishsmart

# Expose port (Railway.app typically uses 8080)
EXPOSE 8080

# Health check endpoint (using dotnet to check if process is running)
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD pgrep dotnet > /dev/null || exit 1

# Entry point
ENTRYPOINT ["dotnet", "Members.dll"]