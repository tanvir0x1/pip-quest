using System.Numerics;
using Raylib_cs;

namespace PipQuest;

public abstract class Entity
{
    public RectF Box;
    public Vector2 Vel;
    public bool Dead;
    public virtual bool DrawBehind => false; // drawn before tiles (e.g. items rising out of a block)
    public abstract void Update(Game g, float dt);
    public abstract void Draw(Game g, double time);
}

public abstract class Enemy : Entity
{
    public bool Stompable = true, Awake;
    public int Dir = -1;
    protected bool OnGround;
    protected float Age;

    public abstract (int r, int g, int b) Rgb { get; }

    public override void Update(Game g, float dt)
    {
        // Enemies sleep until they are about to scroll onto the screen.
        if (!Awake)
        {
            if (Box.X < g.CamX + Game.ScreenW + 64 && Box.Right > g.CamX - 64) Awake = true;
            else return;
        }
        Age += dt;
        Think(g, dt);
        if (Box.Y > g.Level.PixelHeight + 64 || g.Level.TouchesLava(Box) || Box.Right < g.CamX - 700) Dead = true;
    }

    protected abstract void Think(Game g, float dt);

    protected void ApplyGravity(float dt) => Vel.Y = MathF.Min(Vel.Y + Game.Gravity * dt, 700f);

    protected MoveResult MoveWithTiles(Game g, float dt)
    {
        var r = Physics.Move(g.Level, ref Box, Vel.X * dt, Vel.Y * dt);
        OnGround = r.HitBottom;
        if (r.HitBottom || r.HitTop) Vel.Y = 0;
        return r;
    }

    public virtual void OnStomp(Game g)
    {
        Dead = true;
        g.Spawn(new Corpse(Box, Rgb, true));
    }

    public virtual void OnKnock(Game g)
    {
        Dead = true;
        g.Spawn(new Corpse(Box, Rgb, false));
    }
}

/// <summary>Blob (stompable, walks off ledges) or Spiky (not stompable, turns at ledges).</summary>
public class Walker : Enemy
{
    readonly bool spiky;
    readonly float speed;

    public Walker(float cx, float bottom, bool spiky, int world)
    {
        this.spiky = spiky;
        Stompable = !spiky;
        Box = new RectF(cx - 13, bottom - 24, 26, 24);
        speed = (spiky ? 55f : 65f) * (1f + (world - 1) * 0.05f);
    }

    public override (int r, int g, int b) Rgb => spiky ? (125, 115, 105) : (150, 80, 200);

    protected override void Think(Game g, float dt)
    {
        ApplyGravity(dt);
        Vel.X = Dir * speed;
        var r = MoveWithTiles(g, dt);
        if (r.HitLeft) Dir = 1;
        else if (r.HitRight) Dir = -1;

        if (spiky && OnGround)
        {
            float fx = Dir > 0 ? Box.Right + 1 : Box.X - 1;
            int c = (int)MathF.Floor(fx / Level.T);
            int row = (int)MathF.Floor((Box.Bottom + 2) / Level.T);
            if (!g.Level.IsSolid(c, row)) Dir = -Dir;
        }
    }

    public override void Draw(Game g, double time)
    {
        float x = Box.X, y = Box.Y, w = Box.W, h = Box.H, cx = Box.CenterX;
        float step = MathF.Sin(Age * 12f) * 2f;

        if (!spiky)
        {
            var body = Gfx.C(150, 80, 200);
            var dark = Gfx.C(100, 50, 140);
            Gfx.Rect(x + 3 + step, y + h - 5, 8, 5, dark);
            Gfx.Rect(x + w - 11 - step, y + h - 5, 8, 5, dark);
            Gfx.Ellipse(cx, y + 13, 13, 12, body);
            Gfx.Rect(x, y + 12, w, h - 16, body);
            float look = Dir * 2;
            Gfx.Rect(cx - 8, y + 7, 6, 8, Gfx.White);
            Gfx.Rect(cx + 2, y + 7, 6, 8, Gfx.White);
            Gfx.Rect(cx - 6 + look, y + 10, 3, 4, Gfx.Black);
            Gfx.Rect(cx + 4 + look, y + 10, 3, 4, Gfx.Black);
        }
        else
        {
            var body = Gfx.C(125, 115, 105);
            var dark = Gfx.C(80, 72, 66);
            var spike = Gfx.C(225, 225, 235);
            for (int i = 0; i < 4; i++)
            {
                float sx = x + 1 + i * 6.5f;
                Gfx.Tri(sx, y + 10, sx + 3.5f, y - 3, sx + 7, y + 10, spike);
            }
            Gfx.Rect(x + 3 + step, y + h - 4, 7, 4, dark);
            Gfx.Rect(x + w - 10 - step, y + h - 4, 7, 4, dark);
            Gfx.Ellipse(cx, y + 14, 13, 9, body);
            Gfx.Rect(cx + Dir * 5 - 2, y + 11, 4, 4, Gfx.C(255, 60, 60));
        }
    }
}

/// <summary>Frog-like enemy that hops toward the player.</summary>
public class Hopper : Enemy
{
    float wait = 1f;

    public Hopper(float cx, float bottom)
    {
        Box = new RectF(cx - 13, bottom - 22, 26, 22);
    }

    public override (int r, int g, int b) Rgb => (80, 190, 90);

    protected override void Think(Game g, float dt)
    {
        ApplyGravity(dt);
        if (OnGround)
        {
            Vel.X = 0;
            wait -= dt;
            if (wait <= 0)
            {
                Dir = g.Player.Box.CenterX < Box.CenterX ? -1 : 1;
                Vel.Y = -480f;
                Vel.X = Dir * 110f;
                wait = 0.9f + (float)g.Rng.NextDouble() * 0.8f;
            }
        }
        var r = MoveWithTiles(g, dt);
        if (r.HitLeft || r.HitRight) Vel.X = -Vel.X * 0.5f;
    }

    public override void Draw(Game g, double time)
    {
        float x = Box.X, y = Box.Y, w = Box.W, h = Box.H, cx = Box.CenterX;
        var body = Gfx.C(80, 190, 90);
        var belly = Gfx.C(190, 235, 150);
        var dark = Gfx.C(45, 120, 55);
        if (!OnGround)
        {
            Gfx.Rect(x + 2, y + h - 6, 6, 8, dark);
            Gfx.Rect(x + w - 8, y + h - 6, 6, 8, dark);
        }
        else
        {
            Gfx.Rect(x, y + h - 5, 9, 5, dark);
            Gfx.Rect(x + w - 9, y + h - 5, 9, 5, dark);
        }
        Gfx.Ellipse(cx, y + 14, 13, 9, body);
        Gfx.Ellipse(cx, y + 17, 8, 5, belly);
        Gfx.Circle(cx - 6, y + 5, 5, body);
        Gfx.Circle(cx + 6, y + 5, 5, body);
        Gfx.Circle(cx - 6, y + 5, 3, Gfx.White);
        Gfx.Circle(cx + 6, y + 5, 3, Gfx.White);
        Gfx.Rect(cx - 6 + Dir, y + 4, 2, 3, Gfx.Black);
        Gfx.Rect(cx + 6 + Dir, y + 4, 2, 3, Gfx.Black);
    }
}

/// <summary>Flying enemy that bobs in a wave and drifts toward the player. Ignores tiles.</summary>
public class Bat : Enemy
{
    readonly float baseY;

    public Bat(float cx, float cy)
    {
        Box = new RectF(cx - 14, cy - 10, 28, 20);
        baseY = cy - 10;
    }

    public override (int r, int g, int b) Rgb => (70, 40, 90);

    protected override void Think(Game g, float dt)
    {
        float px = g.Player.Box.CenterX;
        if (MathF.Abs(px - Box.CenterX) > 120) Dir = px < Box.CenterX ? -1 : 1;
        Box.X += Dir * 75f * dt;
        Box.Y = baseY + MathF.Sin(Age * 2.6f) * 36f;
    }

    public override void Draw(Game g, double time)
    {
        float cx = Box.CenterX, cy = Box.CenterY;
        var body = Gfx.C(70, 40, 90);
        var wing = Gfx.C(100, 60, 125);
        float flap = MathF.Sin(Age * 18f) * 8f;
        Gfx.Tri(cx - 6, cy - 2, cx - 26, cy - 8 + flap, cx - 10, cy + 7, wing);
        Gfx.Tri(cx + 6, cy - 2, cx + 26, cy - 8 + flap, cx + 10, cy + 7, wing);
        Gfx.Circle(cx, cy, 9, body);
        Gfx.Tri(cx - 7, cy - 6, cx - 4, cy - 14, cx - 1, cy - 7, body);
        Gfx.Tri(cx + 1, cy - 7, cx + 4, cy - 14, cx + 7, cy - 6, body);
        Gfx.Rect(cx - 5, cy - 3, 3, 3, Gfx.C(255, 220, 80));
        Gfx.Rect(cx + 2, cy - 3, 3, 3, Gfx.C(255, 220, 80));
    }
}

/// <summary>A defeated enemy: either squashed flat briefly, or knocked upside-down off the screen.</summary>
public class Corpse : Entity
{
    readonly bool squash;
    readonly (int r, int g, int b) rgb;
    float life;

    public Corpse(RectF box, (int r, int g, int b) rgb, bool squash)
    {
        this.rgb = rgb;
        this.squash = squash;
        if (squash)
        {
            Box = new RectF(box.X, box.Bottom - 8, box.W, 8);
            life = 0.5f;
        }
        else
        {
            Box = box;
            Vel = new Vector2(60f, -380f);
            life = 2.5f;
        }
    }

    public override void Update(Game g, float dt)
    {
        life -= dt;
        if (!squash)
        {
            Vel.Y += 1600f * dt;
            Box.X += Vel.X * dt;
            Box.Y += Vel.Y * dt;
        }
        if (life <= 0) Dead = true;
    }

    public override void Draw(Game g, double time)
    {
        Gfx.Rect(Box.X, Box.Y, Box.W, Box.H, Level.Col(rgb));
        if (squash)
        {
            Gfx.Rect(Box.X + 6, Box.Y + 2, 5, 2, Gfx.Black);
            Gfx.Rect(Box.Right - 11, Box.Y + 2, 5, 2, Gfx.Black);
        }
        else
        {
            Gfx.Rect(Box.X + 5, Box.Bottom - 9, 5, 5, Gfx.White);
            Gfx.Rect(Box.Right - 10, Box.Bottom - 9, 5, 5, Gfx.White);
        }
    }
}
