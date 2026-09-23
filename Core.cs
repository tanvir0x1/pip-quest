using System.Numerics;
using Raylib_cs;

namespace PipQuest;

/// <summary>Axis-aligned rectangle in world pixels. Independent of Raylib's own Rectangle type.</summary>
public struct RectF
{
    public float X, Y, W, H;
    public RectF(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
    public float Right => X + W;
    public float Bottom => Y + H;
    public float CenterX => X + W * 0.5f;
    public float CenterY => Y + H * 0.5f;
    public bool Intersects(RectF o) => X < o.Right && Right > o.X && Y < o.Bottom && Bottom > o.Y;
}

public static class Util
{
    public static float Approach(float value, float target, float step)
    {
        if (value < target) return MathF.Min(value + step, target);
        if (value > target) return MathF.Max(value - step, target);
        return target;
    }
}

public struct MoveResult
{
    public bool HitLeft, HitRight, HitTop, HitBottom;
    public int HeadCol, HeadRow;
    public readonly bool Any => HitLeft || HitRight || HitTop || HitBottom;
}

/// <summary>Moves a box through the tile grid, resolving X then Y. Per-frame moves must stay under one tile (32px).</summary>
public static class Physics
{
    const int T = Level.T;
    static int Cell(float v) => (int)MathF.Floor(v / T);

    public static MoveResult Move(Level lvl, ref RectF b, float dx, float dy)
    {
        var res = new MoveResult { HeadCol = -1, HeadRow = -1 };

        if (dx != 0)
        {
            b.X += dx;
            int top = Cell(b.Y), bottom = Cell(b.Bottom - 0.01f);
            if (dx > 0)
            {
                int col = Cell(b.Right - 0.01f);
                for (int r = top; r <= bottom; r++)
                    if (lvl.IsSolid(col, r)) { b.X = col * T - b.W; res.HitRight = true; break; }
            }
            else
            {
                int col = Cell(b.X);
                for (int r = top; r <= bottom; r++)
                    if (lvl.IsSolid(col, r)) { b.X = (col + 1) * T; res.HitLeft = true; break; }
            }
        }

        if (dy != 0)
        {
            b.Y += dy;
            int left = Cell(b.X), right = Cell(b.Right - 0.01f);
            if (dy > 0)
            {
                int row = Cell(b.Bottom - 0.01f);
                for (int c = left; c <= right; c++)
                    if (lvl.IsSolid(c, row)) { b.Y = row * T - b.H; res.HitBottom = true; break; }
            }
            else
            {
                int row = Cell(b.Y);
                bool hit = false;
                float best = float.MaxValue;
                for (int c = left; c <= right; c++)
                {
                    if (!lvl.IsSolid(c, row)) continue;
                    hit = true;
                    // Remember the block closest to the centre of the head: that's the one we "punch".
                    float d = MathF.Abs(c * T + T * 0.5f - b.CenterX);
                    if (d < best) { best = d; res.HeadCol = c; }
                }
                if (hit) { b.Y = (row + 1) * T; res.HitTop = true; res.HeadRow = row; }
            }
        }
        return res;
    }
}

/// <summary>
/// Keyboard input. Uses raylib's raw key codes (the GLFW values from raylib.h) cast to KeyboardKey,
/// so the code does not depend on how a particular Raylib-cs version names the enum members.
/// </summary>
public static class Input
{
    const int KSpace = 32, KEnter = 257, KRight = 262, KLeft = 263, KUp = 265, KLeftShift = 340;
    const int KA = 65, KD = 68, KF = 70, KK = 75, KP = 80, KW = 87, KX = 88, KZ = 90;

    static bool Down(int k) => Raylib.IsKeyDown((KeyboardKey)k);
    static bool Hit(int k) => Raylib.IsKeyPressed((KeyboardKey)k);

    public static bool Left => Down(KLeft) || Down(KA);
    public static bool Right => Down(KRight) || Down(KD);
    public static bool JumpHeld => Down(KSpace) || Down(KZ) || Down(KUp) || Down(KW);
    public static bool JumpPressed => Hit(KSpace) || Hit(KZ) || Hit(KUp) || Hit(KW);
    public static bool RunHeld => Down(KLeftShift) || Down(KX) || Down(KK);
    public static bool FirePressed => Hit(KX) || Hit(KK) || Hit(KF);
    public static bool Confirm => Hit(KEnter);
    public static bool Pause => Hit(KP);
}

/// <summary>Drawing helpers. World-space calls subtract the integer camera offset Cam.</summary>
public static class Gfx
{
    public static int Cam;

    public static Color C(int r, int g, int b, int a = 255) => new Color(
        (byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255),
        (byte)Math.Clamp(b, 0, 255), (byte)Math.Clamp(a, 0, 255));

    public static readonly Color White = C(255, 255, 255);
    public static readonly Color Black = C(0, 0, 0);

    public static void Rect(float x, float y, float w, float h, Color c)
    {
        int x0 = (int)MathF.Floor(x) - Cam, y0 = (int)MathF.Floor(y);
        int x1 = (int)MathF.Floor(x + w) - Cam, y1 = (int)MathF.Floor(y + h);
        if (x1 > x0 && y1 > y0) Raylib.DrawRectangle(x0, y0, x1 - x0, y1 - y0, c);
    }

    public static void Circle(float x, float y, float r, Color c) =>
        Raylib.DrawCircle((int)MathF.Round(x) - Cam, (int)MathF.Round(y), r, c);

    public static void Ellipse(float x, float y, float rx, float ry, Color c) =>
        Raylib.DrawEllipse((int)MathF.Round(x) - Cam, (int)MathF.Round(y), rx, ry, c);

    /// <summary>raylib culls triangles by winding order, so draw both windings; exactly one is visible.</summary>
    public static void Tri(float ax, float ay, float bx, float by, float cx, float cy, Color c)
    {
        var a = new Vector2(ax - Cam, ay);
        var b = new Vector2(bx - Cam, by);
        var d = new Vector2(cx - Cam, cy);
        Raylib.DrawTriangle(a, b, d, c);
        Raylib.DrawTriangle(a, d, b, c);
    }

    // Screen-space text with a drop shadow.
    public static void Text(string s, int x, int y, int size, Color c)
    {
        Raylib.DrawText(s, x + 2, y + 2, size, C(0, 0, 0, 150));
        Raylib.DrawText(s, x, y, size, c);
    }

    public static void TextCenter(string s, int y, int size, Color c)
    {
        int w = Raylib.MeasureText(s, size);
        Text(s, (Game.ScreenW - w) / 2, y, size, c);
    }

    public static void TextWorld(string s, float x, float y, int size, Color c) =>
        Raylib.DrawText(s, (int)x - Cam, (int)y, size, c);
}

/// <summary>Deterministic pseudo-random hash for background decoration.</summary>
public static class Hash
{
    public static float F(int i, int salt = 0)
    {
        unchecked
        {
            uint x = (uint)i * 374761393u + (uint)salt * 668265263u;
            x = (x ^ (x >> 13)) * 1274126177u;
            x ^= x >> 16;
            return (x & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }
}

public class Particle
{
    public float X, Y, VX, VY, Life, MaxLife, Size, Grav;
    public int R, G, B;
    public string Text;

    public void Update(float dt)
    {
        VY += Grav * dt;
        X += VX * dt;
        Y += VY * dt;
        Life -= dt;
    }

    public void Draw()
    {
        float a = Math.Clamp(Life / MaxLife, 0f, 1f);
        var col = Gfx.C(R, G, B, (int)(255 * a));
        if (Text != null) Gfx.TextWorld(Text, X, Y, (int)Size, col);
        else Gfx.Rect(X - Size / 2, Y - Size / 2, Size, Size, col);
    }
}
