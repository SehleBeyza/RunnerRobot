using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RobotGame.Models;
using RunnerRobot.Data;
using RunnerRobot.Features.Commands;
using RunnerRobot.Features.Queries;
using RunnerRobot.Models;

// Dependency Injection ve Servis Kayıtları
var services = new ServiceCollection();

// Veritabanı ve Mimari Servisleri
services.AddDbContext<AppDbContext>();
services.AddScoped<IUnitOfWork, UnitOfWork>();


// AutoMapper ve MediatR Kayıtları

services.AddAutoMapper(typeof(RunnerRobot.Mappings.RobotMappingProfile));
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(RunnerRobot.Features.Commands.JumpCommand).Assembly));

var serviceProvider = services.BuildServiceProvider();
var mediator = serviceProvider.GetRequiredService<IMediator>();


using (var scope = serviceProvider.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Robots.Add(new RobotEntity { Id = 1, Health = 100, Ammo = 5, PositionY = 0 });
    context.SaveChanges();
}

Console.WriteLine("🤖 ROBOT RUNNER & SHOOTER OYUNUNA HOŞ GELDİN KUŞUM! 🤖");
Console.WriteLine("Kontroller:");
Console.WriteLine(" [W] -> Zıpla (Jump)");
Console.WriteLine(" [SPACE] -> Ateş Et (Shoot)");
Console.WriteLine(" [Q] -> Oyundan Çık\n");

bool isRunning = true;
int robotId = 1;

while (isRunning)
{
    // 1. Anlık Robot Durumunu Sorgula 
    var status = await mediator.Send(new GetRobotStatusQuery(robotId));
    Console.Clear();
    Console.WriteLine($"=== DURUM ===");
    Console.WriteLine($"Can: {status.Health} | Mermi: {status.Ammo} | Konum: {(status.PositionY == 1 ? "🦘 Havada (Zıplıyor)" : "🏃 Yerde")}");
    Console.WriteLine("=============");
    Console.WriteLine("Tuş bekleniyor (W: Zıpla, Space: Ateş, Q: Çıkış)...");

    
    if (Console.KeyAvailable)
    {
        var key = Console.ReadKey(intercept: true).Key;

        if (key == ConsoleKey.W)
        {
            await mediator.Send(new JumpCommand(robotId));
            Console.WriteLine("Robot zıpladı!");
        }
        else if (key == ConsoleKey.Spacebar)
        {
            var success = await mediator.Send(new ShootCommand(robotId));
            if (!success) Console.WriteLine("Mermi bitti kuşum!");
        }
        else if (key == ConsoleKey.Q)
        {
            isRunning = false;
        }
    }

    // Yerçekimi simülasyonu
    await Task.Delay(500);
    using (var scope = serviceProvider.CreateScope())
    {
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var r = await uow.Robots.GetByIdAsync(robotId);
        if (r != null && r.PositionY == 1)
        {
            r.PositionY = 0;
            await uow.CompleteAsync();
        }
    }
}

Console.WriteLine("Oyun bitti kuşum, ellerine sağlık!");