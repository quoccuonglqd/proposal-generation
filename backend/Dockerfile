# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["Cherry.Api/Cherry.Api.csproj", "Cherry.Api/"]
COPY ["Cherry.Core/Cherry.Core.csproj", "Cherry.Core/"]
COPY ["Cherry.Infrastructure/Cherry.Infrastructure.csproj", "Cherry.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "Cherry.Api/Cherry.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/Cherry.Api"
RUN dotnet build "Cherry.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Cherry.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create storage directory
RUN mkdir -p /app/Storage/templates

# Copy published output
COPY --from=publish /app/publish .

# Expose port
EXPOSE 5008

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5008
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "Cherry.Api.dll"]
