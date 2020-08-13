FROM mcr.microsoft.com/dotnet/core/sdk:2.1 AS build
WORKDIR /app
COPY . .
RUN dotnet --info
# ENV BenchmarksTargetFramework netcoreapp2.1
RUN dotnet publish src/Benchmarks/Benchmarks.csproj -c Release -o out -f netcoreapp2.1 /p:BenchmarksTargetFramework=netcoreapp2.1 /p:MicrosoftAspNetCoreAppPackageVersion=2.1.18

FROM mcr.microsoft.com/dotnet/core/aspnet:2.1 AS runtime
ENV ASPNETCORE_URLS http://*:5000
WORKDIR /app
COPY --from=build /app/src/Benchmarks/out ./

ENTRYPOINT ["dotnet", "Benchmarks.dll"]
