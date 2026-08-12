FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Desafio.sln .
COPY src/Desafio.Api/Desafio.Api.csproj src/Desafio.Api/
RUN dotnet restore src/Desafio.Api/Desafio.Api.csproj

COPY src/ src/
RUN dotnet publish src/Desafio.Api/Desafio.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "Desafio.Api.dll"]
