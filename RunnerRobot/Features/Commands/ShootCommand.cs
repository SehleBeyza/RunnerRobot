using MediatR;
using RunnerRobot.Data;

namespace RunnerRobot.Features.Commands
{
    public record ShootCommand(int RobotId) : IRequest<bool>;

    public class ShootCommandHandler : IRequestHandler<ShootCommand, bool>
    {
        private readonly IUnitOfWork _unitOfWork;

        public ShootCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(ShootCommand request, CancellationToken cancellationToken)
        {
            var robot = await _unitOfWork.Robots.GetByIdAsync(request.RobotId);
            if (robot == null || robot.Ammo <= 0 || !robot.IsAlive) return false;

            robot.Ammo--;
            await _unitOfWork.CompleteAsync();
            return true;
        }
    }
}