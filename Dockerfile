# syntax=docker/dockerfile:1.7

ARG DOTNET_VERSION=8.0

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-bookworm-slim AS runtime-base
ARG INSTALL_PLAYWRIGHT_BROWSERS=true

ENV DEBIAN_FRONTEND=noninteractive \
    APP_HOME=/app \
    APP_DATA_ROOT=/data \
    APP_SQLITE_PATH=/data/sqlite/sandra-maya.db \
    APP_UPLOADS_DIR=/data/files \
    APP_CAPABILITIES_DIR=/data/capabilities \
    APP_GENERATED_CAPABILITIES_DIR=/data/capabilities/generated \
    APP_WORK_DIR=/data/work \
    APP_TEMP_DIR=/data/tmp \
    PLAYWRIGHT_BROWSERS_PATH=/ms-playwright \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    PYTHONUNBUFFERED=1 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_URLS=http://+:8080

SHELL ["/bin/bash", "-o", "pipefail", "-c"]

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        bash \
        ca-certificates \
        curl \
        git \
        nodejs \
        npm \
        python3 \
        python3-pip \
        python3-venv \
        tini \
        xdg-utils \
        fonts-liberation \
        libasound2 \
        libatk-bridge2.0-0 \
        libatk1.0-0 \
        libatspi2.0-0 \
        libcairo2 \
        libcups2 \
        libdbus-1-3 \
        libdrm2 \
        libfontconfig1 \
        libfreetype6 \
        libgbm1 \
        libglib2.0-0 \
        libgtk-3-0 \
        libnspr4 \
        libnss3 \
        libpango-1.0-0 \
        libpangocairo-1.0-0 \
        libx11-6 \
        libx11-xcb1 \
        libxcb1 \
        libxcomposite1 \
        libxdamage1 \
        libxext6 \
        libxfixes3 \
        libxkbcommon0 \
        libxrandr2 \
        libxrender1 \
        libxshmfence1 \
    && npm install --global --no-update-notifier --no-fund playwright \
    && if [[ "${INSTALL_PLAYWRIGHT_BROWSERS}" == "true" ]]; then playwright install chromium; fi \
    && ln -sf /usr/bin/python3 /usr/local/bin/python \
    && mkdir -p \
        "${APP_HOME}" \
        "${APP_DATA_ROOT}/sqlite" \
        "${APP_UPLOADS_DIR}" \
        "${APP_CAPABILITIES_DIR}" \
        "${APP_GENERATED_CAPABILITIES_DIR}" \
        "${APP_WORK_DIR}" \
        "${APP_TEMP_DIR}" \
        "${PLAYWRIGHT_BROWSERS_PATH}" \
        /opt/sandra-maya \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-bookworm-slim AS build
ARG PROJECT_PATH=
ARG SOLUTION_PATH=

SHELL ["/bin/bash", "-o", "pipefail", "-c"]
WORKDIR /src

COPY . .

RUN set -euo pipefail; \
    publish_target=""; \
    if [[ -n "${PROJECT_PATH}" && -f "${PROJECT_PATH}" ]]; then \
        publish_target="${PROJECT_PATH}"; \
    elif [[ -n "${SOLUTION_PATH}" && -f "${SOLUTION_PATH}" ]]; then \
        publish_target="${SOLUTION_PATH}"; \
    else \
        mapfile -t executable_projects < <(find . -maxdepth 6 -type f -name '*.csproj' ! -path '*/tests/*' -print | sort | while read -r project; do \
            if grep -Eq 'Sdk="Microsoft\.NET\.Sdk\.Web"|<OutputType>\s*(Exe|WinExe)\s*</OutputType>' "${project}"; then \
                printf '%s\n' "${project}"; \
            fi; \
        done); \
        if [[ "${#executable_projects[@]}" -eq 1 ]]; then \
            publish_target="${executable_projects[0]}"; \
        fi; \
    fi; \
    if [[ -n "${publish_target}" ]]; then \
        echo "Publishing ${publish_target}"; \
        dotnet publish "${publish_target}" -c Release -o /out /p:UseAppHost=false; \
    else \
        echo "No runnable .NET project was detected; building foundation-only image."; \
        mkdir -p /out; \
        printf '%s\n%s\n%s\n' \
            'No runnable .NET host project was published into this image.' \
            'Set the PROJECT_PATH build argument to your app entrypoint project, for example:' \
            '  --build-arg PROJECT_PATH=src/SandraMaya.Host/SandraMaya.Host.csproj' \
            > /out/BUILD_INFO.txt; \
    fi

FROM runtime-base AS final

COPY scripts/docker-entrypoint.sh /opt/sandra-maya/docker-entrypoint.sh
RUN chmod +x /opt/sandra-maya/docker-entrypoint.sh

COPY --from=build /out/ /app/

ENTRYPOINT ["/usr/bin/tini", "--", "/opt/sandra-maya/docker-entrypoint.sh"]
