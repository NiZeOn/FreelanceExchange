using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FreelanceExchange.API.Data;
using FreelanceExchange.API.Hubs;
using FreelanceExchange.API.Services;
using BCrypt.Net; // важно для хеширования

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

// ==== ИНИЦИАЛИЗАЦИЯ БАЗЫ ДАННЫХ И СОЗДАНИЕ РОЛЕЙ/ПОЛЬЗОВАТЕЛЕЙ (СИНХРОННО) ====
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        dbContext.Database.Migrate();
        dbContext.EnsureAchievementsCreated();

        // 1. Создаём роли, если их нет
        if (!dbContext.Roles.Any())
        {
            dbContext.Roles.AddRange(
                new Role { Name = "Admin" },
                new Role { Name = "Moderator" },
                new Role { Name = "Customer" },
                new Role { Name = "Freelancer" }
            );
            dbContext.SaveChanges(); // синхронно
        }

        // 2. Получаем ID ролей
        var adminRole = dbContext.Roles.First(r => r.Name == "Admin");
        var moderatorRole = dbContext.Roles.First(r => r.Name == "Moderator");
        var customerRole = dbContext.Roles.First(r => r.Name == "Customer");
        var freelancerRole = dbContext.Roles.First(r => r.Name == "Freelancer");

        // 3. Администратор
        if (!dbContext.Users.Any(u => u.Email == "admin@freelance.com"))
        {
            dbContext.Users.Add(new User
            {
                Email = "admin@freelance.com",
                PasswordHash = BCrypt.HashPassword("Admin123!"),
                FullName = "Главный администратор",
                RoleId = adminRole.Id,
                RegistrationDate = DateTime.UtcNow,
                IsBlocked = false,
                IsEmailVerified = true
            });
        }
        // 4. Модератор
        if (!dbContext.Users.Any(u => u.Email == "moderator@freelance.com"))
        {
            dbContext.Users.Add(new User
            {
                Email = "moderator@freelance.com",
                PasswordHash = BCrypt.HashPassword("Moderator123!"),
                FullName = "Системный модератор",
                RoleId = moderatorRole.Id,
                RegistrationDate = DateTime.UtcNow,
                IsBlocked = false,
                IsEmailVerified = true
            });
        }

        // 5. Тестовый заказчик
        if (!dbContext.Users.Any(u => u.Email == "customer@test.com"))
        {
            dbContext.Users.Add(new User
            {
                Email = "customer@test.com",
                PasswordHash = BCrypt.HashPassword("Customer123!"),
                FullName = "Тестовый заказчик",
                RoleId = customerRole.Id,
                RegistrationDate = DateTime.UtcNow,
                IsBlocked = false,
                IsEmailVerified = true
            });
        }

        // 6. Тестовый фрилансер
        if (!dbContext.Users.Any(u => u.Email == "freelancer@test.com"))
        {
            dbContext.Users.Add(new User
            {
                Email = "freelancer@test.com",
                PasswordHash = BCrypt.HashPassword("Freelancer123!"),
                FullName = "Тестовый фрилансер",
                RoleId = freelancerRole.Id,
                RegistrationDate = DateTime.UtcNow,
                IsBlocked = false,
                IsEmailVerified = true
            });
        }

        dbContext.SaveChanges(); // синхронное сохранение всех изменений
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ошибка при инициализации базы данных");
    }
}
// =====================================================

// ==== ОТДАЧА ФРОНТЕНДА ====
app.UseDefaultFiles(); // ищет index.html в wwwroot
app.UseStaticFiles();  // отдаёт статические файлы

// SPA fallback
app.MapFallbackToFile("index.html");

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<ChatHub>("/chatHub");
app.MapControllers();

// ==== HEALTH CHECK ====
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
