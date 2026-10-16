FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/CareTogether.Ai/CareTogether.Ai.csproj && dotnet publish src/CareTogether.Ai/CareTogether.Ai.csproj -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet","CareTogether.Ai.dll"]
