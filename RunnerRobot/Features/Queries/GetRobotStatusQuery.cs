using AutoMapper; 
using MediatR;
using RunnerRobot.Data;
using RunnerRobot.Models;

namespace RunnerRobot.Features.Queries
{
    public record GetRobotStatusQuery(int RobotId) : IRequest<RobotStatusDto>;

    public class GetRobotStatusQueryHandler : IRequestHandler<GetRobotStatusQuery, RobotStatusDto>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetRobotStatusQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<RobotStatusDto> Handle(GetRobotStatusQuery request, CancellationToken cancellationToken)
        {
            var robot = await _unitOfWork.Robots.GetByIdAsync(request.RobotId);
            return _mapper.Map<RobotStatusDto>(robot);
        }
    }
}