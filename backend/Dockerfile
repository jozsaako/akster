# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies first (better layer caching)
COPY PetSitting.Api/*.csproj PetSitting.Api/
COPY PetSitting.Application/*.csproj PetSitting.Application/
COPY PetSitting.Domain/*.csproj PetSitting.Domain/
COPY PetSitting.Infrastructure/*.csproj PetSitting.Infrastructure/
RUN dotnet restore PetSitting.Api/PetSitting.Api.csproj

# Copy only the new projects and publish
COPY PetSitting.Api/ PetSitting.Api/
COPY PetSitting.Application/ PetSitting.Application/
COPY PetSitting.Domain/ PetSitting.Domain/
COPY PetSitting.Infrastructure/ PetSitting.Infrastructure/
RUN dotnet publish PetSitting.Api/PetSitting.Api.csproj -c Release --no-restore -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PetSitting.Api.dll"]
