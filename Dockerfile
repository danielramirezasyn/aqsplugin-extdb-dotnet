# mcr.microsoft.com/dotnet/sdk:10.0 and mcr.microsoft.com/dotnet/aspnet:10.0 are published as
# multi-arch manifests (linux/amd64 + linux/arm64v8) under the plain "10.0" tag, so this image
# builds and runs natively on Apple Silicon (M-series) without emulation or extra --platform flags.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/AqsPluginExtDb/AqsPluginExtDb.csproj src/AqsPluginExtDb/
RUN dotnet restore src/AqsPluginExtDb/AqsPluginExtDb.csproj

COPY src/AqsPluginExtDb/ src/AqsPluginExtDb/
RUN dotnet publish src/AqsPluginExtDb/AqsPluginExtDb.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS= \
    PORT=8000

RUN mkdir -p /data && chown -R app:app /data
VOLUME ["/data"]

COPY --from=build /app/publish .

USER app
EXPOSE 8000

ENTRYPOINT ["dotnet", "AqsPluginExtDb.dll"]
