FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/FreelanceExchange.API/*.csproj ./FreelanceExchange.API/
RUN dotnet restore ./FreelanceExchange.API/FreelanceExchange.API.csproj

COPY src/ ./src/

RUN dotnet publish ./src/FreelanceExchange.API/FreelanceExchange.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080/
ENTRYPOINT ["dotnet", "FreelanceExchange.API.dll"]
