using System.Numerics;
using Raylib_cs;

namespace PipQuest;

public enum PowerState { Small, Big, Ember }

/// <summary>Pip: a small explorer in a teal hoodie, yellow goggles and a red scarf (original character).</summary>
public class Player
{
    public RectF Box;
    public Vector2 Vel;
    public PowerState Power = PowerState.Small;
    public bool OnGround, Dead;
    public int Facing = 1;
    public float Invuln, PrevBottom, Anim, GrowFlash;
    public int Combo;
    public MovingPlatform Riding;

    float coyote, jumpBuffer, fireCooldown, throwAnim;
    bool jumping;

    const float SmallW = 20, SmallH = 26, BigW = 22, BigH = 46;

    // Tuning. With these values a standing jump peaks at ~4.5 tiles and a running jump clears ~8 tiles on flat ground.
    const float WalkSpeed = 180f, RunSpeed = 300f;
    const float GroundAccel = 1300f, AirAccel = 850f;
    const float GroundFriction = 1400f, AirDrag = 250f;
    const float JumpSpeed = 610f, JumpRunBonus = 0.12f;
    const float RiseGravity = 1300f, FallGravity = 2500f, MaxFall = 760f;

    public bool IsBig => Power != PowerState.Small;

    public void Spawn(float centerX, float bottom)
    {
        Box.W = IsBig ? BigW : SmallW;
        Box.H = IsBig ? BigH : SmallH;
        Box.X = centerX - Box.W * 0.5f;
        Box.Y = bottom - Box.H;
        Vel = Vector2.Zero;
        Dead = false; OnGround = false; jumping = false; Riding = null;
        Invuln = 0; GrowFlash = 0; Combo = 0; coyote = 0; jumpBuffer = 0; Facing = 1;
    }

    public void SetPower(PowerState p)
    {
        float bottom = Box.Bottom, cx = Box.CenterX;
        Power = p;
        Box.W = IsBig ? BigW : SmallW;
        Box.H = IsBig ? BigH : SmallH;
        Box.X = cx - Box.W * 0.5f;
        Box.Y = bottom - Box.H;
    }

    public void Bounce()
    {
        Vel.Y = Input.JumpHeld ? -600f : -380f;
        jumping = true;
        OnGround = false;
        Riding = null;
    }

    public void Update(Game g, float dt)
    {
        var lvl = g.Level;

        // Get carried by a moving platform we were standing on last frame.
        if (Riding != null && !Riding.Dead) Physics.Move(lvl, ref Box, Riding.Dx, Riding.Dy);
        PrevBottom = Box.Bottom;

        // ---- horizontal
        int dir = (Input.Right ? 1 : 0) - (Input.Left ? 1 : 0);
        float max = Input.RunHeld ? RunSpeed : WalkSpeed;
        if (dir != 0)
        {
            Facing = dir;
            float a = OnGround ? GroundAccel : AirAccel;
            if (MathF.Sign(Vel.X) == -dir && OnGround) a *= 2.2f; // quick turnaround
            float before = MathF.Abs(Vel.X);
            Vel.X += dir * a * dt;
            if (MathF.Abs(Vel.X) > max)
                Vel.X = MathF.Sign(Vel.X) * (before > max ? MathF.Max(max, before - 700f * dt) : max);
        }
        else
        {
            Vel.X = Util.Approach(Vel.X, 0, (OnGround ? GroundFriction : AirDrag) * dt);
        }

        // ---- jumping (with coyote time and input buffering)
        if (Input.JumpPressed) jumpBuffer = 0.12f; else jumpBuffer -= dt;
        if (OnGround) coyote = 0.1f; else coyote -= dt;
        if (jumpBuffer > 0 && coyote > 0)
        {
            Vel.Y = -(JumpSpeed + MathF.Abs(Vel.X) * JumpRunBonus);
            jumpBuffer = 0; coyote = 0;
            jumping = true; OnGround = false; Riding = null;
        }
        // Holding jump while rising = lighter gravity = higher jump.
        float grav = (Vel.Y < 0 && jumping && Input.JumpHeld) ? RiseGravity : FallGravity;
        Vel.Y = MathF.Min(Vel.Y + grav * dt, MaxFall);

        // ---- move + collide
        var res = Physics.Move(lvl, ref Box, Vel.X * dt, Vel.Y * dt);
        if (res.HitLeft || res.HitRight) Vel.X = 0;
        OnGround = false;
        if (res.HitBottom) { Vel.Y = 0; OnGround = true; jumping = false; Combo = 0; }
        if (res.HitTop)
        {
            Vel.Y = 40f;
            jumping = false;
            g.HitBlock(res.HeadCol, res.HeadRow);
        }

        // ---- land on moving platforms (one-way: only from above)
        Riding = null;
        if (Vel.Y >= 0)
        {
            foreach (var e in g.Entities)
            {
                if (e is not MovingPlatform p) continue;
                if (Box.Right > p.Box.X + 2 && Box.X < p.Box.Right - 2 &&
                    PrevBottom <= p.Box.Y + 6 && Box.Bottom >= p.Box.Y)
                {
                    Box.Y = p.Box.Y - Box.H;
                    Vel.Y = 0;
                    OnGround = true; jumping = false; Combo = 0;
                    Riding = p;
                    break;
                }
            }
        }

        // ---- throw embers
        fireCooldown -= dt;
        throwAnim -= dt;
        if (Power == PowerState.Ember && Input.FirePressed && fireCooldown <= 0 && g.CountPlayerFireballs() < 2)
        {
            g.Spawn(new Fireball(Facing > 0 ? Box.Right : Box.X - 12, Box.Y + 14, Facing));
            fireCooldown = 0.2f;
            throwAnim = 0.15f;
        }

        if (Invuln > 0) Invuln -= dt;
        if (GrowFlash > 0) GrowFlash -= dt;
        Anim += MathF.Abs(Vel.X) * dt * 0.06f;

        if (Box.Y > lvl.PixelHeight + 40 || lvl.TouchesLava(Box)) g.KillPlayer();
    }

    public void Draw(double time)
    {
        if (!Dead && Invuln > 0 && ((int)(Invuln * 20f) % 2 == 0)) return; // blink while invulnerable

        float x = Box.X, y = Box.Y, w = Box.W, h = Box.H;
        bool big = IsBig;
        bool ember = Power == PowerState.Ember;
        var hood = ember ? Gfx.C(250, 245, 235) : Gfx.C(40, 170, 160);
        var hoodDark = ember ? Gfx.C(255, 120, 40) : Gfx.C(25, 115, 110);
        if (GrowFlash > 0 && ((int)(GrowFlash * 20f) % 2 == 0)) hood = Gfx.C(255, 255, 170);
        var skin = Gfx.C(255, 214, 170);
        var boot = Gfx.C(80, 50, 30);
        var scarf = Gfx.C(230, 70, 60);
        var goggle = Gfx.C(250, 210, 60);
        var eyeC = Gfx.C(20, 20, 30);

        // legs
        float legH = big ? 12 : 7;
        float swing = 0;
        if (!OnGround && !Dead) swing = 2;
        else if (MathF.Abs(Vel.X) > 10) swing = MathF.Sin(Anim * MathF.Tau) * 3f;
        Gfx.Rect(x + 3 + swing, y + h - legH, 6, legH, boot);
        Gfx.Rect(x + w - 9 - swing, y + h - legH, 6, legH, boot);

        // body
        float headH = big ? 18 : 14;
        float bodyTop = y + headH - 2;
        float bodyBottom = y + h - legH + 1;
        Gfx.Rect(x + 1, bodyTop, w - 2, bodyBottom - bodyTop, hood);
        Gfx.Rect(x + 1, bodyBottom - 3, w - 2, 3, hoodDark);
        if (big) Gfx.Rect(x + 5, bodyTop + 12, w - 10, 3, hoodDark);

        // hood + face
        Gfx.Rect(x, y, w, headH, hood);
        Gfx.Rect(x + w * 0.5f - 2, y - 3, 4, 4, hoodDark); // little tuft on the hood
        float faceX = Facing > 0 ? x + w * 0.3f : x + 2;
        Gfx.Rect(faceX, y + 4, w * 0.68f - 2, headH - 5, skin);
        Gfx.Rect(x, y + 2, w, 3, goggle);

        // eyes
        float eyeY = y + (big ? 8 : 6);
        float e1 = Facing > 0 ? x + w - 6 : x + 3;
        float e2 = e1 - 6 * Facing;
        if (Dead)
        {
            Gfx.Rect(e1 - 1, eyeY + 1, 5, 2, eyeC);
            Gfx.Rect(e2 - 1, eyeY + 1, 5, 2, eyeC);
        }
        else
        {
            Gfx.Rect(e1, eyeY, 3, 4, eyeC);
            Gfx.Rect(e2, eyeY, 3, 4, eyeC);
        }

        // scarf, with a tail that streams out when running
        Gfx.Rect(x - 1, y + headH - 3, w + 2, 4, scarf);
        float tail = 4 + MathF.Min(10f, MathF.Abs(Vel.X) / 30f);
        float tx = Facing > 0 ? x - tail + 1 : x + w - 1;
        Gfx.Rect(tx, y + headH - 2 + MathF.Sin((float)time * 12f) * 1.5f, tail, 3, scarf);

        if (throwAnim > 0) Gfx.Rect(Facing > 0 ? x + w - 2 : x - 6, bodyTop + 4, 8, 4, hood);
    }
}
