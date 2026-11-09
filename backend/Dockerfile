FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached unless a project file changes).
COPY SchoolAdmission.sln Directory.Build.props global.json ./
COPY src/SchoolAdmission.Domain/SchoolAdmission.Domain.csproj                 src/SchoolAdmission.Domain/
COPY src/SchoolAdmission.Application/SchoolAdmission.Application.csproj       src/SchoolAdmission.Application/
COPY src/SchoolAdmission.Infrastructure/SchoolAdmission.Infrastructure.csproj src/SchoolAdmission.Infrastructure/
COPY src/SchoolAdmission.Api/SchoolAdmission.Api.csproj                       src/SchoolAdmission.Api/
COPY tests/SchoolAdmission.UnitTests/SchoolAdmission.UnitTests.csproj               tests/SchoolAdmission.UnitTests/
COPY tests/SchoolAdmission.IntegrationTests/SchoolAdmission.IntegrationTests.csproj tests/SchoolAdmission.IntegrationTests/
RUN dotnet restore SchoolAdmission.sln

COPY . .
RUN dotnet publish src/SchoolAdmission.Api/SchoolAdmission.Api.csproj -c Release -o /app/publish --no-restore

# `docker compose --profile test run --rm backend-tests`
FROM build AS test
ENTRYPOINT ["dotnet", "test", "SchoolAdmission.sln", "-c", "Release", "--no-restore", "--logger", "console;verbosity=normal"]

# Default target: must stay the LAST stage.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "SchoolAdmission.Api.dll"]
