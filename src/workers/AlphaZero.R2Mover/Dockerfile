FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/workers/AlphaZero.R2Mover/AlphaZero.R2Mover.csproj", "src/workers/AlphaZero.R2Mover/"]
# Assuming Directory.Packages.props is in root
COPY ["Directory.Packages.props", "."]
RUN dotnet restore "src/workers/AlphaZero.R2Mover/AlphaZero.R2Mover.csproj"
COPY . .
WORKDIR "/src/src/workers/AlphaZero.R2Mover"
RUN dotnet build "AlphaZero.R2Mover.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AlphaZero.R2Mover.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AlphaZero.R2Mover.dll"]
