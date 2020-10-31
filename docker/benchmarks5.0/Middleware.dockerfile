FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /app
COPY . .
# ENV BenchmarksTargetFramework net5.0
RUN dotnet publish src/Benchmarks/Benchmarks.csproj -c Release -o out -f net5.0 /p:BenchmarksTargetFramework=net5.0 /p:MicrosoftAspNetCoreAppPackageVersion=$ASPNET_VERSION

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS runtime
# ENV ASPNETCORE_URLS http://*:5000
WORKDIR /app
COPY --from=build /app/out ./

ENTRYPOINT ["dotnet", "Benchmarks.dll"]
