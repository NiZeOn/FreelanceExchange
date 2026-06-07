using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FreelanceExchange.API.Data;
using FreelanceExchange.API.Hubs;
using FreelanceExchange.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Настройка порта из переменной окружения PORT (для Render)
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ==== РУЧНАЯ УСТАНОВКА СТРОКИ ПОДКЛЮЧЕНИЯ ====
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString) || connectionString.Contains("localhost"))
{
    // ⚠️ ВАЖНО: замените эту строку на вашу Internal Connection String из Render!
    // Формат: postgresql://username:password@host:port/database
    connectionString = "postgresql://freelanceexchange_user:Pc9vzIb0TbRjM9VsRGvmTqX9kjZw2l6u@dpg-d8is5jk8aovs738lgl60-a:5432/freelanceexchange";
    Console.WriteLine("Using hardcoded connection string for Render PostgreSQL");
}
else
{
    Console.WriteLine("Using connection string from configuration");
}
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
// =============================================

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

// Настройка Swagger только в разработке
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ==== Инициализация базы данных и достижений ====
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        dbContext.Database.Migrate();
        dbContext.EnsureAchievementsCreated();
        Console.WriteLine("Database migration and achievements initialization completed.");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ошибка при инициализации базы данных");
        Console.WriteLine($"DB ERROR: {ex.Message}");
    }
}
// ================================================

// Для статических файлов (если есть wwwroot)
app.UseStaticFiles();

// На Render не нужно принудительное перенаправление на HTTPS (делается на уровне прокси)
// app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// SignalR hub
app.MapHub<ChatHub>("/chatHub");

// API контроллеры
app.MapControllers();

// Health check для Render (чтобы не ждал)
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// Перенаправление с корня на Swagger (чтобы не было 404)
// Если у вас есть статический фронтенд (index.html), лучше использовать:
// app.UseDefaultFiles();
// app.UseStaticFiles();
// app.MapFallbackToFile("index.html");
app.MapGet("/", () => Results.Redirect("/swagger/index.html"));

app.Run();
