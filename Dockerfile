---- Stage 1: Build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

Копируем .csproj и восстанавливаем зависимости
COPY src/FreelanceExchange.API/*.csproj ./FreelanceExchange.API/
RUN dotnet restore ./FreelanceExchange.API/FreelanceExchange.API.csproj

Копируем весь исходный код
COPY src/ ./src/

Публикуем приложение
RUN dotnet publish ./src/FreelanceExchange.API/FreelanceExchange.API.csproj -c Release -o /app/publish

---- Stage 2: Run ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 8080

Копируем собранное приложение
COPY --from=build /app/publish .

Задаём порт (обязательно для Render)
ENV ASPNETCORE_URLS=http://+:8080/

Точка входа
ENTRYPOINT ["dotnet", "FreelanceExchange.API.dll"]
