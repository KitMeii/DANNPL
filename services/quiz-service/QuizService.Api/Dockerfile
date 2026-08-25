FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY shared/Shared.Contracts/Shared.Contracts.csproj shared/Shared.Contracts/
COPY shared/Shared.Infrastructure/Shared.Infrastructure.csproj shared/Shared.Infrastructure/
COPY services/quiz-service/QuizService.Api/QuizService.Api.csproj services/quiz-service/QuizService.Api/
RUN dotnet restore services/quiz-service/QuizService.Api/QuizService.Api.csproj

COPY shared/ shared/
COPY services/quiz-service/QuizService.Api/ services/quiz-service/QuizService.Api/
RUN dotnet publish services/quiz-service/QuizService.Api/QuizService.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "QuizService.Api.dll"]
