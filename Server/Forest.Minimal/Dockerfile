FROM mcr.microsoft.com/dotnet/aspnet:11.0-alpine-amd64 AS base
WORKDIR /app
EXPOSE 7010

ENV ASPNETCORE_HTTP_PORTS=7010

USER app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:11.0-alpine-amd64 AS build
ARG configuration=Release
WORKDIR /src
COPY ["Server/Forest.Minimal/Forest.Minimal.csproj", "Server/Forest.Minimal/"]
RUN dotnet restore "Server/Forest.Minimal/Forest.Minimal.csproj"
COPY . .
WORKDIR "/src/Server/Forest.Minimal"
RUN dotnet build "Forest.Minimal.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "Forest.Minimal.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Forest.Minimal.dll"]
