FROM mcr.microsoft.com/dotnet/sdk:9.0.313 AS build
WORKDIR /src

COPY . .
RUN dotnet restore WileyCoWeb.Api/WileyCoWeb.Api.csproj
RUN dotnet publish WileyCoWeb.Api/WileyCoWeb.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "WileyCoWeb.Api.dll"]