FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine3.18 AS base
WORKDIR /app
# hadolint ignore=DL3018
RUN apk add --no-cache tzdata icu-libs ca-certificates && update-ca-certificates

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.18 AS build
ARG TARGETARCH
WORKDIR /src
COPY ["App/App.csproj", "App/"]
RUN dotnet restore -a $TARGETARCH "App/App.csproj"
COPY . .
WORKDIR "/src/App"
RUN dotnet build "App.csproj" -a $TARGETARCH -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "App.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0
ENTRYPOINT ["dotnet", "App.dll"]
