FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY nuget.config ./
COPY PortfolioStudio.sln ./
COPY src/PortfolioStudio/PortfolioStudio.csproj src/PortfolioStudio/
RUN dotnet restore src/PortfolioStudio/PortfolioStudio.csproj

COPY src/ src/
RUN dotnet publish src/PortfolioStudio/PortfolioStudio.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
RUN mkdir -p /app/App_Data /app/wwwroot/uploads

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet PortfolioStudio.dll"]
