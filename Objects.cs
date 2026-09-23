using System.Numerics;
using Raylib_cs;

namespace PipQuest;

public class GemPickup : Entity
{
    float t;

    public GemPickup(float cx, float cy)
    {
        Box = new RectF(cx - 8, cy - 11, 16, 22);
        t = cx * 0.01f;
    }

    public override void Update(Game g, float dt) => t += dt;
    public override void Draw(Game g, double time) => DrawGem(Box.CenterX, Box.CenterY, t);

    public static void DrawGem(float cx, float cy, float t)
    {
        float w = 2 + 8 * MathF.Abs(MathF.Cos(t * 3f)); // "spinning" by squashing the width
        Gfx.Tri(cx - w, cy, cx, cy - 11, cx + w, cy, Gfx.C(80, 220, 255));
        Gfx.Tri(cx - w, cy, cx + w, cy, cx, cy + 11, Gfx.C(40, 160, 220));
        Gfx.Rect(cx - 1, cy - 6, 2, 5, Gfx.C(225, 250, 255));
    }
}

/// <summary>Gem that pops out of a block and vanishes (already counted when spawned).</summary>
public class PopGem : Entity
{
    float t;

    public PopGem(float cx, float y)
    {
        Box = new RectF(cx - 8, y, 16, 22);
        Vel = new Vector2(0, -520f);
    }

    public override bool DrawBehind => t < 0.08f;

    public override void Update(Game g, float dt)
    {
        t += dt;
        Vel.Y += 1500f * dt;
        Box.Y += Vel.Y * dt;
        if (t > 0.55f)
        {
            Dead = true;
            g.Sparkle(Box.CenterX, Box.CenterY);
        }
    }

    public override void Draw(Game g, double time) => GemPickup.DrawGem(Box.CenterX, Box.CenterY, t * 4f);
}

public enum PowerKind { Berry, Ember, Heart }

/// <summary>Berry (grow), Ember orb (throw embers), Heart (extra life). Rises out of the block it was in.</summary>
public class PowerItem : Entity
{
    public readonly PowerKind Kind;
    float emerge = 0.6f, t;
    int dir = 1;
    readonly float targetY;

    public PowerItem(PowerKind kind, int col, int row)
    {
        Kind = kind;
        Box = new RectF(col * Level.T + 4, row * Level.T + 4, 24, 24);
        targetY = row * Level.T - 24;
    }

    public bool Emerging => emerge > 0;
    public bool CanCollect => emerge <= 0.25f;
    public override bool DrawBehind => Emerging;

    public override void Update(Game g, float dt)
    {
        t += dt;
        if (emerge > 0)
        {
            emerge -= dt;
            Box.Y = MathF.Max(targetY, Box.Y - 50f * dt);
            if (emerge <= 0) Box.Y = targetY;
            return;
        }
        if (Kind == PowerKind.Ember) return; // the orb stays put

        Vel.X = dir * (Kind == PowerKind.Heart ? 130f : 95f);
        Vel.Y = MathF.Min(Vel.Y + Game.Gravity * dt, 700f);
        var r = Physics.Move(g.Level, ref Box, Vel.X * dt, Vel.Y * dt);
        if (r.HitLeft) dir = 1;
        else if (r.HitRight) dir = -1;
        if (r.HitBottom || r.HitTop) Vel.Y = 0;
        if (Box.Y > g.Level.PixelHeight + 50 || g.Level.TouchesLava(Box)) Dead = true;
    }

    /// <summary>Called when the block underneath is punched.</summary>
    public void Hop()
    {
        if (Emerging || Kind == PowerKind.Ember) return;
        Vel.Y = -320f;
        dir = -dir;
    }

    public override void Draw(Game g, double time)
    {
        float cx = Box.CenterX, cy = Box.CenterY;
        switch (Kind)
        {
            case PowerKind.Berry:
                Gfx.Circle(cx, cy + 2, 10, Gfx.C(210, 40, 90));
                Gfx.Circle(cx - 4, cy - 1, 3, Gfx.C(240, 120, 160));
                Gfx.Rect(cx + 3, cy + 4, 2, 2, Gfx.C(255, 210, 220));
                Gfx.Rect(cx - 3, cy + 7, 2, 2, Gfx.C(255, 210, 220));
                Gfx.Rect(cx + 5, cy - 2, 2, 2, Gfx.C(255, 210, 220));
                Gfx.Rect(cx - 1, cy - 12, 2, 5, Gfx.C(90, 60, 30));
                Gfx.Tri(cx, cy - 9, cx + 10, cy - 13, cx + 4, cy - 6, Gfx.C(70, 180, 70));
                break;

            case PowerKind.Ember:
            {
                float bob = Emerging ? 0 : MathF.Sin(t * 3f) * 2f;
                float f = 1 + 0.12f * MathF.Sin(t * 10f);
                cy += bob;
                Gfx.Circle(cx, cy, 14 * f, Gfx.C(255, 120, 30, 70));
                Gfx.Circle(cx, cy + 2, 9 * f, Gfx.C(255, 110, 30));
                Gfx.Tri(cx - 7, cy, cx, cy - 14 * f, cx + 7, cy, Gfx.C(255, 150, 40));
                Gfx.Circle(cx, cy + 3, 5, Gfx.C(255, 230, 120));
                break;
            }

            case PowerKind.Heart:
            {
                var hc = Gfx.C(255, 90, 140);
                Gfx.Circle(cx - 5, cy - 2, 6, hc);
                Gfx.Circle(cx + 5, cy - 2, 6, hc);
                Gfx.Tri(cx - 11, cy, cx, cy + 11, cx + 11, cy, hc);
                Gfx.Rect(cx - 6, cy - 5, 3, 3, Gfx.C(255, 205, 225));
                break;
            }
        }
    }
}

/// <summary>Bouncing ember thrown by the player.</summary>
public class Fireball : Entity
{
    float life = 2.5f, t;

    public Fireball(float x, float y, int dir)
    {
        Box = new RectF(x, y, 12, 12);
        Vel = new Vector2(dir * 440f, 180f);
    }

    public override void Update(Game g, float dt)
    {
        t += dt;
        life -= dt;
        Vel.Y = MathF.Min(Vel.Y + 1800f * dt, 600f);
        var r = Physics.Move(g.Level, ref Box, Vel.X * dt, Vel.Y * dt);
        if (r.HitBottom) Vel.Y = -300f;
        if (r.HitTop) Vel.Y = 50f;

        bool offscreen = Box.Right < g.CamX - 20 || Box.X > g.CamX + Game.ScreenW + 20 || Box.Y > g.Level.PixelHeight;
        if (offscreen) { Dead = true; return; }
        if (r.HitLeft || r.HitRight || life <= 0)
        {
            Dead = true;
            g.Puff(Box.CenterX, Box.CenterY, 255, 160, 40);
        }
    }

    public override void Draw(Game g, double time)
    {
        float cx = Box.CenterX, cy = Box.CenterY;
        Gfx.Circle(cx, cy, 9, Gfx.C(255, 120, 30, 90));
        Gfx.Circle(cx, cy, 6, Gfx.C(255, 130, 30));
        Gfx.Circle(cx + MathF.Cos(t * 20f) * 2f, cy + MathF.Sin(t * 20f) * 2f, 3, Gfx.C(255, 235, 150));
    }
}

/// <summary>Rock lobbed by the fortress guardian.</summary>
public class Rock : Entity
{
    public Rock(float cx, float cy, float vx, float vy)
    {
        Box = new RectF(cx - 9, cy - 9, 18, 18);
        Vel = new Vector2(vx, vy);
    }

    public override void Update(Game g, float dt)
    {
        Vel.Y += 1300f * dt;
        var r = Physics.Move(g.Level, ref Box, Vel.X * dt, Vel.Y * dt);
        if (r.Any)
        {
            Dead = true;
            g.Debris(Box.CenterX, Box.CenterY, (120, 110, 100));
        }
        if (Box.Y > g.Level.PixelHeight) Dead = true;
    }

    public override void Draw(Game g, double time)
    {
        Gfx.Circle(Box.CenterX, Box.CenterY, 9, Gfx.C(120, 110, 100));
        Gfx.Rect(Box.X + 4, Box.Y + 5, 4, 3, Gfx.C(80, 72, 66));
        Gfx.Rect(Box.X + 10, Box.Y + 10, 4, 3, Gfx.C(80, 72, 66));
    }
}

/// <summary>One-way platform that ping-pongs horizontally or vertically.</summary>
public class MovingPlatform : Entity
{
    readonly bool horizontal;
    readonly float min, max, speed;
    int dir;
    public float Dx, Dy;

    public MovingPlatform(float x, float y, bool horizontal, float end, float speed)
    {
        Box = new RectF(x, y, 96, 16);
        this.horizontal = horizontal;
        this.speed = speed;
        float start = horizontal ? x : y;
        min = MathF.Min(start, end);
        max = MathF.Max(start, end);
        dir = end >= start ? 1 : -1;
    }

    public override void Update(Game g, float dt)
    {
        float ox = Box.X, oy = Box.Y;
        if (horizontal)
        {
            Box.X += dir * speed * dt;
            if (Box.X >= max) { Box.X = max; dir = -1; }
            else if (Box.X <= min) { Box.X = min; dir = 1; }
        }
        else
        {
            Box.Y += dir * speed * dt;
            if (Box.Y >= max) { Box.Y = max; dir = -1; }
            else if (Box.Y <= min) { Box.Y = min; dir = 1; }
        }
        Dx = Box.X - ox;
        Dy = Box.Y - oy;
    }

    public override void Draw(Game g, double time)
    {
        bool metal = g.Level.Theme == Theme.Castle || g.Level.Theme == Theme.Underground;
        var top = metal ? Gfx.C(170, 170, 185) : Gfx.C(215, 170, 110);
        var body = metal ? Gfx.C(110, 110, 125) : Gfx.C(160, 110, 60);
        Gfx.Rect(Box.X, Box.Y, Box.W, Box.H, body);
        Gfx.Rect(Box.X, Box.Y, Box.W, 5, top);
        for (int i = 0; i < 3; i++) Gfx.Rect(Box.X + 14 + i * 32, Box.Y + 8, 4, 4, Gfx.C(60, 45, 30));
    }
}

/// <summary>A chain of fireballs rotating around a stone block.</summary>
public class FireBar : Entity
{
    readonly float cx, cy, speed;
    readonly int count;
    float angle;

    public FireBar(float cx, float cy, int count, float speed)
    {
        this.cx = cx; this.cy = cy; this.count = count; this.speed = speed;
        Box = new RectF(cx - count * 16, cy - count * 16, count * 32, count * 32);
        angle = cx % 7f;
    }

    public override void Update(Game g, float dt) => angle += speed * dt;

    public bool Hits(RectF p)
    {
        for (int i = 1; i < count; i++)
        {
            float bx = cx + MathF.Cos(angle) * i * 16f;
            float by = cy + MathF.Sin(angle) * i * 16f;
            float nx = Math.Clamp(bx, p.X, p.Right), ny = Math.Clamp(by, p.Y, p.Bottom);
            float dx = bx - nx, dy = by - ny;
            if (dx * dx + dy * dy < 36f) return true;
        }
        return false;
    }

    public override void Draw(Game g, double time)
    {
        for (int i = 0; i < count; i++)
        {
            float bx = cx + MathF.Cos(angle) * i * 16f;
            float by = cy + MathF.Sin(angle) * i * 16f;
            Gfx.Circle(bx, by, 8, Gfx.C(255, 110, 30));
            Gfx.Circle(bx, by, 4, Gfx.C(255, 230, 120));
        }
    }
}

/// <summary>End-of-stage portal inside a stone arch.</summary>
public class GoalPortal : Entity
{
    float t;

    public GoalPortal(float cx, float bottom)
    {
        Box = new RectF(cx - 18, bottom - 72, 36, 72);
    }

    public override void Update(Game g, float dt)
    {
        t += dt;
        if (g.Rng.NextDouble() < dt * 8)
        {
            g.Particles.Add(new Particle
            {
                X = Box.X + (float)g.Rng.NextDouble() * Box.W,
                Y = Box.Bottom - (float)g.Rng.NextDouble() * Box.H,
                VY = -40, Life = 0.8f, MaxLife = 0.8f, Size = 3, R = 210, G = 180, B = 255,
            });
        }
    }

    public override void Draw(Game g, double time)
    {
        float cx = Box.CenterX, cy = Box.CenterY;
        var stone = Gfx.C(150, 145, 160);
        var dark = Gfx.C(95, 90, 105);
        Gfx.Rect(Box.X - 12, Box.Y - 6, 10, Box.H + 6, stone);
        Gfx.Rect(Box.Right + 2, Box.Y - 6, 10, Box.H + 6, stone);
        Gfx.Rect(Box.X - 16, Box.Y - 16, Box.W + 32, 12, dark);
        float p = MathF.Sin(t * 3f);
        Gfx.Ellipse(cx, cy, 17 + p, 34, Gfx.C(120, 60, 230, 220));
        Gfx.Ellipse(cx, cy, 11 + p, 26, Gfx.C(170, 120, 255));
        Gfx.Ellipse(cx, cy, 5, 16 + p * 2, Gfx.C(235, 220, 255));
    }
}

/// <summary>Mid-stage lantern. Touch it and you respawn here after losing a life.</summary>
public class Checkpoint : Entity
{
    public bool Lit;
    float t;

    public Checkpoint(float cx, float bottom)
    {
        Box = new RectF(cx - 8, bottom - 56, 16, 56);
    }

    public override void Update(Game g, float dt) => t += dt;

    public override void Draw(Game g, double time)
    {
        float cx = Box.CenterX;
        Gfx.Rect(cx - 2, Box.Y + 12, 4, Box.H - 12, Gfx.C(90, 60, 35));
        Gfx.Rect(cx - 8, Box.Y, 16, 16, Gfx.C(60, 50, 45));
        if (Lit)
        {
            float f = 0.8f + 0.2f * MathF.Sin(t * 10f);
            Gfx.Circle(cx, Box.Y + 8, 22 * f, Gfx.C(255, 190, 80, 60));
            Gfx.Rect(cx - 5, Box.Y + 3, 10, 10, Gfx.C(255, 210, 90));
        }
        else
        {
            Gfx.Rect(cx - 5, Box.Y + 3, 10, 10, Gfx.C(120, 130, 140));
        }
    }
}
