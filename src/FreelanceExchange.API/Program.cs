using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FreelanceExchange.API.Data;
using FreelanceExchange.API.Hubs;
using FreelanceExchange.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ==== РУЧНАЯ УСТАНОВКА СТРОКИ ПОДКЛЮЧЕНИЯ ====
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString) || connectionString.Contains("localhost"))
{
    // 🔴 ВАЖНО: замените эту строку на вашу Internal Connection String из Render!
    connectionString = "postgresql://freelanceexchange_user:Pc9vzIb0TbRjM9VsRGvmTqX9kjZw2l6u@dpg-d8is5jk8aovs738lgl60-a/freelanceexchange";
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
        dbContext.EnsureAchievementsCreated(); // метод из AppDbContext
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ошибка при инициализации базы данных");
    }
}
// ================================================

app.UseStaticFiles();
app.UseHttpsRedirection(); // Если на Render не работает, закомментируй эту строку
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<ChatHub>("/chatHub");
app.MapControllers();
app.Run();
