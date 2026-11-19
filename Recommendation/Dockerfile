
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Recommendation.csproj ./
RUN dotnet restore Recommendation.csproj


COPY . .

RUN dotnet publish Recommendation.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "Recommendation.dll"]