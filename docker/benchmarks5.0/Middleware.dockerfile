FROM mcr.microsoft.com/dotnet/core/sdk:5.0 AS build
WORKDIR /app
COPY . .
# ENV BenchmarksTargetFramework netcoreapp3.1
RUN dotnet publish src/Benchmarks/Benchmarks.csproj -c Release -o out -f netcoreapp5.0 /p:BenchmarksTargetFramework=netcoreapp5.0 /p:MicrosoftAspNetCoreAppPackageVersion=5.0.0-preview.3.20215.14

FROM mcr.microsoft.com/dotnet/core/aspnet:5.0 AS runtime
ENV ASPNETCORE_URLS http://*:5000
WORKDIR /app
COPY --from=build /app/out ./

ENTRYPOINT ["dotnet", "Benchmarks.dll"]
