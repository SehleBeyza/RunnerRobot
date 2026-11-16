namespace RobotGame.Models
{
    public class RobotEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "Runner Mino";
        public int Health { get; set; } = 100;
        public int Ammo { get; set; } = 10;
        public int PositionY { get; set; } = 0;
        public bool IsAlive => Health > 0;
    }



}