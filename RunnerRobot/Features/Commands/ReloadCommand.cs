using MediatR;
using RunnerRobot.Data;

namespace RunnerRobot.Features.Commands
{
    public record ReloadCommand(int RobotId, int MaxAmmo = 8) : IRequest<bool>;

    public class ReloadCommandHandler : IRequestHandler<ReloadCommand, bool>
    {
        private readonly IUnitOfWork _unitOfWork;

        public ReloadCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(ReloadCommand request, CancellationToken cancellationToken)
        {
            var robot = await _unitOfWork.Robots.GetByIdAsync(request.RobotId);
            if (robot == null || !robot.IsAlive) return false;

            robot.Ammo = request.MaxAmmo;
            await _unitOfWork.CompleteAsync();
            return true;
        }
    }
}
