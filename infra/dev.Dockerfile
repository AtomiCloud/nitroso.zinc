FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.18 AS build
# hadolint ignore=DL3018
RUN apk add --no-cache tzdata icu-libs ca-certificates && update-ca-certificates
WORKDIR /app
COPY ["App/App.csproj", "App/"]
ENV DOTNET_WATCH_RESTART_ON_RUDE_EDIT=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0
RUN dotnet restore "App/App.csproj"
COPY . .
WORKDIR /app/App
ENTRYPOINT ["dotnet", "watch"]
