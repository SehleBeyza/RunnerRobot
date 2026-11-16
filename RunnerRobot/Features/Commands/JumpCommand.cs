using MediatR;
using RunnerRobot.Data;

namespace RunnerRobot.Features.Commands
{
    public record JumpCommand(int RobotId) : IRequest<bool>;

    public class JumpCommandHandler : IRequestHandler<JumpCommand, bool>
    {
        private readonly IUnitOfWork _unitOfWork;

        public JumpCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(JumpCommand request, CancellationToken cancellationToken)
        {
            var robot = await _unitOfWork.Robots.GetByIdAsync(request.RobotId);
            if (robot == null || !robot.IsAlive) return false;

            robot.PositionY = 1;
            await _unitOfWork.CompleteAsync();
            return true;
        }
    }
}