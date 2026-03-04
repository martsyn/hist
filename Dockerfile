# Stage 1: Build Vue frontend
FROM node:22-alpine AS frontend-build
WORKDIR /frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# Stage 2: Build .NET app
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS dotnet-build
WORKDIR /src
COPY src/Hist.Server/Hist.Server.csproj ./Hist.Server/
RUN dotnet restore ./Hist.Server/Hist.Server.csproj
COPY src/Hist.Server/ ./Hist.Server/
RUN dotnet publish ./Hist.Server/Hist.Server.csproj -c Release -o /out

# Stage 3: Runtime — ClickHouse image has CH pre-installed with correct config layout
FROM clickhouse/clickhouse-server:24.8

ARG S6_VERSION=3.2.0.2

RUN apt-get update && apt-get install -y --no-install-recommends \
    wget xz-utils netcat-openbsd libicu-dev \
    && rm -rf /var/lib/apt/lists/*

# Install s6-overlay
RUN ARCH=$(uname -m) && \
    wget -qO /tmp/s6.tar.xz \
        "https://github.com/just-containers/s6-overlay/releases/download/v${S6_VERSION}/s6-overlay-${ARCH}.tar.xz" && \
    wget -qO /tmp/s6-noarch.tar.xz \
        "https://github.com/just-containers/s6-overlay/releases/download/v${S6_VERSION}/s6-overlay-noarch.tar.xz" && \
    tar -C / -Jxpf /tmp/s6-noarch.tar.xz && \
    tar -C / -Jxpf /tmp/s6.tar.xz && \
    rm /tmp/s6*.tar.xz

# Install ASP.NET Core 9 runtime
RUN wget -qO /tmp/dotnet-install.sh https://dot.net/v1/dotnet-install.sh && \
    chmod +x /tmp/dotnet-install.sh && \
    /tmp/dotnet-install.sh --runtime aspnetcore --version 9.0.3 --install-dir /usr/share/dotnet && \
    ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet && \
    rm /tmp/dotnet-install.sh

# Copy application
COPY --from=dotnet-build /out/ /app/hist/
COPY --from=frontend-build /frontend/dist/ /app/hist/wwwroot/

# Override ClickHouse config (base config.xml already exists in the image)
COPY clickhouse/config.d/ /etc/clickhouse-server/config.d/
COPY clickhouse/users.d/ /etc/clickhouse-server/users.d/

# Copy s6 service definitions
COPY s6-overlay/ /etc/s6-overlay/
RUN chmod +x /etc/s6-overlay/s6-rc.d/clickhouse/run \
              /etc/s6-overlay/s6-rc.d/hist/run

EXPOSE 8088 8123 9000

ENTRYPOINT ["/init"]
