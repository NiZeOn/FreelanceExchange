# ---- Stage 1: Build the application ----
FROM ://microsoft.com AS build
WORKDIR /src

# Копируем файл проекта с учетом имени .API
COPY ["src/FreelanceExchange.API/FreelanceExchange.API.csproj", "src/FreelanceExchange.API/"]
RUN dotnet restore "src/FreelanceExchange.API/FreelanceExchange.API.csproj"

# Копируем остальной код
COPY . .
WORKDIR "/src/src/FreelanceExchange.API"
RUN dotnet publish "FreelanceExchange.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ---- Stage 2: Run the application ----
FROM ://microsoft.com
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "FreelanceExchange.API.dll"]
