#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM python:3.9-buster AS base

# Install .NET 5.0 Runtime
RUN apt-get install -y wget
RUN wget https://packages.microsoft.com/config/debian/10/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
RUN dpkg -i packages-microsoft-prod.deb
RUN apt-get update -y
RUN apt-get install -y apt-transport-https
RUN apt-get update -y
RUN apt-get install -y aspnetcore-runtime-5.0

WORKDIR /app

# Install JRE 11
RUN for i in $(seq 1 8); do mkdir -p "/usr/share/man/man${i}"; done
RUN apt-get install -y default-jre

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src

# Copy and build project
COPY ["Crawler/Crawler.csproj", "Crawler/"]
RUN dotnet restore "Crawler/Crawler.csproj"
COPY . .
WORKDIR "/src/Crawler"
RUN dotnet build "Crawler.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Crawler.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Crawler.dll"]