FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/lambdas/AlphaZero.VideoAnalyzer/AlphaZero.VideoAnalyzer.csproj", "src/lambdas/AlphaZero.VideoAnalyzer/"]
COPY ["Directory.Packages.props", "."]
RUN dotnet restore "src/lambdas/AlphaZero.VideoAnalyzer/AlphaZero.VideoAnalyzer.csproj"
COPY . .
WORKDIR "/src/src/lambdas/AlphaZero.VideoAnalyzer"
RUN dotnet publish "AlphaZero.VideoAnalyzer.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /var/task
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg ca-certificates && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
