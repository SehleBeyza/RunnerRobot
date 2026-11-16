using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Raylib_cs;
using RobotGame.Models;
using RunnerRobot.Data;
using RunnerRobot.Events;
using RunnerRobot.Features.Commands;
using RunnerRobot.Features.Queries;
using RunnerRobot.Mapping;
using RunnerRobot.Models;

namespace RunnerRobot
{
    // Oyun Durumu
    public enum GameState
    {
        NameInput, // Oyun Başlangıcında İsim Girişi Ekranı
        Playing,   // Oyun Sırasında
        GameOver   // Yanınca Skor Tablosu Ekranı
    }

    // Mermi Modeli
    public struct LaserBullet
    {
        public float X;
        public float Y;
        public float Speed;
        public bool Active;
    }

    // Zemin Engeli Modeli
    public struct GroundObstacle
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public int Type;
        public bool Active;
    }

    // Uçan Düşman Drone Modeli
    public struct FlyingEnemy
    {
        public float X;
        public float Y;
        public float BaseY;
        public float Width;
        public float Height;
        public float SinOffset;
        public bool Active;
    }

    // Ekstra Can Modeli
    public struct HealthPickup
    {
        public float X;
        public float Y;
        public float BaseY;
        public float Width;
        public float Height;
        public float SinOffset;
        public bool Active;
    }

    // Kalkan Küresi Modeli
    public struct ShieldPickup
    {
        public float X;
        public float Y;
        public float BaseY;
        public float Width;
        public float Height;
        public float SinOffset;
        public bool Active;
    }

    // Patlama & Kıvılcım Parçacığı
    public struct SparkParticle
    {
        public float X;
        public float Y;
        public float Vx;
        public float Vy;
        public float Life;
        public float MaxLife;
        public Color Color;
        public bool Active;
    }

    public static class Program
    {
        public static async Task Main()
        {
            const int screenWidth = 960;
            const int screenHeight = 540;
            const float groundY = 430f;

            // ====================== DEPENDENCY INJECTION & CQRS ======================
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AppDbContext>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddAutoMapper(cfg => cfg.AddProfile<RobotMappingProfile>());
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

            var serviceProvider = services.BuildServiceProvider();
            var mediator = serviceProvider.GetRequiredService<IMediator>();

            // ====================== SQLITE VERİTABANI BAŞLATMA ======================
            int highScore = 0;
            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();

                if (!db.Robots.Any())
                {
                    db.Robots.Add(new RobotEntity { Id = 1, Health = 100, Ammo = 8, PositionY = 0 });
                    db.SaveChanges();
                }

                if (db.GameSessions.Any())
                {
                    highScore = db.GameSessions.Max(s => s.Score);
                }
            }

            // ====================== OYUN PENCERESİ BAŞLATMA ======================
            Raylib.InitWindow(screenWidth, screenHeight, "Runner Robot - 2D Pixel Edition (SQLite Leaderboard)");
            Raylib.SetTargetFPS(60);

            var random = new Random();

            // Oyun Durumu ve Oyuncu Bilgisi
            var currentGameState = GameState.NameInput;
            string playerName = "Mino";
            var leaderboard = new List<GameSessionEntity>();

            // Oyuncu Robot Değişkenleri
            const int robotId = 1;
            float robotX = 140f;
            float robotY = groundY - 48f;
            float robotVy = 0f;
            const float robotWidth = 36f;
            const float robotHeight = 48f;
            const float gravity = 1500f;
            const float jumpVelocity = -650f;
            bool isGrounded = true;
            int jumpCount = 0;
            const int maxJumps = 2; // Çift Zıplama

            int health = 3;
            const int maxHealth = 3;
            int ammo = 8;
            const int maxAmmo = 8;
            float ammoRechargeTimer = 0f;

            float shieldTimer = 0f;
            float invulnerableTimer = 0f;
            float screenShakeTimer = 0f;
            float runAnimTimer = 0f;

            int score = 0;
            float scoreDistanceCounter = 0f;

            float worldSpeed = 380f;
            float groundOffset = 0f;

            // Listeler
            var bullets = new List<LaserBullet>();
            var obstacles = new List<GroundObstacle>();
            var enemies = new List<FlyingEnemy>();
            var healthPickups = new List<HealthPickup>();
            var shieldPickups = new List<ShieldPickup>();
            var particles = new List<SparkParticle>();

            float obstacleSpawnTimer = 1.2f;
            float enemySpawnTimer = 2.8f;
            float healthSpawnTimer = 7.0f;
            float shieldSpawnTimer = 13.0f;

            // Yardımcı: Parçacık fırlatma
            void SpawnExplosion(float x, float y, int count, Color mainColor)
            {
                for (int i = 0; i < count; i++)
                {
                    float angle = (float)(random.NextDouble() * Math.PI * 2);
                    float spd = (float)(random.Next(60, 260));
                    particles.Add(new SparkParticle
                    {
                        X = x,
                        Y = y,
                        Vx = (float)Math.Cos(angle) * spd,
                        Vy = (float)Math.Sin(angle) * spd,
                        Life = 0f,
                        MaxLife = (float)(0.25 + random.NextDouble() * 0.35),
                        Color = mainColor,
                        Active = true
                    });
                }
            }

            // Yardımcı: Oyunu Sıfırla
            void ResetGame()
            {
                robotX = 140f;
                robotY = groundY - robotHeight;
                robotVy = 0f;
                isGrounded = true;
                jumpCount = 0;
                health = maxHealth;
                ammo = maxAmmo;
                shieldTimer = 0f;
                score = 0;
                worldSpeed = 380f;
                scoreDistanceCounter = 0f;
                ammoRechargeTimer = 0f;
                invulnerableTimer = 0f;
                screenShakeTimer = 0f;

                bullets.Clear();
                obstacles.Clear();
                enemies.Clear();
                healthPickups.Clear();
                shieldPickups.Clear();
                particles.Clear();

                obstacleSpawnTimer = 1.2f;
                enemySpawnTimer = 2.5f;
                healthSpawnTimer = 7.0f;
                shieldSpawnTimer = 13.0f;
            }

            // ====================== OYUN DÖNGÜSÜ (GAME LOOP) ======================
            while (!Raylib.WindowShouldClose())
            {
                float dt = Raylib.GetFrameTime();
                if (dt > 0.1f) dt = 0.1f;

                runAnimTimer += dt;

                // ------------------ 1. DURUM: İSİM GİRİŞİ (NAME INPUT) ------------------
                if (currentGameState == GameState.NameInput)
                {
                    // Klavye Karakterlerini Yakala
                    int key = Raylib.GetCharPressed();
                    while (key > 0)
                    {
                        if ((key >= 32) && (key <= 126) && (playerName.Length < 12))
                        {
                            playerName += (char)key;
                        }
                        key = Raylib.GetCharPressed();
                    }

                    // Silme (Backspace)
                    if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && playerName.Length > 0)
                    {
                        playerName = playerName.Substring(0, playerName.Length - 1);
                    }

                    // Enter Tuşu İle Oyuna Başla
                    if (Raylib.IsKeyPressed(KeyboardKey.Enter) && !string.IsNullOrWhiteSpace(playerName))
                    {
                        ResetGame();
                        currentGameState = GameState.Playing;
                    }
                }
                // ------------------ 2. DURUM: OYUN SIRASINDA (PLAYING) ------------------
                else if (currentGameState == GameState.Playing)
                {
                    groundOffset += worldSpeed * dt;

                    // Mesafe Skoru
                    scoreDistanceCounter += worldSpeed * dt;
                    if (scoreDistanceCounter >= 50f)
                    {
                        score += 5;
                        scoreDistanceCounter = 0f;
                        if (score > highScore) highScore = score;
                    }

                    // Kademeli Hız
                    worldSpeed = 380f + (score * 0.08f);
                    if (worldSpeed > 680f) worldSpeed = 680f;

                    // Kalkan Süresi
                    if (shieldTimer > 0f)
                    {
                        shieldTimer -= dt;
                        if (shieldTimer < 0f) shieldTimer = 0f;
                    }

                    // Mermi Yenilenmesi
                    if (ammo < maxAmmo)
                    {
                        ammoRechargeTimer += dt;
                        if (ammoRechargeTimer >= 1.2f)
                        {
                            ammo++;
                            ammoRechargeTimer = 0f;
                        }
                    }

                    if (invulnerableTimer > 0f) invulnerableTimer -= dt;
                    if (screenShakeTimer > 0f) screenShakeTimer -= dt;

                    // --- KONTROLLER ---
                    // 1. Zıplama & Çift Zıplama [SPACE] / [W]
                    if (Raylib.IsKeyPressed(KeyboardKey.Space) || Raylib.IsKeyPressed(KeyboardKey.W) || Raylib.IsKeyPressed(KeyboardKey.Up))
                    {
                        if (isGrounded)
                        {
                            robotVy = jumpVelocity;
                            isGrounded = false;
                            jumpCount = 1;
                            SpawnExplosion(robotX + 16, groundY, 4, new Color(180, 220, 255, 200));
                        }
                        else if (jumpCount < maxJumps)
                        {
                            robotVy = jumpVelocity * 0.92f;
                            jumpCount++;
                            SpawnExplosion(robotX + 18, robotY + robotHeight, 8, new Color(0, 255, 255, 255));
                            SpawnExplosion(robotX + 18, robotY + robotHeight, 6, new Color(255, 180, 50, 255));
                        }
                    }

                    // 2. Ateş Etme [F] / [ENTER] / [SOL TIK]
                    if (Raylib.IsKeyPressed(KeyboardKey.F) || Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.J) || Raylib.IsMouseButtonPressed(MouseButton.Left))
                    {
                        if (ammo > 0)
                        {
                            ammo--;
                            bullets.Add(new LaserBullet
                            {
                                X = robotX + robotWidth + 4,
                                Y = robotY + 18f,
                                Speed = 900f,
                                Active = true
                            });

                            SpawnExplosion(robotX + robotWidth + 4, robotY + 20f, 3, new Color(255, 240, 100, 255));
                            _ = mediator.Publish(new RobotFiredEvent(robotId, ammo));
                        }
                    }

                    // 3. Mermi Tazeleme [R]
                    if (Raylib.IsKeyPressed(KeyboardKey.R))
                    {
                        if (ammo < maxAmmo)
                        {
                            ammo = maxAmmo;
                            SpawnExplosion(robotX + robotWidth, robotY + 20f, 10, new Color(0, 240, 255, 255));
                            _ = mediator.Send(new ReloadCommand(robotId, maxAmmo));
                        }
                    }

                    // 4. Manevra [A] / [D]
                    if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left))
                    {
                        robotX -= 240f * dt;
                        if (robotX < 40f) robotX = 40f;
                    }
                    if (Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right))
                    {
                        robotX += 240f * dt;
                        if (robotX > 380f) robotX = 380f;
                    }

                    // --- ROBOT FİZİĞİ ---
                    if (!isGrounded)
                    {
                        robotVy += gravity * dt;
                        robotY += robotVy * dt;

                        if (robotY >= groundY - robotHeight)
                        {
                            robotY = groundY - robotHeight;
                            robotVy = 0f;
                            isGrounded = true;
                            jumpCount = 0;
                            SpawnExplosion(robotX + 18, groundY, 5, new Color(160, 200, 255, 220));
                        }
                    }

                    // --- MERMİLERİ GÜNCELLE ---
                    for (int i = 0; i < bullets.Count; i++)
                    {
                        var b = bullets[i];
                        b.X += b.Speed * dt;
                        if (b.X > screenWidth + 50) b.Active = false;
                        bullets[i] = b;
                    }

                    // --- ENGELLER ---
                    obstacleSpawnTimer -= dt;
                    if (obstacleSpawnTimer <= 0f)
                    {
                        int type = random.Next(0, 3);
                        float w = (type == 1) ? 46f : (type == 2 ? 30f : 36f);
                        float h = (type == 1) ? 40f : (type == 2 ? 60f : 36f);

                        obstacles.Add(new GroundObstacle
                        {
                            X = screenWidth + 20f,
                            Y = groundY - h,
                            Width = w,
                            Height = h,
                            Type = type,
                            Active = true
                        });

                        obstacleSpawnTimer = (float)(1.3 + random.NextDouble() * 1.4);
                    }

                    for (int i = 0; i < obstacles.Count; i++)
                    {
                        var obs = obstacles[i];
                        obs.X -= worldSpeed * dt;
                        if (obs.X + obs.Width < -50) obs.Active = false;
                        obstacles[i] = obs;
                    }

                    // --- UÇAN DÜŞMANLAR ---
                    enemySpawnTimer -= dt;
                    if (enemySpawnTimer <= 0f)
                    {
                        float spawnY = (float)random.Next(240, 340);
                        enemies.Add(new FlyingEnemy
                        {
                            X = screenWidth + 30f,
                            Y = spawnY,
                            BaseY = spawnY,
                            Width = 34f,
                            Height = 24f,
                            SinOffset = (float)(random.NextDouble() * 10),
                            Active = true
                        });

                        enemySpawnTimer = (float)(2.2 + random.NextDouble() * 2.0);
                    }

                    for (int i = 0; i < enemies.Count; i++)
                    {
                        var en = enemies[i];
                        en.X -= (worldSpeed * 0.95f) * dt;
                        en.SinOffset += dt * 5f;
                        en.Y = en.BaseY + (float)Math.Sin(en.SinOffset) * 12f;

                        if (en.X + en.Width < -50) en.Active = false;
                        enemies[i] = en;
                    }

                    // --- CAN PICKUP ---
                    healthSpawnTimer -= dt;
                    if (healthSpawnTimer <= 0f)
                    {
                        float spawnY = (random.Next(0, 2) == 0) ? (groundY - 55f) : (groundY - 125f);
                        healthPickups.Add(new HealthPickup
                        {
                            X = screenWidth + 30f,
                            Y = spawnY,
                            BaseY = spawnY,
                            Width = 26f,
                            Height = 24f,
                            SinOffset = (float)(random.NextDouble() * 10),
                            Active = true
                        });

                        healthSpawnTimer = (float)(8.0 + random.NextDouble() * 7.0);
                    }

                    for (int i = 0; i < healthPickups.Count; i++)
                    {
                        var hp = healthPickups[i];
                        hp.X -= worldSpeed * dt;
                        hp.SinOffset += dt * 4.5f;
                        hp.Y = hp.BaseY + (float)Math.Sin(hp.SinOffset) * 7f;

                        if (hp.X + hp.Width < -50) hp.Active = false;
                        healthPickups[i] = hp;
                    }

                    // --- KALKAN PICKUP ---
                    shieldSpawnTimer -= dt;
                    if (shieldSpawnTimer <= 0f)
                    {
                        float spawnY = (random.Next(0, 2) == 0) ? (groundY - 60f) : (groundY - 130f);
                        shieldPickups.Add(new ShieldPickup
                        {
                            X = screenWidth + 30f,
                            Y = spawnY,
                            BaseY = spawnY,
                            Width = 28f,
                            Height = 28f,
                            SinOffset = (float)(random.NextDouble() * 10),
                            Active = true
                        });

                        shieldSpawnTimer = (float)(14.0 + random.NextDouble() * 10.0);
                    }

                    for (int i = 0; i < shieldPickups.Count; i++)
                    {
                        var sh = shieldPickups[i];
                        sh.X -= worldSpeed * dt;
                        sh.SinOffset += dt * 5f;
                        sh.Y = sh.BaseY + (float)Math.Sin(sh.SinOffset) * 6f;

                        if (sh.X + sh.Width < -50) sh.Active = false;
                        shieldPickups[i] = sh;
                    }

                    // --- ÇARPIŞMALAR (COLLISIONS) ---
                    Rectangle playerHitbox = new Rectangle(robotX + 4, robotY + 4, robotWidth - 8, robotHeight - 4);

                    // 1. Can Toplama
                    for (int i = 0; i < healthPickups.Count; i++)
                    {
                        if (!healthPickups[i].Active) continue;
                        var hp = healthPickups[i];
                        Rectangle heartBox = new Rectangle(hp.X, hp.Y, hp.Width, hp.Height);

                        if (Raylib.CheckCollisionRecs(playerHitbox, heartBox))
                        {
                            hp.Active = false;
                            healthPickups[i] = hp;

                            if (health < maxHealth) health++;
                            score += 80;

                            SpawnExplosion(hp.X + hp.Width / 2, hp.Y + hp.Height / 2, 14, new Color(50, 255, 120, 255));
                        }
                    }

                    // 2. Kalkan Toplama
                    for (int i = 0; i < shieldPickups.Count; i++)
                    {
                        if (!shieldPickups[i].Active) continue;
                        var sh = shieldPickups[i];
                        Rectangle shieldBox = new Rectangle(sh.X, sh.Y, sh.Width, sh.Height);

                        if (Raylib.CheckCollisionRecs(playerHitbox, shieldBox))
                        {
                            sh.Active = false;
                            shieldPickups[i] = sh;

                            shieldTimer = 7.0f;
                            score += 100;

                            SpawnExplosion(sh.X + sh.Width / 2, sh.Y + sh.Height / 2, 20, new Color(0, 240, 255, 255));
                        }
                    }

                    // 3. Mermi & Düşman Çarpışması
                    for (int i = 0; i < bullets.Count; i++)
                    {
                        if (!bullets[i].Active) continue;
                        Rectangle bulletBox = new Rectangle(bullets[i].X, bullets[i].Y, 16, 6);

                        for (int j = 0; j < enemies.Count; j++)
                        {
                            if (!enemies[j].Active) continue;
                            Rectangle enemyBox = new Rectangle(enemies[j].X, enemies[j].Y, enemies[j].Width, enemies[j].Height);

                            if (Raylib.CheckCollisionRecs(bulletBox, enemyBox))
                            {
                                var b = bullets[i];
                                b.Active = false;
                                bullets[i] = b;

                                var en = enemies[j];
                                en.Active = false;
                                enemies[j] = en;

                                score += 120;
                                if (score > highScore) highScore = score;

                                SpawnExplosion(en.X + en.Width / 2, en.Y + en.Height / 2, 16, new Color(255, 90, 40, 255));
                                break;
                            }
                        }
                    }

                    // 4. Robot & Zemin Engeli Çarpışması
                    for (int i = 0; i < obstacles.Count; i++)
                    {
                        if (!obstacles[i].Active) continue;
                        Rectangle obsBox = new Rectangle(obstacles[i].X + 2, obstacles[i].Y + 2, obstacles[i].Width - 4, obstacles[i].Height - 2);

                        if (Raylib.CheckCollisionRecs(playerHitbox, obsBox))
                        {
                            if (shieldTimer > 0f)
                            {
                                SpawnExplosion(obstacles[i].X + obstacles[i].Width / 2, obstacles[i].Y, 8, new Color(0, 240, 255, 255));
                            }
                            else if (invulnerableTimer <= 0f)
                            {
                                health--;
                                invulnerableTimer = 1.4f;
                                screenShakeTimer = 0.25f;
                                SpawnExplosion(robotX + robotWidth / 2, robotY + robotHeight / 2, 12, new Color(255, 50, 50, 255));

                                if (health <= 0)
                                {
                                    currentGameState = GameState.GameOver;
                                    SpawnExplosion(robotX + robotWidth / 2, robotY + robotHeight / 2, 35, new Color(255, 120, 0, 255));

                                    // 🔔 MediatR ile Ölüm Olayını Yayınla & SQLite'a Kaydet
                                    await mediator.Publish(new RobotDiedEvent(robotId, score, playerName));

                                    // 📋 SQLite'tan Güncel Liderlik Tablosunu Çek (Top 5)
                                    leaderboard = await mediator.Send(new GetLeaderboardQuery(5));
                                }
                                break;
                            }
                        }
                    }

                    // 5. Robot & Uçan Düşman Çarpışması
                    if (currentGameState == GameState.Playing)
                    {
                        for (int i = 0; i < enemies.Count; i++)
                        {
                            if (!enemies[i].Active) continue;
                            Rectangle enemyBox = new Rectangle(enemies[i].X, enemies[i].Y, enemies[i].Width, enemies[i].Height);

                            if (Raylib.CheckCollisionRecs(playerHitbox, enemyBox))
                            {
                                var en = enemies[i];
                                en.Active = false;
                                enemies[i] = en;

                                if (shieldTimer > 0f)
                                {
                                    score += 150;
                                    SpawnExplosion(en.X + en.Width / 2, en.Y + en.Height / 2, 22, new Color(0, 240, 255, 255));
                                }
                                else if (invulnerableTimer <= 0f)
                                {
                                    health--;
                                    invulnerableTimer = 1.4f;
                                    screenShakeTimer = 0.25f;

                                    SpawnExplosion(en.X + en.Width / 2, en.Y + en.Height / 2, 16, new Color(255, 60, 60, 255));

                                    if (health <= 0)
                                    {
                                        currentGameState = GameState.GameOver;
                                        SpawnExplosion(robotX + robotWidth / 2, robotY + robotHeight / 2, 35, new Color(255, 120, 0, 255));

                                        // 🔔 MediatR ile Ölüm Olayını Yayınla & SQLite'a Kaydet
                                        await mediator.Publish(new RobotDiedEvent(robotId, score, playerName));

                                        // 📋 SQLite'tan Güncel Liderlik Tablosunu Çek (Top 5)
                                        leaderboard = await mediator.Send(new GetLeaderboardQuery(5));
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    bullets.RemoveAll(b => !b.Active);
                    obstacles.RemoveAll(o => !o.Active);
                    enemies.RemoveAll(e => !e.Active);
                    healthPickups.RemoveAll(h => !h.Active);
                    shieldPickups.RemoveAll(s => !s.Active);
                }
                // ------------------ 3. DURUM: OYUN BİTTİ (GAME OVER & SKOR TABLOSU) ------------------
                else if (currentGameState == GameState.GameOver)
                {
                    // [R] Tuşu -> Aynı isimle tekrar başla
                    if (Raylib.IsKeyPressed(KeyboardKey.R))
                    {
                        ResetGame();
                        currentGameState = GameState.Playing;
                    }
                    // [N] Tuşu -> Yeni isim girme ekranına dön
                    else if (Raylib.IsKeyPressed(KeyboardKey.N))
                    {
                        currentGameState = GameState.NameInput;
                    }
                }

                // Parçacıkları Güncelle
                for (int i = 0; i < particles.Count; i++)
                {
                    var p = particles[i];
                    p.Life += dt;
                    p.X += p.Vx * dt;
                    p.Y += p.Vy * dt;
                    p.Vy += 400f * dt;
                    if (p.Life >= p.MaxLife) p.Active = false;
                    particles[i] = p;
                }
                particles.RemoveAll(p => !p.Active);

                // ------------------ ÇİZİM (DRAW) ------------------
                Raylib.BeginDrawing();

                if (screenShakeTimer > 0f)
                {
                    Raylib.ClearBackground(new Color(35, 15, 25, 255));
                }
                else
                {
                    Raylib.ClearBackground(new Color(18, 16, 38, 255));
                }

                // 1. Arka Plan & Zemin
                DrawBackgroundSky(screenWidth, groundY);
                DrawGround(screenWidth, groundY, groundOffset);

                // --- EĞER İSİM GİRİŞİNDEYSE ---
                if (currentGameState == GameState.NameInput)
                {
                    DrawNameInputScreen(screenWidth, screenHeight, playerName, runAnimTimer);
                }
                else
                {
                    // 2. Oyun İçi Öğeler
                    for (int i = 0; i < obstacles.Count; i++) DrawGroundObstacle(obstacles[i]);
                    for (int i = 0; i < enemies.Count; i++) DrawFlyingDrone(enemies[i], runAnimTimer);
                    for (int i = 0; i < healthPickups.Count; i++) DrawHealthHeart(healthPickups[i], runAnimTimer);
                    for (int i = 0; i < shieldPickups.Count; i++) DrawShieldOrb(shieldPickups[i], runAnimTimer);
                    for (int i = 0; i < bullets.Count; i++) DrawLaserBullet(bullets[i]);

                    for (int i = 0; i < particles.Count; i++)
                    {
                        var p = particles[i];
                        float alpha = 1.0f - (p.Life / p.MaxLife);
                        Raylib.DrawRectangle((int)p.X, (int)p.Y, 3, 3, Raylib.ColorAlpha(p.Color, alpha));
                    }

                    // 3. Robot & Kalkan
                    if (currentGameState == GameState.Playing)
                    {
                        bool isBlinking = invulnerableTimer > 0f && ((int)(invulnerableTimer * 12) % 2 == 0);
                        if (!isBlinking)
                        {
                            DrawPixelRobot(robotX, robotY, isGrounded, jumpCount, runAnimTimer);
                            if (shieldTimer > 0f)
                            {
                                DrawShieldBubble(robotX + robotWidth / 2, robotY + robotHeight / 2, shieldTimer, runAnimTimer);
                            }
                        }
                    }

                    // 4. HUD Arayüzü
                    DrawHUD(playerName, health, maxHealth, ammo, maxAmmo, score, highScore, shieldTimer);

                    // 5. Game Over Skor Tablosu
                    if (currentGameState == GameState.GameOver)
                    {
                        DrawGameOverLeaderboard(screenWidth, screenHeight, playerName, score, leaderboard);
                    }
                }

                Raylib.EndDrawing();
            }

            Raylib.CloseWindow();
        }

        // ====================== EKRAN ÇİZİM METOTLARI ======================

        // 1. İsim Girişi Ekranı
        private static void DrawNameInputScreen(int screenWidth, int screenHeight, string currentName, float animTime)
        {
            // Panel Arka Planı
            Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, new Color(10, 8, 24, 230));

            // Ana Başlık
            string title = "ROBOT RUNNER";
            int titleW = Raylib.MeasureText(title, 42);
            Raylib.DrawText(title, (screenWidth - titleW) / 2, 90, 42, new Color(0, 240, 255, 255));

            string subTitle = "2D Pixel Edition - SQLite Entegrasyonlu";
            int subW = Raylib.MeasureText(subTitle, 16);
            Raylib.DrawText(subTitle, (screenWidth - subW) / 2, 140, 16, new Color(200, 220, 255, 200));

            // İsim Kutusu Çerçevesi
            int boxW = 420;
            int boxH = 60;
            int boxX = (screenWidth - boxW) / 2;
            int boxY = 220;

            Raylib.DrawRectangle(boxX, boxY - 30, boxW, 25, new Color(0, 0, 0, 0));
            Raylib.DrawText("OYUNCU ISMINIZI GIRIN:", boxX + 5, boxY - 26, 16, new Color(255, 230, 90, 255));

            Raylib.DrawRectangle(boxX, boxY, boxW, boxH, new Color(20, 20, 45, 255));
            Raylib.DrawRectangleLines(boxX, boxY, boxW, boxH, new Color(0, 240, 255, 255));

            // Yazılan İsim & Yanıp Sönen İmleç
            bool cursorVisible = ((int)(animTime * 3f) % 2) == 0;
            string displayText = currentName + (cursorVisible ? "_" : "");
            Raylib.DrawText(displayText, boxX + 20, boxY + 16, 28, Color.White);

            // Başlama Butonu İpucu
            string prompt = "[ ENTER ] TUSUNA BASARAK BASLAYIN";
            int promptW = Raylib.MeasureText(prompt, 18);
            Raylib.DrawText(prompt, (screenWidth - promptW) / 2, 330, 18, new Color(100, 255, 140, 255));

            // Kontrol Özeti Kutusu
            Raylib.DrawRectangle(boxX, 390, boxW, 80, new Color(15, 12, 30, 200));
            Raylib.DrawRectangleLines(boxX, 390, boxW, 80, new Color(100, 120, 160, 120));
            Raylib.DrawText("KONTROLLER:", boxX + 16, 400, 13, new Color(255, 200, 80, 255));
            Raylib.DrawText("• [SPACE x2] Zipla ve Cift Zipla     • [F/Sol Tik] Ates Et", boxX + 16, 420, 13, new Color(200, 220, 240, 220));
            Raylib.DrawText("• [R] Mermi Doldur (Reload)         • [A / D] Sag-Sol", boxX + 16, 442, 13, new Color(200, 220, 240, 220));
        }

        // 2. Oyun Sonu & SQLite Skor Tablosu Ekranı
        private static void DrawGameOverLeaderboard(int screenWidth, int screenHeight, string currentPlayer, int currentScore, List<GameSessionEntity> leaderboard)
        {
            // Karartma
            Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, new Color(5, 5, 15, 225));

            // Skor Paneli Kutusu
            int panelW = 580;
            int panelH = 430;
            int panelX = (screenWidth - panelW) / 2;
            int panelY = 45;

            Raylib.DrawRectangle(panelX, panelY, panelW, panelH, new Color(20, 15, 32, 250));
            Raylib.DrawRectangleLines(panelX, panelY, panelW, panelH, new Color(255, 50, 80, 255));

            // Başlık
            string title = "OYUN BITTI!";
            int titleW = Raylib.MeasureText(title, 32);
            Raylib.DrawText(title, (screenWidth - titleW) / 2, panelY + 16, 32, new Color(255, 50, 70, 255));

            // Bu Oyunun Skoru
            string currentScoreText = $"Oyuncu: {currentPlayer}   |   Skorunuz: {currentScore}";
            int scoreW = Raylib.MeasureText(currentScoreText, 17);
            Raylib.DrawText(currentScoreText, (screenWidth - scoreW) / 2, panelY + 56, 17, Color.White);

            // Tablo Başlığı
            Raylib.DrawText("🏆 EN YUKSEK SKORLAR (SQLITE)", panelX + 30, panelY + 95, 16, new Color(255, 220, 80, 255));

            // Tablo Başlık Satırı
            int tableY = panelY + 125;
            Raylib.DrawRectangle(panelX + 25, tableY, panelW - 50, 26, new Color(35, 30, 55, 255));
            Raylib.DrawText("SIRA", panelX + 38, tableY + 6, 14, new Color(200, 210, 230, 255));
            Raylib.DrawText("OYUNCU ADI", panelX + 110, tableY + 6, 14, new Color(200, 210, 230, 255));
            Raylib.DrawText("SKOR", panelX + 300, tableY + 6, 14, new Color(200, 210, 230, 255));
            Raylib.DrawText("TARIH", panelX + 430, tableY + 6, 14, new Color(200, 210, 230, 255));

            // Tablo Satırları (SQLite Verileri)
            for (int i = 0; i < 5; i++)
            {
                int rowY = tableY + 32 + (i * 32);
                bool hasData = i < leaderboard.Count;

                Color rowBg = (i % 2 == 0) ? new Color(25, 20, 42, 255) : new Color(30, 24, 48, 255);
                Raylib.DrawRectangle(panelX + 25, rowY, panelW - 50, 28, rowBg);

                if (hasData)
                {
                    var item = leaderboard[i];
                    bool isCurrent = item.Score == currentScore && item.PlayerName == currentPlayer;
                    Color textColor = isCurrent ? new Color(0, 255, 255, 255) : Color.White;

                    // Sıra madalyaları
                    string rankStr = $"#{i + 1}";
                    if (i == 0) rankStr = "🥇 1";
                    else if (i == 1) rankStr = "🥈 2";
                    else if (i == 2) rankStr = "🥉 3";

                    Raylib.DrawText(rankStr, panelX + 38, rowY + 6, 14, textColor);
                    Raylib.DrawText(item.PlayerName, panelX + 110, rowY + 6, 14, textColor);
                    Raylib.DrawText(item.Score.ToString(), panelX + 300, rowY + 6, 14, isCurrent ? new Color(255, 230, 80, 255) : new Color(255, 200, 60, 255));
                    Raylib.DrawText(item.CreatedAt.ToLocalTime().ToString("dd.MM HH:mm"), panelX + 430, rowY + 6, 12, new Color(180, 180, 200, 200));
                }
                else
                {
                    Raylib.DrawText($"#{i + 1}", panelX + 38, rowY + 6, 14, new Color(100, 100, 120, 150));
                    Raylib.DrawText("---", panelX + 110, rowY + 6, 14, new Color(100, 100, 120, 150));
                    Raylib.DrawText("0", panelX + 300, rowY + 6, 14, new Color(100, 100, 120, 150));
                    Raylib.DrawText("-", panelX + 430, rowY + 6, 12, new Color(100, 100, 120, 150));
                }
            }

            // Alt Bilgi / Aksiyon Butonları
            Raylib.DrawRectangle(panelX + 35, panelY + 365, panelW - 70, 42, new Color(15, 12, 28, 220));
            Raylib.DrawRectangleLines(panelX + 35, panelY + 365, panelW - 70, 42, new Color(0, 240, 255, 180));

            string actionText = "[ R ] Ayni Isimle Tekrar Oyna     |     [ N ] Yeni Isim Gir";
            int actW = Raylib.MeasureText(actionText, 15);
            Raylib.DrawText(actionText, (screenWidth - actW) / 2, panelY + 378, 15, new Color(0, 240, 255, 255));
        }

        // 3. HUD Arayüzü
        private static void DrawHUD(string player, int health, int maxHealth, int ammo, int maxAmmo, int score, int highScore, float shieldTime)
        {
            // Panel Arka Planı (Sol Üst)
            Raylib.DrawRectangle(16, 14, 230, 68, new Color(10, 10, 25, 210));
            Raylib.DrawRectangleLines(16, 14, 230, 68, new Color(0, 200, 255, 140));

            // OYUNCU ADI
            Raylib.DrawText($"PILOT: {player}", 26, 18, 13, new Color(0, 240, 255, 255));

            // CAN
            Raylib.DrawText("CAN:", 26, 36, 14, Color.White);
            for (int i = 0; i < maxHealth; i++)
            {
                Color heartColor = (i < health) ? new Color(255, 45, 75, 255) : new Color(70, 30, 40, 180);
                Raylib.DrawRectangle(68 + (i * 20), 36, 15, 13, heartColor);
                if (i < health)
                {
                    Raylib.DrawRectangle(70 + (i * 20), 38, 4, 4, new Color(255, 180, 190, 255));
                }
            }

            // MERMİ
            Raylib.DrawText("MERMI:", 26, 54, 12, new Color(200, 220, 255, 255));
            for (int i = 0; i < maxAmmo; i++)
            {
                Color ammoColor = (i < ammo) ? new Color(0, 240, 255, 255) : new Color(25, 55, 70, 180);
                Raylib.DrawRectangle(74 + (i * 13), 54, 8, 11, ammoColor);
            }

            // KALKAN DURUMU
            if (shieldTime > 0f)
            {
                Raylib.DrawRectangle(255, 14, 130, 24, new Color(10, 10, 25, 210));
                Raylib.DrawRectangleLines(255, 14, 130, 24, new Color(0, 240, 255, 200));
                Raylib.DrawText($"🛡️ KALKAN: {shieldTime:0.0}s", 262, 19, 12, new Color(0, 255, 255, 255));
            }

            // SKOR (Sağ Üst)
            Raylib.DrawRectangle(730, 14, 214, 60, new Color(10, 10, 25, 210));
            Raylib.DrawRectangleLines(730, 14, 214, 60, new Color(255, 190, 0, 140));

            Raylib.DrawText($"SKOR: {score}", 742, 20, 18, new Color(255, 230, 90, 255));
            Raylib.DrawText($"REKOR: {highScore}", 742, 42, 14, new Color(200, 200, 220, 200));
            Raylib.DrawText("💾 SQLite Tablo Aktif", 742, 58, 10, new Color(100, 255, 120, 200));

            // Alt Bilgi / Kontrol İpuçları
            Raylib.DrawRectangle(190, 508, 580, 24, new Color(10, 10, 25, 170));
            Raylib.DrawText("[SPACE x2] Cift Zipla  [F/Sol Tik] Ates  [R] Mermi Doldur  [A/D] Hareket", 202, 513, 13, new Color(180, 220, 255, 230));
        }

        // ====================== OYUN ÖĞELERİ ÇİZİM METOTLARI ======================

        private static void DrawBackgroundSky(int screenWidth, float groundY)
        {
            Raylib.DrawCircle(120, 80, 1.5f, new Color(200, 220, 255, 120));
            Raylib.DrawCircle(280, 140, 2f, new Color(255, 200, 255, 140));
            Raylib.DrawCircle(520, 90, 1.5f, new Color(180, 240, 255, 130));
            Raylib.DrawCircle(750, 130, 2.5f, new Color(255, 255, 200, 160));
            Raylib.DrawCircle(880, 70, 1.5f, new Color(220, 200, 255, 120));

            Raylib.DrawTriangle(
                new System.Numerics.Vector2(200, groundY),
                new System.Numerics.Vector2(320, groundY - 140),
                new System.Numerics.Vector2(440, groundY),
                new Color(28, 24, 55, 255));

            Raylib.DrawTriangle(
                new System.Numerics.Vector2(550, groundY),
                new System.Numerics.Vector2(680, groundY - 180),
                new System.Numerics.Vector2(810, groundY),
                new Color(32, 26, 62, 255));
        }

        private static void DrawGround(int screenWidth, float groundY, float groundOffset)
        {
            Raylib.DrawRectangle(0, (int)groundY, screenWidth, 120, new Color(22, 18, 44, 255));
            Raylib.DrawRectangle(0, (int)groundY - 1, screenWidth, 3, new Color(0, 240, 255, 255));
            Raylib.DrawRectangle(0, (int)groundY + 2, screenWidth, 2, new Color(0, 160, 220, 180));

            int spacing = 48;
            int shift = (int)(groundOffset % spacing);
            for (int x = -shift; x < screenWidth + spacing; x += spacing)
            {
                Raylib.DrawLine(x, (int)groundY + 4, x - 20, (int)groundY + 110, new Color(0, 180, 230, 60));
            }

            Raylib.DrawLine(0, (int)groundY + 35, screenWidth, (int)groundY + 35, new Color(0, 180, 230, 45));
            Raylib.DrawLine(0, (int)groundY + 75, screenWidth, (int)groundY + 75, new Color(0, 180, 230, 30));
        }

        private static void DrawPixelRobot(float x, float y, bool isGrounded, int jumpCount, float animTime)
        {
            Color steelDark = new Color(75, 85, 105, 255);
            Color steelLight = new Color(175, 190, 210, 255);
            Color neonCyan = new Color(0, 255, 255, 255);
            Color coreOrange = new Color(255, 130, 20, 255);

            int px = (int)x;
            int py = (int)y;

            if (isGrounded)
            {
                float legSwing = (float)Math.Sin(animTime * 18f) * 6f;
                Raylib.DrawRectangle(px + 8, py + 34, 6, (int)(14 + legSwing), steelDark);
                Raylib.DrawRectangle(px + 6, py + 34 + (int)(14 + legSwing) - 3, 10, 3, steelLight);

                Raylib.DrawRectangle(px + 20, py + 34, 6, (int)(14 - legSwing), steelDark);
                Raylib.DrawRectangle(px + 18, py + 34 + (int)(14 - legSwing) - 3, 10, 3, steelLight);
            }
            else
            {
                Raylib.DrawRectangle(px + 6, py + 34, 7, 9, steelDark);
                Raylib.DrawRectangle(px + 18, py + 32, 7, 8, steelDark);

                Color thrustColor = (jumpCount == 2) ? new Color(0, 255, 255, 255) : new Color(255, 180, 40, 255);
                int thrustHeight = (jumpCount == 2) ? 9 : 5;

                Raylib.DrawRectangle(px + 10, py + 43, 4, thrustHeight, thrustColor);
                Raylib.DrawRectangle(px + 22, py + 40, 4, thrustHeight, thrustColor);
            }

            Raylib.DrawRectangle(px + 6, py + 14, 24, 20, steelDark);
            Raylib.DrawRectangle(px + 8, py + 16, 20, 16, steelLight);

            Raylib.DrawRectangle(px + 14, py + 19, 8, 8, coreOrange);
            Raylib.DrawRectangle(px + 16, py + 21, 4, 4, new Color(255, 240, 120, 255));

            Raylib.DrawRectangle(px + 12, py + 11, 12, 4, steelDark);
            Raylib.DrawRectangle(px + 7, py, 22, 12, steelLight);
            Raylib.DrawRectangle(px + 5, py + 3, 2, 7, steelDark);

            Raylib.DrawRectangle(px + 15, py + 3, 13, 5, neonCyan);
            Raylib.DrawRectangle(px + 22, py + 4, 4, 3, new Color(255, 255, 255, 255));

            Raylib.DrawLine(px + 11, py, px + 11, py - 6, steelDark);
            Raylib.DrawCircle(px + 11, py - 7, 2.5f, new Color(255, 60, 80, 255));

            Raylib.DrawRectangle(px + 22, py + 18, 14, 6, steelDark);
            Raylib.DrawRectangle(px + 32, py + 17, 4, 8, neonCyan);
        }

        private static void DrawShieldBubble(float cx, float cy, float shieldTime, float animTime)
        {
            float pulse = (float)Math.Sin(animTime * 10f) * 3f;
            float radius = 34f + pulse;

            Raylib.DrawCircle((int)cx, (int)cy, radius, new Color(0, 200, 255, 45));
            Raylib.DrawCircleLines((int)cx, (int)cy, radius, new Color(0, 255, 255, 220));
            Raylib.DrawCircleLines((int)cx, (int)cy, radius - 3, new Color(180, 240, 255, 140));

            for (int i = 0; i < 4; i++)
            {
                float angle = animTime * 4f + (i * (float)Math.PI / 2f);
                int ox = (int)(cx + Math.Cos(angle) * (radius - 2));
                int oy = (int)(cy + Math.Sin(angle) * (radius - 2));
                Raylib.DrawCircle(ox, oy, 2.5f, Color.White);
            }
        }

        private static void DrawLaserBullet(LaserBullet b)
        {
            int bx = (int)b.X;
            int by = (int)b.Y;

            Raylib.DrawRectangle(bx - 14, by + 1, 12, 3, new Color(255, 140, 0, 160));
            Raylib.DrawRectangle(bx, by, 16, 5, new Color(255, 240, 50, 255));
            Raylib.DrawRectangle(bx + 4, by + 1, 10, 3, new Color(255, 255, 255, 255));
        }

        private static void DrawGroundObstacle(GroundObstacle obs)
        {
            int ox = (int)obs.X;
            int oy = (int)obs.Y;
            int ow = (int)obs.Width;
            int oh = (int)obs.Height;

            if (obs.Type == 0)
            {
                Raylib.DrawTriangle(
                    new System.Numerics.Vector2(ox, oy + oh),
                    new System.Numerics.Vector2(ox + ow / 2, oy),
                    new System.Numerics.Vector2(ox + ow, oy + oh),
                    new Color(255, 55, 55, 255));

                Raylib.DrawTriangle(
                    new System.Numerics.Vector2(ox + 6, oy + oh),
                    new System.Numerics.Vector2(ox + ow / 2, oy + 7),
                    new System.Numerics.Vector2(ox + ow - 6, oy + oh),
                    new Color(255, 160, 60, 255));
            }
            else if (obs.Type == 1)
            {
                Raylib.DrawRectangle(ox, oy, ow, oh, new Color(60, 30, 40, 255));
                Raylib.DrawRectangle(ox + 3, oy + 3, ow - 6, oh - 6, new Color(180, 40, 60, 255));
                Raylib.DrawRectangle(ox + 7, oy + (oh / 2) - 3, ow - 14, 6, new Color(255, 220, 0, 255));
            }
            else
            {
                Raylib.DrawRectangle(ox + 6, oy, ow - 12, oh, new Color(80, 50, 100, 255));
                Raylib.DrawRectangle(ox, oy, ow, 10, new Color(255, 50, 100, 255));
                Raylib.DrawCircle(ox + ow / 2, oy + 5, 4f, new Color(255, 255, 255, 255));
            }
        }

        private static void DrawFlyingDrone(FlyingEnemy en, float animTime)
        {
            int ex = (int)en.X;
            int ey = (int)en.Y;
            int ew = (int)en.Width;
            int eh = (int)en.Height;

            Color droneBody = new Color(200, 40, 60, 255);
            Color darkMetal = new Color(50, 20, 35, 255);

            Raylib.DrawRectangle(ex + 4, ey + 4, ew - 8, eh - 8, droneBody);
            Raylib.DrawRectangle(ex + 8, ey + 6, ew - 16, eh - 12, darkMetal);

            Raylib.DrawRectangle(ex + 5, ey + 8, 9, 8, new Color(255, 230, 40, 255));
            Raylib.DrawRectangle(ex + 6, ey + 10, 4, 4, new Color(255, 40, 40, 255));

            float flameSize = 4f + (float)Math.Sin(animTime * 25f) * 3f;
            Raylib.DrawRectangle(ex + ew - 4, ey + 8, (int)flameSize, 7, new Color(255, 140, 0, 255));
            Raylib.DrawRectangle(ex + ew - 2, ey + 10, (int)(flameSize * 0.6f), 3, new Color(255, 240, 80, 255));

            Raylib.DrawRectangle(ex + 10, ey, 14, 4, darkMetal);
            Raylib.DrawRectangle(ex + 10, ey + eh - 4, 14, 4, darkMetal);
        }

        private static void DrawHealthHeart(HealthPickup hp, float animTime)
        {
            int hx = (int)hp.X;
            int hy = (int)hp.Y;

            float pulse = (float)Math.Sin(animTime * 7f) * 2f;
            Raylib.DrawCircle(hx + 13, hy + 11, 14f + pulse, new Color(255, 50, 90, 70));

            Color heartRed = new Color(255, 40, 75, 255);
            Color heartLight = new Color(255, 150, 180, 255);

            Raylib.DrawRectangle(hx + 3, hy + 3, 9, 8, heartRed);
            Raylib.DrawRectangle(hx + 14, hy + 3, 9, 8, heartRed);
            Raylib.DrawRectangle(hx + 3, hy + 8, 20, 7, heartRed);

            Raylib.DrawTriangle(
                new System.Numerics.Vector2(hx + 3, hy + 14),
                new System.Numerics.Vector2(hx + 13, hy + 22),
                new System.Numerics.Vector2(hx + 23, hy + 14),
                heartRed);

            Raylib.DrawRectangle(hx + 5, hy + 5, 3, 3, Color.White);
            Raylib.DrawRectangle(hx + 16, hy + 5, 2, 2, heartLight);
        }

        private static void DrawShieldOrb(ShieldPickup sh, float animTime)
        {
            int sx = (int)sh.X;
            int sy = (int)sh.Y;

            float pulse = (float)Math.Sin(animTime * 8f) * 2f;
            Raylib.DrawCircle(sx + 14, sy + 14, 15f + pulse, new Color(0, 200, 255, 60));

            Raylib.DrawCircle(sx + 14, sy + 14, 10f, new Color(0, 230, 255, 255));
            Raylib.DrawCircle(sx + 14, sy + 14, 7f, new Color(220, 250, 255, 255));

            Raylib.DrawRectangle(sx + 11, sy + 10, 6, 8, new Color(0, 120, 220, 255));
            Raylib.DrawTriangle(
                new System.Numerics.Vector2(sx + 11, sy + 18),
                new System.Numerics.Vector2(sx + 14, sy + 22),
                new System.Numerics.Vector2(sx + 17, sy + 18),
                new Color(0, 120, 220, 255));
        }
    }
}
