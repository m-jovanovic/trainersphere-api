# Build context: repository root (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/TrainerSphere.SharedKernel/TrainerSphere.SharedKernel.csproj src/TrainerSphere.SharedKernel/
COPY src/TrainerSphere.Api/TrainerSphere.Api.csproj src/TrainerSphere.Api/
RUN dotnet restore src/TrainerSphere.Api/TrainerSphere.Api.csproj

COPY src/ src/
RUN dotnet publish src/TrainerSphere.Api/TrainerSphere.Api.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

HEALTHCHECK --interval=5s --timeout=3s --start-period=10s --retries=12 \
    CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /health/ready HTTP/1.0\\r\\nHost: localhost\\r\\n\\r\\n' >&3 && head -n 1 <&3 | grep -q ' 200 '"]

ENTRYPOINT ["dotnet", "TrainerSphere.Api.dll"]
