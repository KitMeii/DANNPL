FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY shared/Shared.Contracts/Shared.Contracts.csproj shared/Shared.Contracts/
COPY shared/Shared.Infrastructure/Shared.Infrastructure.csproj shared/Shared.Infrastructure/
COPY services/gateway/Gateway/Gateway.csproj services/gateway/Gateway/
RUN dotnet restore services/gateway/Gateway/Gateway.csproj

COPY shared/ shared/
COPY services/gateway/Gateway/ services/gateway/Gateway/
RUN dotnet publish services/gateway/Gateway/Gateway.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Gateway.dll"]
