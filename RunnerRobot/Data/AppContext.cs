using Microsoft.EntityFrameworkCore;
using RobotGame.Models;
using RunnerRobot.Models;

namespace RunnerRobot.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<RobotEntity> Robots { get; set; }
        public DbSet<GameSessionEntity> GameSessions { get; set; }

        public AppDbContext()
        {
        }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Kalıcı SQLite Veritabanı (game.db)
                optionsBuilder.UseSqlite("Data Source=game.db");
            }
        }
    }
}
