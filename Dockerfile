# Tarantula ingest API. Build context = repository root:  docker build -t tarantula-api .
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Directory.Build.props Tarantula.sln ./
COPY src/ src/
RUN dotnet publish src/Tarantula.Ingest.Api/Tarantula.Ingest.Api.csproj -c Release -o /out --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /out .
# Iran wall-clock like the Pastil services; event times are stored by the server in UTC regardless.
ENV TZ=Asia/Tehran \
    ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=0
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Tarantula.Ingest.Api.dll"]
