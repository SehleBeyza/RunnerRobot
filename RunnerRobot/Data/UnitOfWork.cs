using Microsoft.EntityFrameworkCore;
using RobotGame.Models;
using RunnerRobot.Data;

namespace RunnerRobot.Data
{
    public interface IRepository<T> where T : class
    {
        Task<T> GetByIdAsync(int id);
        Task AddAsync(T entity);
    }

    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly AppDbContext _context;
        public Repository(AppDbContext context) => _context = context;

        public async Task<T> GetByIdAsync(int id) => await _context.Set<T>().FindAsync(id);
        public async Task AddAsync(T entity) => await _context.Set<T>().AddAsync(entity);
    }

    public interface IUnitOfWork : IDisposable
    {
        IRepository<RobotEntity> Robots { get; }
        Task<int> CompleteAsync();
    }

    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        public IRepository<RobotEntity> Robots { get; }

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
            Robots = new Repository<RobotEntity>(_context);
        }

        public async Task<int> CompleteAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}