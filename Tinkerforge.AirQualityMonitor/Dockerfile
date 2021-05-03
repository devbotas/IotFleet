#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app
RUN apt-get update
RUN apt-get install -y nano
RUN apt-get install -y inetutils-ping 
RUN rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim-amd64 AS build
WORKDIR /src
COPY ["Tinkerforge.AirQualityMonitor/Tinkerforge.AirQualityMonitor.csproj", "Tinkerforge.AirQualityMonitor/"]
RUN dotnet restore "Tinkerforge.AirQualityMonitor/Tinkerforge.AirQualityMonitor.csproj"
COPY . .
WORKDIR "/src/Tinkerforge.AirQualityMonitor"
RUN dotnet build "Tinkerforge.AirQualityMonitor.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Tinkerforge.AirQualityMonitor.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Tinkerforge.AirQualityMonitor.dll"]