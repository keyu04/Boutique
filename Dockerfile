# ================================
# Build stage
# ================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy project files first
# This allows Docker to cache the restore step
COPY ["src/Boutique.Api/Boutique.Api.csproj", "src/Boutique.Api/"]
COPY ["src/Boutique.Application/Boutique.Application.csproj", "src/Boutique.Application/"]
COPY ["src/Boutique.Domain/Boutique.Domain.csproj", "src/Boutique.Domain/"]
COPY ["src/Boutique.Infrastructure/Boutique.Infrastructure.csproj", "src/Boutique.Infrastructure/"]

# Restore NuGet packages
RUN dotnet restore "src/Boutique.Api/Boutique.Api.csproj"

# Copy the rest of the source code
COPY . .

# Publish the API
WORKDIR "/src/src/Boutique.Api"

RUN dotnet publish "Boutique.Api.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false


# ================================
# Runtime stage
# ================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

# Cloud hosting port
ENV ASPNETCORE_URLS=http://+:${PORT}

EXPOSE 10000

ENTRYPOINT ["dotnet", "Boutique.Api.dll"]