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
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ==== СТРОКА ПОДКЛЮЧЕНИЯ К POSTGRESQL ====
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString) || connectionString.Contains("localhost"))
{
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
        // ИСПРАВЛЕНО: Отключен вызов автоматических миграций. 
        // База данных на Neon уже содержит все необходимые таблицы (Categories, SubscriptionPlans и т.д.),
        // поэтому повторная попытка их создать приводила к падению контейнера.
        // dbContext.Database.Migrate();
        
        logger.LogInformation("Автоматический мигратор EF Core пропущен. Проверяем наполнение достижений...");
        dbContext.EnsureAchievementsCreated();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Ошибка при инициализации данных или проверке ачивок");
    }
}

// ==== ОТДАЧА ФРОНТЕНДА ====
app.UseDefaultFiles(); // ищет index.html в wwwroot
app.UseStaticFiles();  // отдаёт статические файлы (css, js, изображения)

// Для SPA (клиентский роутинг) – все не-API запросы отдаём index.html
app.MapFallbackToFile("index.html");

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<ChatHub>("/chatHub");
app.MapControllers();

// ==== HEALTH CHECK (для Render) ====
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
