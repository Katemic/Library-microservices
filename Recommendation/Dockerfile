
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy NuGet configuration and project file
COPY NuGet.Config ./
COPY Recommendation.csproj ./

# Restore NuGet dependencies
RUN dotnet restore Recommendation.csproj --configfile NuGet.Config

# Copy the remaining source files
COPY . .

# Publish the application
RUN dotnet publish Recommendation.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Recommendation.dll"]
