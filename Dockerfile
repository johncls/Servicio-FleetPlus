FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY global.json ./
COPY src/FleetPulse.Domain/FleetPulse.Domain.csproj src/FleetPulse.Domain/
COPY src/FleetPulse.Application/FleetPulse.Application.csproj src/FleetPulse.Application/
COPY src/FleetPulse.Infrastructure/FleetPulse.Infrastructure.csproj src/FleetPulse.Infrastructure/
COPY src/FleetPulse.Api/FleetPulse.Api.csproj src/FleetPulse.Api/
RUN dotnet restore src/FleetPulse.Api/FleetPulse.Api.csproj
COPY src/ src/
RUN dotnet publish src/FleetPulse.Api/FleetPulse.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FleetPulse.Api.dll"]
