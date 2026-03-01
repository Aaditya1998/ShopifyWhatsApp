# ──────────────────────────────────────────────
# Stage 1: Build
# ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy project file and restore
COPY src/ShopifyWhatsApp.csproj ./src/
RUN dotnet restore ./src/ShopifyWhatsApp.csproj

# Copy everything and build
COPY src/ ./src/
RUN dotnet publish ./src/ShopifyWhatsApp.csproj -c Release -o /app/publish

# ──────────────────────────────────────────────
# Stage 2: Runtime
# ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create a non-root user
RUN adduser --disabled-password --gecos "" appuser

COPY --from=build /app/publish .

# Create logs directory
RUN mkdir -p /app/logs && chown -R appuser:appuser /app

USER appuser

EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD dotnet --list-runtimes > /dev/null 2>&1 || exit 1

ENTRYPOINT ["dotnet", "ShopifyWhatsApp.dll"]
