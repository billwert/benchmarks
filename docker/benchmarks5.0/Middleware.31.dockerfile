FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /app
COPY . .
# ENV BenchmarksTargetFramework netcoreapp3.1
RUN dotnet publish src/Benchmarks/Benchmarks.csproj -c Release -o out -f netcoreapp3.1 /p:BenchmarksTargetFramework=netcoreapp3.1 /p:MicrosoftAspNetCoreAppPackageVersion=3.1.4

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS runtime
ENV ASPNETCORE_URLS http://*:5000
WORKDIR /app
COPY --from=build /app/out ./

ENTRYPOINT ["dotnet", "Benchmarks.dll"]
