using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SpaceInvaders
{
    public partial class SpaceInvadersForm : Form
    {
        private GamePanel gamePanel;
        private Timer gameTimer;
        private GameState gameState = GameState.Title;
        private Player player;
        private List<Bullet> bullets;
        private List<Enemy> enemies;
        private List<PowerUp> powerUps;
        private List<Shield> shields;
        private Boss boss;
        private List<Explosion> explosions;
        private Random rnd = new Random();
        private int level = 1;
        private int score = 0;
        private int lives = 3;
        private int enemyDirection = 1; // 1 = right, -1 = left
        private int enemySpeed = 2;
        private bool bossActive = false;
        private int enemyRows = 3;
        private int enemyCols = 10;
        private const int PLAYER_SPEED = 8;
        private const int BULLET_SPEED = 15;
        private const int ENEMY_BULLET_SPEED = 5;
        private bool leftPressed, rightPressed, spacePressed;
        private bool canShoot = true;
        private int shootCooldown = 20; // frames
        private int cooldownTimer = 0;
        private Font gameFont = new Font("Arial", 14, FontStyle.Bold);
        private Font smallFont = new Font("Arial", 10);
        private HighScoreManager highScoreManager;
        private bool rapidFireActive = false;
        private int rapidFireTimer = 0;
        private int bossBulletTimer = 0;

        public SpaceInvadersForm()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            this.ClientSize = new Size(800, 600);
            this.Text = "Space Invaders";
            this.KeyDown += SpaceInvadersForm_KeyDown;
            this.KeyUp += SpaceInvadersForm_KeyUp;

            gamePanel = new GamePanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };
            this.Controls.Add(gamePanel);

            gameTimer = new Timer { Interval = 16 }; // ~60 FPS
            gameTimer.Tick += GameTimer_Tick;
            gameTimer.Start();

            highScoreManager = new HighScoreManager();
            ResetGame();
        }

        private void ResetGame()
        {
            player = new Player(new PointF(400, 550));
            bullets = new List<Bullet>();
            enemies = new List<Enemy>();
            powerUps = new List<PowerUp>();
            shields = new List<Shield>();
            boss = null;
            explosions = new List<Explosion>();
            score = 0;
            lives = 3;
            level = 1;
            enemyRows = 3;
            enemyCols = 10;
            enemySpeed = 2;
            enemyDirection = 1;
            bossActive = false;
            rapidFireActive = false;
            rapidFireTimer = 0;
            cooldownTimer = 0;
            canShoot = true;
            bossBulletTimer = 0;

            // Create shields (4 blocks)
            float shieldStartX = 150;
            float shieldY = 480;
            for (int i = 0; i < 4; i++)
            {
                shields.Add(new Shield(new RectangleF(shieldStartX + i * 170, shieldY, 80, 20)));
            }

            CreateEnemies();
        }

        private void CreateEnemies()
        {
            enemies.Clear();
            float startX = 100;
            float startY = 50;
            float spacingX = 60;
            float spacingY = 40;
            int rows = enemyRows;
            int cols = enemyCols;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    // Different colors per row
                    Color color = Color.Green;
                    if (r == 0) color = Color.Cyan;
                    else if (r == 1) color = Color.Yellow;
                    enemies.Add(new Enemy(new PointF(startX + c * spacingX, startY + r * spacingY), color));
                }
            }
        }

        private void SpaceInvadersForm_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left: leftPressed = true; break;
                case Keys.Right: rightPressed = true; break;
                case Keys.Space: spacePressed = true; break;
                case Keys.Enter:
                    if (gameState == GameState.Title || gameState == GameState.GameOver) StartGame();
                    break;
                case Keys.Escape:
                    if (gameState == GameState.Playing) gameState = GameState.Paused;
                    else if (gameState == GameState.Paused) gameState = GameState.Playing;
                    break;
            }
        }

        private void SpaceInvadersForm_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left: leftPressed = false; break;
                case Keys.Right: rightPressed = false; break;
                case Keys.Space: spacePressed = false; break;
            }
        }

        private void StartGame()
        {
            ResetGame();
            gameState = GameState.Playing;
        }

        private void GameTimer_Tick(object sender, EventArgs e)
        {
            if (gameState == GameState.Playing)
            {
                UpdateGame();
            }
            gamePanel.Invalidate();
        }

        private void UpdateGame()
        {
            // Player movement
            if (leftPressed) player.Position = new PointF(player.Position.X - PLAYER_SPEED, player.Position.Y);
            if (rightPressed) player.Position = new PointF(player.Position.X + PLAYER_SPEED, player.Position.Y);
            // Keep player in bounds
            if (player.Position.X < 10) player.Position = new PointF(10, player.Position.Y);
            if (player.Position.X > ClientSize.Width - 40) player.Position = new PointF(ClientSize.Width - 40, player.Position.Y);

            // Shooting cooldown
            if (cooldownTimer > 0) cooldownTimer--;
            if (cooldownTimer <= 0) canShoot = true;
            if (spacePressed && canShoot)
            {
                ShootBullet();
                canShoot = false;
                cooldownTimer = rapidFireActive ? 5 : shootCooldown;
                if (rapidFireActive)
                {
                    rapidFireTimer--;
                    if (rapidFireTimer <= 0) rapidFireActive = false;
                }
            }

            // Update bullets (player)
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                bullets[i].Position = new PointF(bullets[i].Position.X, bullets[i].Position.Y - BULLET_SPEED);
                if (bullets[i].Position.Y < 0)
                {
                    bullets.RemoveAt(i);
                    continue;
                }
                // Check collision with enemies
                bool bulletRemoved = false;
                for (int j = enemies.Count - 1; j >= 0; j--)
                {
                    if (RectContains(enemies[j].Bounds, bullets[i].Position))
                    {
                        explosions.Add(new Explosion(enemies[j].Bounds.Location, Color.Yellow));
                        score += 100;
                        Console.Beep(800, 50); // enemy hit sound
                        enemies.RemoveAt(j);
                        bulletRemoved = true;
                        // Drop power-up randomly (20% chance)
                        if (rnd.NextDouble() < 0.2)
                        {
                            PowerUpType type = (PowerUpType)rnd.Next(4);
                            powerUps.Add(new PowerUp(enemies[j].Bounds.Location, type));
                        }
                        break;
                    }
                }
                if (bulletRemoved) { bullets.RemoveAt(i); continue; }
                // Check collision with boss
                if (bossActive && boss != null && boss.Bounds.Contains(Point.Round(bullets[i].Position)))
                {
                    boss.Health -= 1;
                    explosions.Add(new Explosion(bullets[i].Position, Color.Red));
                    bullets.RemoveAt(i);
                    if (boss.Health <= 0)
                    {
                        explosions.Add(new Explosion(boss.Bounds.Location, Color.Orange, 30));
                        score += 1000;
                        Console.Beep(200, 200); // boss explosion sound
                        bossActive = false;
                        boss = null;
                        NextLevel();
                    }
                    continue;
                }
                // Check collision with shields
                for (int j = shields.Count - 1; j >= 0; j--)
                {
                    if (shields[j].Bounds.Contains(Point.Round(bullets[i].Position)))
                    {
                        shields[j].Health--;
                        if (shields[j].Health <= 0)
                            shields.RemoveAt(j);
                        bullets.RemoveAt(i);
                        bulletRemoved = true;
                        break;
                    }
                }
                if (bulletRemoved) continue;
            }

            // Enemy movement
            bool hitEdge = false;
            if (enemies.Count > 0)
            {
                float maxX = enemies.Max(e => e.Bounds.Right);
                float minX = enemies.Min(e => e.Bounds.X);
                if (maxX >= ClientSize.Width - 20 || minX <= 20)
                    hitEdge = true;

                foreach (var enemy in enemies)
                {
                    enemy.Position = new PointF(enemy.Position.X + enemyDirection * enemySpeed, enemy.Position.Y);
                    // Random shooting (1% chance per frame)
                    if (!bossActive && rnd.NextDouble() < 0.005)
                    {
                        bullets.Add(new Bullet(enemy.Position, false)); // enemy bullet
                    }
                }
                if (hitEdge)
                {
                    enemyDirection *= -1;
                    // Drop down
                    foreach (var enemy in enemies)
                        enemy.Position = new PointF(enemy.Position.X, enemy.Position.Y + 20);
                    // Check if enemies reach bottom
                    if (enemies.Any(e => e.Bounds.Bottom >= player.Position.Y))
                    {
                        GameOver();
                        return;
                    }
                }
            }

            // Enemy bullets
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                if (!bullets[i].IsPlayerBullet)
                {
                    bullets[i].Position = new PointF(bullets[i].Position.X, bullets[i].Position.Y + ENEMY_BULLET_SPEED);
                    // Check collision with player
                    if (RectContains(new RectangleF(player.Position.X, player.Position.Y, 40, 20), bullets[i].Position))
                    {
                        explosions.Add(new Explosion(player.Position, Color.Red));
                        lives--;
                        Console.Beep(400, 100);
                        bullets.RemoveAt(i);
                        if (lives <= 0) { GameOver(); return; }
                        continue;
                    }
                    // Check collision with shields
                    for (int j = shields.Count - 1; j >= 0; j--)
                    {
                        if (shields[j].Bounds.Contains(Point.Round(bullets[i].Position)))
                        {
                            shields[j].Health--;
                            if (shields[j].Health <= 0)
                                shields.RemoveAt(j);
                            bullets.RemoveAt(i);
                            break;
                        }
                    }
                    if (bullets.Count > i && bullets[i].Position.Y > ClientSize.Height)
                        bullets.RemoveAt(i);
                }
            }

            // Power-ups
            for (int i = powerUps.Count - 1; i >= 0; i--)
            {
                powerUps[i].Position = new PointF(powerUps[i].Position.X, powerUps[i].Position.Y + 2);
                if (RectContains(new RectangleF(player.Position.X, player.Position.Y, 40, 20), powerUps[i].Position))
                {
                    ApplyPowerUp(powerUps[i].Type);
                    powerUps.RemoveAt(i);
                    continue;
                }
                if (powerUps[i].Position.Y > ClientSize.Height)
                    powerUps.RemoveAt(i);
            }

            // Boss logic
            if (!bossActive && level % 3 == 0 && enemies.Count == 0 && boss == null)
            {
                // Spawn boss
                bossActive = true;
                boss = new Boss(new PointF(400, 60));
                bossBulletTimer = 0;
            }
            if (bossActive && boss != null)
            {
                // Boss movement
                boss.Position = new PointF(boss.Position.X + enemyDirection * (enemySpeed + 1), boss.Position.Y);
                if (boss.Bounds.Left <= 20 || boss.Bounds.Right >= ClientSize.Width - 20)
                    enemyDirection *= -1;
                // Boss shooting
                bossBulletTimer++;
                if (bossBulletTimer > 30)
                {
                    bossBulletTimer = 0;
                    bullets.Add(new Bullet(new PointF(boss.Bounds.X + 40, boss.Bounds.Bottom), false));
                }
            }

            // Update explosions
            for (int i = explosions.Count - 1; i >= 0; i--)
            {
                explosions[i].Update();
                if (explosions[i].IsExpired)
                    explosions.RemoveAt(i);
            }

            // Level complete if all enemies gone and no boss active
            if (enemies.Count == 0 && !bossActive && (boss == null || boss.Health <= 0))
            {
                NextLevel();
            }
        }

        private void ShootBullet()
        {
            PointF bulletPos = new PointF(player.Position.X + 18, player.Position.Y);
            bullets.Add(new Bullet(bulletPos, true));
            Console.Beep(1000, 30);
        }

        private void ApplyPowerUp(PowerUpType type)
        {
            Console.Beep(1200, 100);
            switch (type)
            {
                case PowerUpType.ShieldRepair:
                    foreach (var s in shields)
                        s.Health = 3;
                    break;
                case PowerUpType.RapidFire:
                    rapidFireActive = true;
                    rapidFireTimer = 300; // 5 seconds at 60fps
                    break;
                case PowerUpType.ExtraLife:
                    lives = Math.Min(lives + 1, 5);
                    break;
                case PowerUpType.Bomb:
                    // Destroy all enemies on screen
                    for (int i = enemies.Count - 1; i >= 0; i--)
                    {
                        explosions.Add(new Explosion(enemies[i].Bounds.Location, Color.Orange));
                        score += 50;
                        enemies.RemoveAt(i);
                    }
                    break;
            }
        }

        private void NextLevel()
        {
            level++;
            enemyRows = Math.Min(level + 2, 6);
            enemyCols = 10;
            enemySpeed = 2 + level;
            enemyDirection = 1;
            bossActive = false;
            boss = null;
            CreateEnemies();
            // Increase difficulty: enemy speed, boss frequency handled.
        }

        private void GameOver()
        {
            gameState = GameState.GameOver;
            Console.Beep(200, 500);
            highScoreManager.AddScore(score);
        }

        private bool RectContains(RectangleF rect, PointF point)
        {
            return point.X >= rect.X && point.X <= rect.Right &&
                   point.Y >= rect.Y && point.Y <= rect.Bottom;
        }

        // Paint
        private void gamePanel_Paint(object sender, PaintEventArgs e)
        {
            // This will be handled by the GamePanel's Paint event.
        }

        private class GamePanel : Panel
        {
            public GamePanel()
            {
                this.DoubleBuffered = true;
                this.Paint += GamePanel_Paint;
            }

            private void GamePanel_Paint(object sender, PaintEventArgs e)
            {
                var form = this.FindForm() as SpaceInvadersForm;
                form?.DrawGame(e.Graphics);
            }
        }

        private void DrawGame(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (gameState == GameState.Title)
            {
                DrawTitleScreen(g);
                return;
            }
            if (gameState == GameState.Paused)
            {
                DrawTitleScreen(g); // can overlay
                return;
            }

            // Draw player
            g.FillRectangle(Brushes.Cyan, player.Position.X, player.Position.Y, 40, 20);
            // Draw bullets
            foreach (var b in bullets)
            {
                g.FillRectangle(b.IsPlayerBullet ? Brushes.White : Brushes.Red, b.Position.X - 1, b.Position.Y - 5, 3, 10);
            }
            // Draw enemies
            foreach (var enemy in enemies)
            {
                g.FillRectangle(new SolidBrush(enemy.Color), enemy.Bounds);
            }
            // Draw boss
            if (bossActive && boss != null)
            {
                g.FillRectangle(Brushes.Purple, boss.Bounds);
                // Health bar
                float healthPercent = (float)boss.Health / boss.MaxHealth;
                g.FillRectangle(Brushes.Red, boss.Bounds.X, boss.Bounds.Y - 10, boss.Bounds.Width, 5);
                g.FillRectangle(Brushes.Lime, boss.Bounds.X, boss.Bounds.Y - 10, boss.Bounds.Width * healthPercent, 5);
            }
            // Draw shields
            foreach (var s in shields)
            {
                Color shieldColor = s.Health == 3 ? Color.Green : (s.Health == 2 ? Color.Yellow : Color.Red);
                g.FillRectangle(new SolidBrush(shieldColor), s.Bounds);
            }
            // Draw power-ups
            foreach (var p in powerUps)
            {
                Brush brush = Brushes.Yellow;
                if (p.Type == PowerUpType.RapidFire) brush = Brushes.Orange;
                else if (p.Type == PowerUpType.ExtraLife) brush = Brushes.Pink;
                else if (p.Type == PowerUpType.Bomb) brush = Brushes.Red;
                g.FillEllipse(brush, p.Position.X - 5, p.Position.Y - 5, 10, 10);
                g.DrawString(p.Type.ToString().Substring(0, 1), smallFont, Brushes.Black, p.Position.X - 5, p.Position.Y - 5);
            }
            // Draw explosions
            foreach (var exp in explosions)
            {
                int size = (int)(exp.Radius * 2);
                g.FillEllipse(new SolidBrush(Color.FromArgb(exp.Alpha, exp.Color)), exp.Position.X - size / 2, exp.Position.Y - size / 2, size, size);
            }

            // UI
            g.DrawString($"Score: {score}", gameFont, Brushes.White, 10, 10);
            g.DrawString($"Lives: {lives}", gameFont, Brushes.White, 10, 35);
            g.DrawString($"Level: {level}", gameFont, Brushes.White, 10, 60);
            g.DrawString($"High: {highScoreManager.HighScore}", gameFont, Brushes.White, 650, 10);

            if (gameState == GameState.GameOver)
            {
                string message = $"GAME OVER\nScore: {score}\nHigh Score: {highScoreManager.HighScore}\nPress Enter to Play Again";
                SizeF size = g.MeasureString(message, gameFont);
                g.DrawString(message, gameFont, Brushes.Red, (ClientSize.Width - size.Width) / 2, (ClientSize.Height - size.Height) / 2);
            }
        }

        private void DrawTitleScreen(Graphics g)
        {
            string title = "SPACE INVADERS";
            SizeF titleSize = g.MeasureString(title, new Font("Arial", 30, FontStyle.Bold));
            g.DrawString(title, new Font("Arial", 30, FontStyle.Bold), Brushes.Lime,
                (ClientSize.Width - titleSize.Width) / 2, 100);
            string instr = "Press Enter to Start\nArrow Keys to Move, Space to Shoot\nEsc to Pause";
            g.DrawString(instr, gameFont, Brushes.White,
                (ClientSize.Width - 200) / 2, 250);
            g.DrawString($"High Score: {highScoreManager.HighScore}", gameFont, Brushes.Yellow,
                (ClientSize.Width - 150) / 2, 400);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            highScoreManager.Save();
            base.OnFormClosing(e);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new Size(800, 600);
            this.Name = "SpaceInvadersForm";
            this.ResumeLayout(false);
        }
    }

    // ---------- Game Objects ----------
    enum GameState { Title, Playing, Paused, GameOver }
    enum PowerUpType { ShieldRepair, RapidFire, ExtraLife, Bomb }

    class Player
    {
        public PointF Position { get; set; }
        public Player(PointF pos) { Position = pos; }
    }

    class Bullet
    {
        public PointF Position { get; set; }
        public bool IsPlayerBullet { get; set; }
        public Bullet(PointF pos, bool isPlayer)
        {
            Position = pos;
            IsPlayerBullet = isPlayer;
        }
    }

    class Enemy
    {
        public PointF Position { get; set; }
        public Color Color { get; set; }
        public RectangleF Bounds => new RectangleF(Position.X, Position.Y, 30, 20);
        public Enemy(PointF pos, Color color)
        {
            Position = pos;
            Color = color;
        }
    }

    class PowerUp
    {
        public PointF Position { get; set; }
        public PowerUpType Type { get; set; }
        public PowerUp(PointF pos, PowerUpType type)
        {
            Position = pos;
            Type = type;
        }
    }

    class Shield
    {
        public RectangleF Bounds { get; set; }
        public int Health { get; set; } = 3;
        public Shield(RectangleF bounds)
        {
            Bounds = bounds;
        }
    }

    class Boss
    {
        public PointF Position { get; set; }
        public int Health { get; set; } = 20;
        public int MaxHealth { get; set; } = 20;
        public RectangleF Bounds => new RectangleF(Position.X, Position.Y, 80, 40);
        public Boss(PointF pos)
        {
            Position = pos;
        }
    }

    class Explosion
    {
        public PointF Position { get; set; }
        public Color Color { get; set; }
        public float Radius { get; set; }
        public int Alpha { get; set; }
        public float GrowthRate { get; set; }
        public bool IsExpired => Alpha <= 0;
        public Explosion(PointF pos, Color color, float radius = 10)
        {
            Position = pos;
            Color = color;
            Radius = radius;
            Alpha = 200;
            GrowthRate = 1.5f;
        }
        public void Update()
        {
            Radius += GrowthRate;
            Alpha -= 15;
            if (Alpha < 0) Alpha = 0;
        }
    }

    // ---------- High Score Persistence ----------
    class HighScoreManager
    {
        private string filePath = "highscore.txt";
        public int HighScore { get; private set; }

        public HighScoreManager()
        {
            Load();
        }

        public void AddScore(int score)
        {
            if (score > HighScore)
                HighScore = score;
        }

        public void Load()
        {
            if (File.Exists(filePath))
            {
                int.TryParse(File.ReadAllText(filePath), out int hs);
                HighScore = hs;
            }
        }

        public void Save()
        {
            File.WriteAllText(filePath, HighScore.ToString());
        }
    }
}