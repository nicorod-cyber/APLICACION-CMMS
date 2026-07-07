FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY backend/MaintenanceCMMS.sln backend/
COPY backend/src/MaintenanceCMMS.Api/MaintenanceCMMS.Api.csproj backend/src/MaintenanceCMMS.Api/
COPY backend/src/MaintenanceCMMS.Application/MaintenanceCMMS.Application.csproj backend/src/MaintenanceCMMS.Application/
COPY backend/src/MaintenanceCMMS.Domain/MaintenanceCMMS.Domain.csproj backend/src/MaintenanceCMMS.Domain/
COPY backend/src/MaintenanceCMMS.Infrastructure/MaintenanceCMMS.Infrastructure.csproj backend/src/MaintenanceCMMS.Infrastructure/
COPY backend/tests/MaintenanceCMMS.Tests/MaintenanceCMMS.Tests.csproj backend/tests/MaintenanceCMMS.Tests/

RUN dotnet restore backend/MaintenanceCMMS.sln

COPY backend backend
RUN dotnet publish backend/src/MaintenanceCMMS.Api/MaintenanceCMMS.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MaintenanceCMMS.Api.dll"]

