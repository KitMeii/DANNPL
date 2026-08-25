FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY shared/Shared.Contracts/Shared.Contracts.csproj shared/Shared.Contracts/
COPY shared/Shared.Infrastructure/Shared.Infrastructure.csproj shared/Shared.Infrastructure/
COPY services/ai-service/AiService.Api/AiService.Api.csproj services/ai-service/AiService.Api/
RUN dotnet restore services/ai-service/AiService.Api/AiService.Api.csproj

COPY shared/ shared/
COPY services/ai-service/AiService.Api/ services/ai-service/AiService.Api/
RUN dotnet publish services/ai-service/AiService.Api/AiService.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AiService.Api.dll"]
