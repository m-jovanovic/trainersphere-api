FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Common
COPY ["Common/Shared/Shared.csproj", "Common/Shared/"]
COPY ["Common/Domain/Domain.csproj", "Common/Domain/"]
COPY ["Common/Application/Application.csproj", "Common/Application/"]
COPY ["Common/Infrastructure/Infrastructure.csproj", "Common/Infrastructure/"]

# Modules - Users
COPY ["Modules/Users/Modules.Users.Domain.csproj", "Modules/Users/Modules.Users.Domain/"]
COPY ["Modules/Users/Modules.Users.Application.Boundary.csproj", "Modules/Users/Modules.Application.Boundary/"]
COPY ["Modules/Users/Modules.Users.Application.csproj", "Modules/Users/Modules.Users.Application/"]
COPY ["Modules/Users/Modules.Users.Endpoints.csproj", "Modules/Users/Modules.Users.Endpoints/"]
COPY ["Modules/Users/Modules.Users.Infrastructure.csproj", "Modules/Users/Modules.Users.Infrastructure/"]

# Executable
COPY ["App/App.csproj", "App/"]

RUN dotnet restore "App/App.csproj"

COPY . .

WORKDIR "/src/App"
RUN dotnet build "App.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "App.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "App.dll"]