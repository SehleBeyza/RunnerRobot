using AutoMapper;
using RobotGame.Models;
using RunnerRobot.Models;

namespace RunnerRobot.Mapping
{
    public class RobotMappingProfile : Profile
    {
        public RobotMappingProfile()
        {
            CreateMap<RobotEntity, RobotStatusDto>();
        }
    }
}
