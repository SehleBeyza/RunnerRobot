using System;

namespace RunnerRobot.Models
{
    public class GameSessionEntity
    {
        public int Id { get; set; }
        public string PlayerName { get; set; } = "Runner Mino";
        public int Score { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
