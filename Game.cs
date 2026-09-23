using System.Numerics;
using Raylib_cs;

namespace PipQuest;

public enum GameState { Title, Intro, Playing, Dying, Clear, GameOver, Victory }

public partial class Game
{
    public const int ScreenW = 960, ScreenH = 544;
    public const float Gravity = 2000f;
    public static readonly string[] StageNames = { "", "Overworld", "Caverns", "Skyways", "Fortress" };
    static readonly int[] ComboScores = { 100, 200, 400, 800, 1000, 2000, 4000, 8000 };

    public Level Level;
    public readonly Player Player = new();
    public readonly List<Entity> Entities = new();
    public readonly List<Particle> Particles = new();
    public readonly Random Rng = new();
    readonly List<Entity> pending = new();

    public float CamX, Time;
    public int Score, Gems, Lives = 3, World = 1, Stage = 1;
    public Boss ActiveBoss;

    GameState state = GameState.Title;
    float stateT, titleCam;
    bool paused, checkpointReached;
    Level titleLevel;
    double clock;

    public void Run()
    {
        Raylib.InitWindow(ScreenW, ScreenH, "Pip's Quest");
        Raylib.SetTargetFPS(60);
        titleLevel = LevelGenerator.Build(1, 1);

        while (!Raylib.WindowShouldClose()) // also true when Esc is pressed
        {
            float dt = MathF.Min(Raylib.GetFrameTime(), 1f / 30f); // clamp so physics never tunnels
            clock += dt;
            Update(dt);
            Raylib.BeginDrawing();
            Draw();
            Raylib.EndDrawing();
        }
        Raylib.CloseWindow();
    }

    void SetState(GameState s)
    {
        state = s;
        stateT = 0;
        paused = false;
    }

    // ------------------------------------------------------------------ flow

    void NewGame()
    {
        Score = 0; Gems = 0; Lives = 3; World = 1; Stage = 1;
        checkpointReached = false;
        Player.Power = PowerState.Small;
        SetState(GameState.Intro);
    }

    void NextLevel()
    {
        checkpointReached = false;
        Stage++;
        if (Stage > 4) { Stage = 1; World++; }
        if (World > 8)
        {
            World = 8; Stage = 4;
            Particles.Clear();
            SetState(GameState.Victory);
            return;
        }
        SetState(GameState.Intro);
    }

    void LoadLevel()
    {
        Level = LevelGenerator.Build(World, Stage);
        Entities.Clear();
        pending.Clear();
        Particles.Clear();
        ActiveBoss = null;

        foreach (var s in Level.Spawns)
        {
            var e = Create(s);
            if (e != null) Entities.Add(e);
        }

        bool useCheck = checkpointReached && Level.CheckCol >= 0;
        int col = useCheck ? Level.CheckCol : Level.StartCol;
        int row = useCheck ? Level.CheckRow : Level.StartRow;
        Player.Spawn(col * Level.T + Level.T * 0.5f, (row + 1) * Level.T);
        if (useCheck)
            foreach (var e in Entities)
                if (e is Checkpoint cp) cp.Lit = true;

        Time = Level.Theme == Theme.Castle ? 400 : 300;
        CamX = Math.Clamp(Player.Box.CenterX - ScreenW * 0.42f, 0f, (float)(Level.PixelWidth - ScreenW));
    }

    Entity Create(Spawn s)
    {
        const int T = PipQuest.Level.T;
        float cx = s.Col * T + T * 0.5f;
        float cy = s.Row * T + T * 0.5f;
        float bottom = (s.Row + 1) * T;
        return s.Kind switch
        {
            SpawnKind.Gem => new GemPickup(cx, cy),
            SpawnKind.Blob => new Walker(cx, bottom, false, World),
            SpawnKind.Spiky => new Walker(cx, bottom, true, World),
            SpawnKind.Hopper => new Hopper(cx, bottom),
            SpawnKind.Bat => new Bat(cx, cy),
            SpawnKind.PlatformH => new MovingPlatform(s.Col * T, s.Row * T, true, s.P1, s.P2),
            SpawnKind.PlatformV => new MovingPlatform(s.Col * T, s.Row * T, false, s.P1, s.P2),
            SpawnKind.FireBar => new FireBar(cx, cy, (int)s.P1, s.P2),
            SpawnKind.Boss => new Boss(cx, bottom, s.P1, s.P2, World),
            SpawnKind.Goal => new GoalPortal(cx, bottom),
            SpawnKind.Checkpoint => new Checkpoint(cx, bottom),
            _ => null,
        };
    }

    // ------------------------------------------------------------------ update

    void Update(float dt)
    {
        stateT += dt;
        switch (state)
        {
            case GameState.Title:
                titleCam += 70f * dt;
                if (titleCam > titleLevel.PixelWidth - ScreenW) titleCam = 0;
                if (Input.Confirm) NewGame();
                break;

            case GameState.Intro:
                if (stateT > 2.2f || (stateT > 0.4f && Input.Confirm))
                {
                    LoadLevel();
                    SetState(GameState.Playing);
                }
                break;

            case GameState.Playing:
                if (Input.Pause) paused = !paused;
                if (!paused) UpdatePlaying(dt);
                break;

            case GameState.Dying:
                Player.Vel.Y += 1600f * dt;
                Player.Box.Y += Player.Vel.Y * dt;
                UpdateParticles(dt);
                if (stateT > 2.6f)
                {
                    Lives--;
                    Player.SetPower(PowerState.Small);
                    SetState(Lives <= 0 ? GameState.GameOver : GameState.Intro);
                }
                break;

            case GameState.Clear:
                UpdateParticles(dt);
                if (stateT > 0.8f && Time > 0)
                {
                    int n = Math.Min((int)MathF.Ceiling(Time), 4);
                    Time = MathF.Max(0, Time - n);
                    Score += n * 50;
                }
                if (stateT > 3f && Time <= 0) NextLevel();
                break;

            case GameState.GameOver:
                if (stateT > 1f && Input.Confirm)
                {
                    // Continue: restart the current world with a fresh score.
                    Lives = 3; Score = 0; Gems = 0; Stage = 1;
                    checkpointReached = false;
                    Player.SetPower(PowerState.Small);
                    SetState(GameState.Intro);
                }
                break;

            case GameState.Victory:
                if (Rng.NextDouble() < dt * 3) Firework(Rng.Next(100, ScreenW - 100), Rng.Next(60, 300));
                UpdateParticles(dt);
                if (stateT > 2f && Input.Confirm) SetState(GameState.Title);
                break;
        }
    }

    void UpdatePlaying(float dt)
    {
        Time -= dt * 2.5f;
        if (Time <= 0) { Time = 0; KillPlayer(); return; }

        Level.Update(dt);

        // Platforms move first so the player can be carried by them this frame.
        foreach (var e in Entities)
            if (e is MovingPlatform) e.Update(this, dt);

        Player.Update(this, dt);
        if (state != GameState.Playing) return;

        for (int i = 0; i < Entities.Count; i++)
        {
            var e = Entities[i];
            if (!e.Dead && e is not MovingPlatform) e.Update(this, dt);
        }
        FlushPending();

        Collisions();
        EnemyBumps();
        FlushPending();
        Entities.RemoveAll(e => e.Dead);

        UpdateParticles(dt);
        UpdateCamera(dt);
    }

    void UpdateCamera(float dt)
    {
        float target = Player.Box.CenterX - ScreenW * 0.42f;
        CamX += (target - CamX) * MathF.Min(1f, dt * 8f);
        CamX = Math.Clamp(CamX, 0f, (float)(Level.PixelWidth - ScreenW));
    }

    void UpdateParticles(float dt)
    {
        foreach (var p in Particles) p.Update(dt);
        Particles.RemoveAll(p => p.Life <= 0);
    }

    public void Spawn(Entity e) => pending.Add(e);

    void FlushPending()
    {
        if (pending.Count == 0) return;
        Entities.AddRange(pending);
        pending.Clear();
    }

    public int CountPlayerFireballs()
    {
        int n = 0;
        foreach (var e in Entities) if (e is Fireball && !e.Dead) n++;
        foreach (var e in pending) if (e is Fireball) n++;
        return n;
    }

    // ------------------------------------------------------------------ collisions

    void Collisions()
    {
        var p = Player;
        bool falling = p.Vel.Y > 0;

        for (int i = 0; i < Entities.Count; i++)
        {
            if (state != GameState.Playing) return;
            var e = Entities[i];
            if (e.Dead) continue;

            switch (e)
            {
                case Enemy en:
                    if (!en.Awake || !p.Box.Intersects(en.Box)) break;
                    if (en.Stompable && falling && p.PrevBottom <= en.Box.Y + 14)
                    {
                        en.OnStomp(this);
                        AwardStomp(en.Box.CenterX, en.Box.Y);
                        p.Bounce();
                    }
                    else HurtPlayer();
                    break;

                case Boss boss:
                    if (!boss.Awake || !p.Box.Intersects(boss.Box)) break;
                    if (falling && p.PrevBottom <= boss.Box.Y + 18)
                    {
                        if (boss.Damage(this, 4)) AddScore(1000, boss.Box.CenterX, boss.Box.Y);
                        p.Bounce();
                        p.Vel.X = p.Box.CenterX < boss.Box.CenterX ? -220f : 220f;
                    }
                    else if (!boss.Stunned) HurtPlayer();
                    break;

                case Rock rock:
                    if (p.Box.Intersects(rock.Box)) { rock.Dead = true; HurtPlayer(); }
                    break;

                case FireBar bar:
                    if (bar.Hits(p.Box)) HurtPlayer();
                    break;

                case GemPickup gem:
                    if (p.Box.Intersects(gem.Box))
                    {
                        gem.Dead = true;
                        CollectGem(gem.Box.CenterX, gem.Box.CenterY, true);
                    }
                    break;

                case PowerItem item:
                    if (item.CanCollect && p.Box.Intersects(item.Box)) Collect(item);
                    break;

                case Checkpoint cp:
                    if (!cp.Lit && p.Box.Intersects(cp.Box))
                    {
                        cp.Lit = true;
                        checkpointReached = true;
                        Popup("CHECKPOINT", cp.Box.X - 40, cp.Box.Y - 24);
                    }
                    break;

                case GoalPortal goal:
                    if (p.Box.Intersects(goal.Box)) { StageClear(goal); return; }
                    break;

                case Fireball fb:
                    FireballHits(fb);
                    break;
            }
        }
    }

    void FireballHits(Fireball fb)
    {
        foreach (var e in Entities)
        {
            if (e.Dead || e == fb || !fb.Box.Intersects(e.Box)) continue;
            if (e is Enemy en && en.Awake)
            {
                en.OnKnock(this);
                AddScore(200, en.Box.CenterX, en.Box.Y);
            }
            else if (e is Boss b && b.Awake) b.Damage(this, 1);
            else if (e is Rock r) r.Dead = true;
            else continue;

            fb.Dead = true;
            Puff(fb.Box.CenterX, fb.Box.CenterY, 255, 160, 40);
            return;
        }
    }

    /// <summary>Walkers that bump into each other turn around.</summary>
    void EnemyBumps()
    {
        for (int i = 0; i < Entities.Count; i++)
        {
            if (Entities[i] is not Walker a || !a.Awake || a.Dead) continue;
            for (int j = i + 1; j < Entities.Count; j++)
            {
                if (Entities[j] is not Walker b || !b.Awake || b.Dead) continue;
                if (!a.Box.Intersects(b.Box)) continue;
                bool aLeft = a.Box.CenterX < b.Box.CenterX;
                a.Dir = aLeft ? -1 : 1;
                b.Dir = aLeft ? 1 : -1;
            }
        }
    }

    // ------------------------------------------------------------------ events

    public void HitBlock(int c, int r)
    {
        var tile = Level.Get(c, r);
        if (tile != Tile.Brick && tile != Tile.Mystery) return;

        Level.Bump(c, r);

        // Anything standing on the punched block gets knocked.
        var zone = new RectF(c * Level.T, r * Level.T - 8, Level.T, 10);
        foreach (var e in Entities)
        {
            if (e.Dead || !e.Box.Intersects(zone)) continue;
            if (e is Enemy en && en.Awake)
            {
                en.OnKnock(this);
                AddScore(200, en.Box.CenterX, en.Box.Y);
            }
            else if (e is PowerItem pi) pi.Hop();
            else if (e is GemPickup gp)
            {
                gp.Dead = true;
                CollectGem(gp.Box.CenterX, gp.Box.CenterY, true);
            }
        }

        bool hasContent = Level.Contents.TryGetValue((c, r), out var kind);
        if (tile == Tile.Mystery || hasContent)
        {
            if (!hasContent) kind = ItemKind.Gem;
            Level.Contents.Remove((c, r));
            Level.Set(c, r, Tile.Used);
            switch (kind)
            {
                case ItemKind.Gem:
                    Spawn(new PopGem(c * Level.T + Level.T * 0.5f, r * Level.T - 22));
                    CollectGem(c * Level.T + 16, r * Level.T - 20, false);
                    break;
                case ItemKind.Power:
                    Spawn(new PowerItem(Player.IsBig ? PowerKind.Ember : PowerKind.Berry, c, r));
                    break;
                case ItemKind.Heart:
                    Spawn(new PowerItem(PowerKind.Heart, c, r));
                    break;
            }
        }
        else if (tile == Tile.Brick && Player.IsBig)
        {
            Level.Set(c, r, Tile.Empty);
            Debris(c * Level.T + 16, r * Level.T + 16, Level.BrickRgb);
            Score += 50;
        }
    }

    void Collect(PowerItem item)
    {
        item.Dead = true;
        switch (item.Kind)
        {
            case PowerKind.Berry:
                if (!Player.IsBig) { Player.SetPower(PowerState.Big); Player.GrowFlash = 0.6f; }
                AddScore(1000, item.Box.CenterX, item.Box.Y);
                break;
            case PowerKind.Ember:
                if (Player.Power != PowerState.Ember) { Player.SetPower(PowerState.Ember); Player.GrowFlash = 0.6f; }
                AddScore(1000, item.Box.CenterX, item.Box.Y);
                break;
            case PowerKind.Heart:
                Lives++;
                Popup("1UP", item.Box.CenterX - 10, item.Box.Y - 16);
                break;
        }
    }

    void CollectGem(float x, float y, bool sparkle)
    {
        Gems++;
        Score += 200;
        if (sparkle) Sparkle(x, y);
        if (Gems >= 100)
        {
            Gems -= 100;
            Lives++;
            Popup("1UP", x - 10, y - 24);
        }
    }

    void AwardStomp(float x, float y)
    {
        // Chained stomps without touching the ground are worth more.
        if (Player.Combo < ComboScores.Length) AddScore(ComboScores[Player.Combo], x, y);
        else { Lives++; Popup("1UP", x - 10, y - 16); }
        Player.Combo++;
    }

    void HurtPlayer()
    {
        if (state != GameState.Playing || Player.Dead || Player.Invuln > 0) return;
        if (Player.Power == PowerState.Small) { KillPlayer(); return; }
        Player.SetPower(Player.Power == PowerState.Ember ? PowerState.Big : PowerState.Small);
        Player.Invuln = 2f;
    }

    public void KillPlayer()
    {
        if (state != GameState.Playing || Player.Dead) return;
        Player.Dead = true;
        Player.Vel = new Vector2(0, -560f);
        SetState(GameState.Dying);
    }

    void StageClear(GoalPortal goal)
    {
        SetState(GameState.Clear);
        Player.Vel = Vector2.Zero;
        for (int i = 0; i < 30; i++)
        {
            float a = (float)(Rng.NextDouble() * Math.PI * 2);
            float s = 60 + (float)Rng.NextDouble() * 160;
            Particles.Add(new Particle
            {
                X = goal.Box.CenterX, Y = goal.Box.CenterY,
                VX = MathF.Cos(a) * s, VY = MathF.Sin(a) * s,
                Life = 1f, MaxLife = 1f, Size = 5, R = 210, G = 180, B = 255,
            });
        }
    }

    public void OnBossAwake(Boss b) => ActiveBoss = b;

    public void OnBossDefeated(Boss b)
    {
        Level.OpenGates();
        foreach (var e in Entities) if (e is Rock) e.Dead = true;
        for (int i = 0; i < 5; i++)
            Debris(b.Box.X + Rng.Next(64), b.Box.Y + Rng.Next(72), (112, 102, 96));
        AddScore(5000, b.Box.CenterX, b.Box.Y);
        Popup("THE GATE OPENS!", b.Box.X - 40, b.Box.Y - 44);
        ActiveBoss = null;
    }

    // ------------------------------------------------------------------ effects

    public void AddScore(int pts, float x, float y)
    {
        Score += pts;
        Popup(pts.ToString(), x - 10, y - 16);
    }

    public void Popup(string text, float x, float y) => Particles.Add(new Particle
    {
        X = x, Y = y, VY = -50f, Life = 0.9f, MaxLife = 0.9f, Size = 16, R = 255, G = 255, B = 255, Text = text,
    });

    public void Puff(float x, float y, int r, int g, int b)
    {
        for (int i = 0; i < 6; i++)
        {
            float a = (float)(Rng.NextDouble() * Math.PI * 2);
            float s = 40 + (float)Rng.NextDouble() * 80;
            Particles.Add(new Particle
            {
                X = x, Y = y, VX = MathF.Cos(a) * s, VY = MathF.Sin(a) * s,
                Life = 0.4f, MaxLife = 0.4f, Size = 5, R = r, G = g, B = b,
            });
        }
    }

    public void Sparkle(float x, float y) => Puff(x, y, 180, 240, 255);

    public void Debris(float x, float y, (int r, int g, int b) c)
    {
        for (int i = 0; i < 4; i++)
        {
            float vx = (i % 2 == 0 ? -1 : 1) * (60 + (float)Rng.NextDouble() * 100);
            float vy = i < 2 ? -520f : -340f;
            Particles.Add(new Particle
            {
                X = x + (i % 2) * 12 - 6, Y = y + (i / 2) * 12 - 6, VX = vx, VY = vy, Grav = 1500f,
                Life = 1.2f, MaxLife = 1.2f, Size = 10, R = c.r, G = c.g, B = c.b,
            });
        }
    }

    void Firework(float x, float y)
    {
        int r = Rng.Next(150, 256), g = Rng.Next(100, 256), b = Rng.Next(100, 256);
        for (int i = 0; i < 24; i++)
        {
            float a = i / 24f * MathF.Tau;
            float s = 120 + (float)Rng.NextDouble() * 60;
            Particles.Add(new Particle
            {
                X = x, Y = y, VX = MathF.Cos(a) * s, VY = MathF.Sin(a) * s, Grav = 200f,
                Life = 1.4f, MaxLife = 1.4f, Size = 4, R = r, G = g, B = b,
            });
        }
    }
}
