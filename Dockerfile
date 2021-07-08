#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app

# Install python
RUN apt-get update -y
RUN apt-get install -y python3

# Install JRE 11
RUN for i in $(seq 1 8); do mkdir -p "/usr/share/man/man${i}"; done
RUN apt-get install -y default-jre

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src

# Copy python files used for extraction
COPY ["rede.py", ""]
COPY ["text-extraction19.py", ""]

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