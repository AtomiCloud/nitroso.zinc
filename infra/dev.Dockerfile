FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /app
COPY ["App/App.csproj", "App/"]
ENV DOTNET_WATCH_RESTART_ON_RUDE_EDIT=true
RUN dotnet restore "App/App.csproj"
COPY . .
WORKDIR /app/App
ENTRYPOINT ["dotnet", "watch"]
