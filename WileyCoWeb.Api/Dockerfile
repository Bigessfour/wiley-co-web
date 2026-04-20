FROM mcr.microsoft.com/dotnet/sdk:9.0.313 AS build
WORKDIR /src

COPY . .
RUN dotnet restore WileyCoWeb.Api/WileyCoWeb.Api.csproj
RUN dotnet publish WileyCoWeb.Api/WileyCoWeb.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

RUN groupadd -r appuser && useradd -r -g appuser appuser
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates \
    && update-ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
RUN chown -R appuser:appuser /app

USER appuser

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD dotnet WileyCoWeb.Api.dll --health-check || exit 1

ENTRYPOINT ["dotnet", "WileyCoWeb.Api.dll"]