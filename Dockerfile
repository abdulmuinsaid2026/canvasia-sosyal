# syntax=docker/dockerfile:1
ARG CANVASIA_RUNTIME_TARGET=web-final
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CanvasiaSocial.sln ./
COPY src/CanvasiaSocial.Domain/CanvasiaSocial.Domain.csproj src/CanvasiaSocial.Domain/
COPY src/CanvasiaSocial.Application/CanvasiaSocial.Application.csproj src/CanvasiaSocial.Application/
COPY src/CanvasiaSocial.Infrastructure/CanvasiaSocial.Infrastructure.csproj src/CanvasiaSocial.Infrastructure/
COPY src/CanvasiaSocial.Web/CanvasiaSocial.Web.csproj src/CanvasiaSocial.Web/
COPY src/CanvasiaSocial.Worker/CanvasiaSocial.Worker.csproj src/CanvasiaSocial.Worker/
COPY tests/CanvasiaSocial.UnitTests/CanvasiaSocial.UnitTests.csproj tests/CanvasiaSocial.UnitTests/
COPY tests/CanvasiaSocial.IntegrationTests/CanvasiaSocial.IntegrationTests.csproj tests/CanvasiaSocial.IntegrationTests/
RUN dotnet restore CanvasiaSocial.sln

COPY . .
RUN dotnet publish src/CanvasiaSocial.Web/CanvasiaSocial.Web.csproj -c Release -o /app/web --no-restore
RUN dotnet publish src/CanvasiaSocial.Worker/CanvasiaSocial.Worker.csproj -c Release -o /app/worker --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS web-final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/canvasia-social/keys && chown -R app:app /var/lib/canvasia-social
COPY --from=build --chown=app:app /app/web .
USER app
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --start-period=20s --retries=3 CMD curl --fail --silent http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "CanvasiaSocial.Web.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS worker-final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8081
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/canvasia-social/keys && chown -R app:app /var/lib/canvasia-social
COPY --from=build --chown=app:app /app/worker .
USER app
EXPOSE 8081
HEALTHCHECK --interval=15s --timeout=3s --start-period=20s --retries=3 CMD curl --fail --silent http://localhost:8081/health/live || exit 1
ENTRYPOINT ["dotnet", "CanvasiaSocial.Worker.dll"]

# Railway builds the final stage by default. Web is the safe default; the Worker
# service selects worker-final with CANVASIA_RUNTIME_TARGET=worker-final.
FROM ${CANVASIA_RUNTIME_TARGET} AS final
