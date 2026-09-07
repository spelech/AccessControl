FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY Directory.Build.props AccessControl.slnx ./
COPY src/ src/
COPY tests/ tests/
RUN dotnet publish tests/AccessControl.Tests.Simulator/AccessControl.Tests.Simulator.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "AccessControl.Tests.Simulator.dll"]
