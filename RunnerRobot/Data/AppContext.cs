using Microsoft.EntityFrameworkCore;
using RobotGame.Models;
using RunnerRobot.Models;

namespace RunnerRobot.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<RobotEntity> Robots { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase("RobotGameDb");
        }
    }
}