namespace PipQuest;

/// <summary>
/// The Stone Warden guards the end of every fortress. Stomp it (big damage, stuns it) or pelt it
/// with embers (chip damage). It chases, jumps, and lobs rocks; it gets faster as it loses health.
/// Defeating it opens the gate to the portal.
/// </summary>
public class Boss : Entity
{
    public int Hp, MaxHp;
    public bool Awake;
    public bool Stunned => stunT > 0;

    readonly float left, right;
    readonly int world;
    float throwT = 1.5f, jumpT = 3f, stunT, flashT, t;
    int dir = -1;
    bool onGround;

    public Boss(float cx, float bottom, float left, float right, int world)
    {
        Box = new RectF(cx - 32, bottom - 72, 64, 72);
        this.left = left;
        this.right = right;
        this.world = world;
        MaxHp = Hp = 4 * (3 + (world - 1) / 2); // 3 stomps in world 1 .. 6 stomps in world 8
    }

    public override void Update(Game g, float dt)
    {
        t += dt;
        if (!Awake)
        {
            if (g.Player.Box.X > left + 48) { Awake = true; g.OnBossAwake(this); }
            else return;
        }

        Vel.Y = MathF.Min(Vel.Y + Game.Gravity * dt, 800f);
        if (flashT > 0) flashT -= dt;

        if (stunT > 0)
        {
            stunT -= dt;
            Vel.X = 0;
        }
        else
        {
            float rage = 1f - (float)Hp / MaxHp;
            float speed = 70f + rage * 90f + world * 5f;
            float px = g.Player.Box.CenterX;
            if (MathF.Abs(px - Box.CenterX) > 30) dir = px < Box.CenterX ? -1 : 1;
            Vel.X = dir * speed;

            throwT -= dt;
            if (throwT <= 0)
            {
                const float flight = 0.9f;
                float vx = Math.Clamp((px - Box.CenterX) / flight, -380f, 380f);
                g.Spawn(new Rock(Box.CenterX, Box.Y + 6, vx, -480f));
                throwT = MathF.Max(0.7f, 1.9f - world * 0.12f - rage * 0.5f) + (float)g.Rng.NextDouble() * 0.6f;
            }

            jumpT -= dt;
            if (jumpT <= 0 && onGround)
            {
                Vel.Y = -580f;
                jumpT = 2.5f + (float)g.Rng.NextDouble() * 2f;
            }
        }

        var r = Physics.Move(g.Level, ref Box, Vel.X * dt, Vel.Y * dt);
        onGround = r.HitBottom;
        if (r.HitBottom || r.HitTop) Vel.Y = 0;
        if (Box.X < left) Box.X = left;
        if (Box.Right > right) Box.X = right - Box.W;
    }

    /// <summary>Returns true if the hit landed. amount 4 = stomp, 1 = ember.</summary>
    public bool Damage(Game g, int amount)
    {
        if (Dead || stunT > 0 || (amount < 4 && flashT > 0)) return false;
        Hp -= amount;
        if (amount >= 4) stunT = 0.9f; else flashT = 0.1f;
        if (Hp <= 0)
        {
            Dead = true;
            g.OnBossDefeated(this);
        }
        return true;
    }

    public override void Draw(Game g, double time)
    {
        float x = Box.X, y = Box.Y, cx = Box.CenterX;
        bool flash = flashT > 0 || (stunT > 0 && ((int)(stunT * 16f) % 2 == 0));
        var body = flash ? Gfx.C(255, 255, 255) : Gfx.C(112, 102, 96);
        var dark = Gfx.C(70, 62, 58);

        // legs
        float step = onGround && !Stunned ? MathF.Sin(t * 8f) * 3f : 0;
        Gfx.Rect(x + 8, y + 58 + step, 16, 14 - step, dark);
        Gfx.Rect(x + 40, y + 58 - step, 16, 14 + step, dark);

        // torso + head
        Gfx.Rect(x + 4, y + 20, 56, 42, body);
        Gfx.Rect(x + 12, y, 40, 24, body);

        // cracks
        Gfx.Rect(x + 16, y + 30, 3, 14, dark);
        Gfx.Rect(x + 19, y + 42, 10, 3, dark);
        Gfx.Rect(x + 44, y + 26, 3, 10, dark);

        // arms
        float swing = MathF.Sin(t * 6f) * 4f;
        Gfx.Rect(x - 8, y + 24 + swing, 12, 30, body);
        Gfx.Rect(x + 60, y + 24 - swing, 12, 30, body);

        // single glowing eye
        float glow = 0.7f + 0.3f * MathF.Sin(t * 5f);
        float ex = cx + dir * 6;
        Gfx.Circle(ex, y + 12, 11 * glow, Gfx.C(255, 120, 40, 70));
        Gfx.Circle(ex, y + 12, 6, Gfx.C(255, 140, 40));
        Gfx.Circle(ex, y + 12, 3, Gfx.C(255, 240, 180));

        // moss and crystals on its head
        Gfx.Rect(x + 14, y - 4, 6, 6, Gfx.C(90, 170, 90));
        Gfx.Rect(x + 22, y - 2, 4, 4, Gfx.C(90, 170, 90));
        Gfx.Rect(x + 40, y - 6, 5, 8, Gfx.C(160, 110, 230));
    }
}
