using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RunnerRobot.Data;
using RunnerRobot.Models;

namespace RunnerRobot.Features.Queries
{
    // SQLite'tan En Yüksek Skorları Çeken CQRS Query
    public record GetLeaderboardQuery(int TopCount = 5) : IRequest<List<GameSessionEntity>>;

    public class GetLeaderboardQueryHandler : IRequestHandler<GetLeaderboardQuery, List<GameSessionEntity>>
    {
        private readonly AppDbContext _context;

        public GetLeaderboardQueryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<GameSessionEntity>> Handle(GetLeaderboardQuery request, CancellationToken cancellationToken)
        {
            return await _context.GameSessions
                .OrderByDescending(s => s.Score)
                .Take(request.TopCount)
                .ToListAsync(cancellationToken);
        }
    }
}
