using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FreelanceExchange.API.Data;
using FreelanceExchange.API.Hubs;
using FreelanceExchange.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ==== НАСТРОЙКА ПОРТА ДЛЯ RENDER ====
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0:{port}");
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ==== СТРОКА ПОДКЛЮЧЕНИЯ К POSTGRESQL ====
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString) || connectionString.Contains("localhost"))
{
    // Ваша актуальная строка подключения к Neon Postgres
    connectionString = "Host=dpg-d8is5jk8aovs738lgl60-a;Port=5432;Database=freelanceexchange;Username=freelanceexchange_user;Password=Pc9vzIb0TbRjM9VsRGvmTqX9kjZw2l6u;SslMode=Require;Trust Server Certificate=true;";
}
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<AchievementService>();

var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

builder.Services.AddAuthorization();
builder.Services.AddSignalR();

var app = builder.Build();

// ==== SWAGGER (ДОСТУПЕН ПО /swagger) ====
app.UseSwagger();
app.UseSwaggerUI();

// ==== ИНИЦИАЛИЗАЦИЯ БАЗЫ ДАННЫХ ====
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Автоматический мигратор EF Core пропущен. 
        // База данных на Neon уже содержит все необходимые таблицы, ручные миграции отключены.
        logger.LogInformation("Автоматический мигратор EF Core пропущен. Проверяем наполнение достижений...");
        dbContext.EnsureAchievementsCreated();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Ошибка при инициализации данных или проверке ачивок");
    }
}

// ==== ОТДАЧА СТАТИЧЕСКИХ ФАЙЛОВ ====
app.UseDefaultFiles(); // Ищет index.html в wwwroot
app.UseStaticFiles();  // Отдаёт статические файлы (css, js, изображения)

// ==== БЕЗОПАСНОСТЬ И АВТОРИЗАЦИЯ (СТРОГИЙ ПОРЯДОК) ====
app.UseAuthentication(); // 1. Проверяем JWT-токен пользователя
app.UseAuthorization();  // 2. Проверяем права доступа (Фрилансер/Заказчик)

// ==== МАРШРУТИЗАЦИЯ АППЛИКАЦИИ ====
app.MapHub<ChatHub>("/chatHub");
app.MapControllers();

// ==== ДЕФОЛТНЫЙ ФАЛБЭК ДЛЯ КЛИЕНТСКОГО РОУТИНГА (SPA) ====
// Должен вызываться строго после авторизации и маппинга API контроллеров!
app.MapFallbackToFile("index.html");

// ==== HEALTH CHECK (для проверки статуса контейнера Render) ====
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
