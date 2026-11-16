using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using RunnerRobot.Data;
using RunnerRobot.Models;

namespace RunnerRobot.Events
{
    // Robot Ateş Ettiğinde Tetiklenen Domain Event
    public record RobotFiredEvent(int RobotId, int RemainingAmmo) : INotification;

    // Robot Öldüğünde Tetiklenen Domain Event
    public record RobotDiedEvent(int RobotId, int FinalScore, string PlayerName = "Runner Mino") : INotification;

    // Ateş Etme Olayı Dinleyicisi (Event Handler)
    public class RobotFiredEventHandler : INotificationHandler<RobotFiredEvent>
    {
        public Task Handle(RobotFiredEvent notification, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[EVENT LOG] 🔫 Robot #{notification.RobotId} ateş etti! Kalan Mermi: {notification.RemainingAmmo}");
            return Task.CompletedTask;
        }
    }

    // Ölüm Olayı Dinleyicisi (Event Handler: Skor tablosunu günceller ve SQLite'a kaydeder)
    public class RobotDiedEventHandler : INotificationHandler<RobotDiedEvent>
    {
        private readonly AppDbContext _context;

        public RobotDiedEventHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task Handle(RobotDiedEvent notification, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[EVENT LOG] 💥 Robot #{notification.RobotId} öldü! Oyun Sonu Skoru: {notification.FinalScore}");

            try
            {
                var session = new GameSessionEntity
                {
                    PlayerName = notification.PlayerName,
                    Score = notification.FinalScore,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.GameSessions.AddAsync(session, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                Console.WriteLine($"[SQLITE DB] 💾 Skor ({notification.FinalScore}) 'game.db' veritabanına başarıyla kaydedildi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SQLITE DB HATA] Skor veritabanına kaydedilemedi: {ex.Message}");
            }
        }
    }
}
