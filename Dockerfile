# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/MacroRegimeFactorMonitor/MacroRegimeFactorMonitor.csproj", "src/MacroRegimeFactorMonitor/"]
RUN dotnet restore "src/MacroRegimeFactorMonitor/MacroRegimeFactorMonitor.csproj"

COPY . .
RUN dotnet publish "src/MacroRegimeFactorMonitor/MacroRegimeFactorMonitor.csproj" \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MacroRegimeFactorMonitor.dll"]
