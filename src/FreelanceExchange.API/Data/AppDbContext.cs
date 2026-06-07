using Microsoft.EntityFrameworkCore;
using FreelanceExchange.API.Models;

namespace FreelanceExchange.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Response> Responses => Set<Response>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
    public DbSet<Achievement> Achievements { get; set; }
    public DbSet<UserAchievement> UserAchievements { get; set; }
    public DbSet<Wallet> Wallets => Set<Wallet>();

    // Новая сущность для переписки с администратором
    public DbSet<AdminMessage> AdminMessages => Set<AdminMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Начальные роли
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Guest" },
            new Role { Id = 2, Name = "Customer" },
            new Role { Id = 3, Name = "Freelancer" },
            new Role { Id = 4, Name = "Moderator" },
            new Role { Id = 5, Name = "Admin" }
        );

        // Уникальный email у User
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Account>()
            .HasKey(a => a.UserId);


        // Настройки для ChatMessage (без явных внешних ключей, EF определит сам)
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Message).IsRequired();
            entity.Property(m => m.SentAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // ========== НАСТРОЙКИ ДЛЯ ADMINMESSAGE ==========
        modelBuilder.Entity<AdminMessage>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Message).IsRequired();
            entity.Property(m => m.SentAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(m => m.User)
.WithMany()
.HasForeignKey(m => m.UserId)
.OnDelete(DeleteBehavior.Cascade);
            
            // Индекс для быстрого поиска сообщений по пользователю
            entity.HasIndex(m => m.UserId);
        });
    }

    // Метод для заполнения таблицы достижений начальными данными
    public void EnsureAchievementsCreated()
    {
        if (!Achievements.Any())
        {
            Achievements.AddRange(
                new Achievement { Name = "Email подтверждён", Description = "Подтвердите свой email адрес", TriggerType = "EmailVerified", Icon = "mail-check" },
                new Achievement { Name = "Телефон подтверждён", Description = "Привяжите и подтвердите номер телефона", TriggerType = "PhoneVerified", Icon = "smartphone" },
                new Achievement { Name = "Паспорт подтверждён", Description = "Пройдите расширенную верификацию", TriggerType = "PassportVerified", Icon = "id-card" },
                new Achievement { Name = "Первый заказ", Description = "Успешно завершите свой первый заказ", TriggerType = "FirstOrder", RequiredCount = 1, Icon = "trophy" },
                new Achievement { Name = "Пятый заказ", Description = "Завершите 5 заказов", TriggerType = "OrdersCompleted", RequiredCount = 5, Icon = "star" },
                new Achievement { Name = "Первая пятёрка", Description = "Получите отзыв с рейтингом 5", TriggerType = "FirstFiveStars", RequiredCount = 1, Icon = "heart" },
                new Achievement { Name = "Долгожитель", Description = "Активны на платформе более 30 дней", TriggerType = "ActiveDays", RequiredCount = 30, Icon = "calendar" }
            );
            SaveChanges();
        }
    }
}