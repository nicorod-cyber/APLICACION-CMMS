FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:78235e09001f52b6592c458ac010775ebac6725422e80cd0c1650590f67b2743 AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:8.0@sha256:2f202e1169ec507bdc07007cf68c14d0ff3a098110b17c460a60185e1f36a9d1 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
RUN mkdir -p /app/data/imports /app/data/templates /app/data/sharepoint-simulated /app/logs && chown -R app:app /app/data /app/logs && chmod -R u+rwX,g+rwX,o-rwx /app/data /app/logs
USER app
ENTRYPOINT ["dotnet", "MaintenanceCMMS.Api.dll"]

