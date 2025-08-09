FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine3.18 AS base
WORKDIR /app
# hadolint ignore=DL3018
RUN apk add --no-cache tzdata icu-libs ca-certificates && update-ca-certificates

FROM oven/bun:1.2.13 as emails
WORKDIR /src
COPY ["App/Templates/Email/package.json", "App/Templates/Email/package.json"]
COPY ["App/Templates/Email/bun.lock", "App/Templates/Email/bun.lock"]
WORKDIR "/src/App/Templates/Email"
RUN bun i
WORKDIR /src
COPY . .
WORKDIR "/src/App/Templates/Email"
RUN bun run email export -p --outDir templates


FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.18 AS build
ARG TARGETARCH
WORKDIR /src
COPY ["App/App.csproj", "App/"]
RUN dotnet restore -a $TARGETARCH "App/App.csproj"
COPY . .
COPY --from=emails "/src/App/Templates/Email/templates" "/src/App/Templates/Email/templates"
WORKDIR "/src/App"
RUN dotnet build "App.csproj" -a $TARGETARCH -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "App.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0
ENTRYPOINT ["dotnet", "App.dll"]
