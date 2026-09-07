# Multi-stage Dockerfile: Node Vite build + .NET 10 SDK publish + runtime
FROM node:22-alpine AS ui-build
WORKDIR /app/src/AccessControl.UI
COPY src/AccessControl.UI/package*.json ./
RUN npm ci
COPY src/AccessControl.UI/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY Directory.Build.props AccessControl.slnx ./
COPY src/ src/
COPY tests/ tests/
COPY --from=ui-build /app/src/AccessControl.Web/wwwroot src/AccessControl.Web/wwwroot
RUN dotnet publish src/AccessControl.Web/AccessControl.Web.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /out ./
VOLUME ["/app/data"]
ENV ASPNETCORE_URLS=http://+:8150
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/accesscontrol.db"
EXPOSE 8150
ENTRYPOINT ["dotnet", "AccessControl.Web.dll"]
