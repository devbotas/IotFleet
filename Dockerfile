#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app
RUN apt-get update
RUN apt-get install -y nano
RUN apt-get install -y inetutils-ping 
RUN apt-get install -y fonts-symbola
RUN apt-get install -y fonts-dejavu
RUN rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim AS build
WORKDIR /src
COPY ["GreenhouseMonitor/GreenhouseMonitor.csproj", "GreenhouseMonitor/"]
RUN dotnet restore "GreenhouseMonitor/GreenhouseMonitor.csproj"
COPY . .
WORKDIR "/src/GreenhouseMonitor"
RUN dotnet build "GreenhouseMonitor.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GreenhouseMonitor.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GreenhouseMonitor.dll"]