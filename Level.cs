using Raylib_cs;

namespace PipQuest;

public enum Tile : byte { Empty, Ground, Brick, Stone, Mystery, Used, Lava, Gate, Stump, Island }
public enum Theme { Overworld, Underground, Sky, Castle }
public enum ItemKind { Gem, Power, Heart }
public enum SpawnKind { Gem, Blob, Spiky, Hopper, Bat, PlatformH, PlatformV, FireBar, Boss, Goal, Checkpoint }

/// <summary>An entity to create when the level loads. Col/Row is the tile cell the entity occupies.</summary>
public readonly struct Spawn
{
    public readonly SpawnKind Kind;
    public readonly int Col, Row;
    public readonly float P1, P2;
    public Spawn(SpawnKind kind, int col, int row, float p1 = 0, float p2 = 0)
    {
        Kind = kind; Col = col; Row = row; P1 = p1; P2 = p2;
    }
}

public class Level
{
    public const int T = 32;

    public static readonly string[] WorldNames =
    {
        "Meadowlands", "Amber Dunes", "Mistwood", "Frostpeak",
        "Coral Coast", "Ashen Hills", "Starfall Heights", "Obsidian Keep",
    };

    // Per-world palettes, index = world - 1.
    static readonly (int r, int g, int b)[] SkyTop =
        { (92,160,255), (245,170,100), (110,140,135), (160,195,235), (70,185,230), (140,100,100), (14,18,50), (55,25,35) };
    static readonly (int r, int g, int b)[] SkyBottom =
        { (190,230,255), (255,225,170), (185,205,190), (235,245,255), (200,245,250), (210,160,140), (60,50,120), (120,55,50) };
    static readonly (int r, int g, int b)[] Grass =
        { (90,190,70), (215,175,70), (70,140,90), (235,245,255), (240,220,150), (125,125,95), (130,95,210), (95,70,85) };
    static readonly (int r, int g, int b)[] Dirt =
        { (150,95,50), (190,130,70), (100,80,60), (125,115,130), (205,165,110), (90,70,60), (70,60,105), (60,45,50) };
    static readonly (int r, int g, int b)[] Hill =
        { (70,160,90), (220,160,90), (85,120,110), (195,210,235), (85,170,160), (125,95,90), (55,45,105), (90,40,50) };

    public readonly int Width, Height, World, Stage;
    public readonly Theme Theme;
    public readonly Tile[,] Tiles;
    public readonly List<Spawn> Spawns = new();
    public readonly Dictionary<(int, int), ItemKind> Contents = new();
    readonly Dictionary<(int, int), float> bumps = new();

    public int StartCol = 3, StartRow = 12;
    public int CheckCol = -1, CheckRow = -1;

    public Level(int width, int height, Theme theme, int world, int stage)
    {
        Width = width; Height = height; Theme = theme; World = world; Stage = stage;
        Tiles = new Tile[width, height];
    }

    public int PixelWidth => Width * T;
    public int PixelHeight => Height * T;

    public Tile Get(int c, int r)
    {
        if (c < 0 || c >= Width) return Tile.Stone;   // invisible walls at the level edges
        if (r < 0 || r >= Height) return Tile.Empty;  // open sky above, bottomless pits below
        return Tiles[c, r];
    }

    public void Set(int c, int r, Tile t)
    {
        if (c >= 0 && c < Width && r >= 0 && r < Height) Tiles[c, r] = t;
    }

    public static bool Solid(Tile t) => t != Tile.Empty && t != Tile.Lava;
    public bool IsSolid(int c, int r) => Solid(Get(c, r));

    public bool TouchesLava(RectF b)
    {
        int c0 = (int)MathF.Floor(b.X / T), c1 = (int)MathF.Floor((b.Right - 1) / T);
        int r0 = (int)MathF.Floor(b.Y / T), r1 = (int)MathF.Floor((b.Bottom - 1) / T);
        for (int c = c0; c <= c1; c++)
            for (int r = r0; r <= r1; r++)
                if (Get(c, r) == Tile.Lava) return true;
        return false;
    }

    public void Bump(int c, int r) => bumps[(c, r)] = 0.2f;

    public void Update(float dt)
    {
        if (bumps.Count == 0) return;
        foreach (var key in bumps.Keys.ToList())
        {
            float v = bumps[key] - dt;
            if (v <= 0) bumps.Remove(key); else bumps[key] = v;
        }
    }

    public void OpenGates()
    {
        for (int c = 0; c < Width; c++)
            for (int r = 0; r < Height; r++)
                if (Tiles[c, r] == Tile.Gate) Tiles[c, r] = Tile.Empty;
    }

    public static Color Col((int r, int g, int b) c, float m = 1f, int a = 255) =>
        Gfx.C((int)(c.r * m), (int)(c.g * m), (int)(c.b * m), a);

    public (int r, int g, int b) BrickRgb => Theme switch
    {
        Theme.Underground => (60, 128, 160),
        Theme.Castle => (128, 64, 64),
        _ => (196, 98, 54),
    };

    // ---------------------------------------------------------------- background

    static void Parallax(float factor, float spacing, Action<float, int> draw)
    {
        float off = Gfx.Cam * factor;
        int first = (int)MathF.Floor(off / spacing) - 1;
        int count = (int)(Game.ScreenW / spacing) + 4;
        for (int i = first; i < first + count; i++) draw(i * spacing - off, i);
    }

    static void Cloud(float x, float y, float s, int alpha = 220)
    {
        var c = Gfx.C(255, 255, 255, alpha);
        Raylib.DrawEllipse((int)x, (int)y, 40 * s, 18 * s, c);
        Raylib.DrawEllipse((int)(x - 30 * s), (int)(y + 6 * s), 26 * s, 14 * s, c);
        Raylib.DrawEllipse((int)(x + 32 * s), (int)(y + 5 * s), 28 * s, 14 * s, c);
    }

    static void Stars(double time)
    {
        float off = Gfx.Cam * 0.03f;
        for (int i = 0; i < 90; i++)
        {
            float x = ((Hash.F(i, 71) * 1400 - off) % 1400 + 1400) % 1400 - 200;
            float y = Hash.F(i, 72) * 320;
            int a = (int)(150 + 100 * MathF.Sin((float)time * 2 + i));
            Raylib.DrawRectangle((int)x, (int)y, 2, 2, Gfx.C(255, 255, 230, a));
        }
    }

    public void DrawBackground(double time)
    {
        int w = World - 1;
        int sw = Game.ScreenW, sh = Game.ScreenH;
        switch (Theme)
        {
            case Theme.Overworld:
            case Theme.Sky:
                Raylib.DrawRectangleGradientV(0, 0, sw, sh, Col(SkyTop[w]), Col(SkyBottom[w]));
                if (World == 7) Stars(time);
                int cloudAlpha = World == 7 ? 70 : 220;
                Parallax(0.08f, 260f, (sx, i) =>
                    Cloud(sx + Hash.F(i, 12) * 80, 70 + Hash.F(i, 11) * 140, 0.7f + Hash.F(i, 13) * 0.6f, cloudAlpha));
                if (Theme == Theme.Overworld)
                {
                    var far = Col(Hill[w], 0.8f);
                    var near = Col(Hill[w]);
                    Parallax(0.25f, 380f, (sx, i) =>
                        Raylib.DrawCircle((int)(sx + 190), sh + 60, 140 + Hash.F(i, 21) * 90, far));
                    Parallax(0.45f, 300f, (sx, i) =>
                        Raylib.DrawCircle((int)(sx + 150), sh + 40, 90 + Hash.F(i, 31) * 60, near));
                }
                else
                {
                    Parallax(0.2f, 340f, (sx, i) =>
                        Cloud(sx + Hash.F(i, 14) * 100, 380 + Hash.F(i, 15) * 110, 1.4f + Hash.F(i, 16) * 0.8f, cloudAlpha));
                }
                break;

            case Theme.Underground:
                Raylib.DrawRectangleGradientV(0, 0, sw, sh, Gfx.C(14, 16, 28), Gfx.C(30, 34, 52));
                var pillar = Gfx.C(40, 46, 70);
                Parallax(0.35f, 180f, (sx, i) =>
                {
                    int h = 140 + (int)(Hash.F(i, 41) * 200);
                    Raylib.DrawRectangle((int)sx, sh - h, 46, h, pillar);
                    Raylib.DrawRectangle((int)sx, 0, 46, 60 + (int)(Hash.F(i, 42) * 80), pillar);
                });
                Parallax(0.5f, 230f, (sx, i) =>
                {
                    if (Hash.F(i, 51) < 0.5f) return;
                    float y = 150 + Hash.F(i, 52) * 250;
                    float pulse = 0.6f + 0.4f * MathF.Sin((float)time * 2 + i);
                    Raylib.DrawCircle((int)sx, (int)y, 6, Gfx.C(120, 220, 255, (int)(120 * pulse)));
                });
                break;

            default: // Castle
                Raylib.DrawRectangleGradientV(0, 0, sw, sh, Gfx.C(24, 14, 20), Gfx.C(70, 26, 24));
                float off = Gfx.Cam * 0.5f;
                var line = Gfx.C(60, 30, 34);
                for (int row = 0; row < 18; row++)
                {
                    float shift = (row % 2) * 24;
                    int first = (int)MathF.Floor((off - shift) / 48f) - 1;
                    for (int k = first; k < first + 23; k++)
                        Raylib.DrawRectangleLines((int)(k * 48 + shift - off), row * 32, 48, 32, line);
                }
                Parallax(0.5f, 320f, (sx, i) =>
                {
                    float fl = 0.7f + 0.3f * MathF.Sin((float)time * 9 + i * 3);
                    Raylib.DrawCircle((int)sx, 200, 26 * fl, Gfx.C(255, 140, 40, 50));
                    Raylib.DrawRectangle((int)sx - 3, 204, 6, 20, Gfx.C(90, 60, 40));
                    Raylib.DrawCircle((int)sx, 198, 6 * fl, Gfx.C(255, 200, 80));
                });
                break;
        }
    }

    // ---------------------------------------------------------------- tiles

    public void DrawTiles(double time)
    {
        int c0 = Math.Max(0, Gfx.Cam / T - 1);
        int c1 = Math.Min(Width - 1, (Gfx.Cam + Game.ScreenW) / T + 1);
        for (int c = c0; c <= c1; c++)
            for (int r = 0; r < Height; r++)
            {
                var tl = Tiles[c, r];
                if (tl == Tile.Empty) continue;
                float y = r * T;
                if (bumps.TryGetValue((c, r), out float bt)) y -= MathF.Sin(bt / 0.2f * MathF.PI) * 8f;
                DrawTile(tl, c, r, c * T, y, time);
            }
    }

    static void StoneBlock(float x, float y, (int r, int g, int b) c)
    {
        Gfx.Rect(x, y, T, T, Col(c));
        Gfx.Rect(x, y, T, 3, Col(c, 1.25f));
        Gfx.Rect(x, y, 3, T, Col(c, 1.25f));
        Gfx.Rect(x, y + T - 3, T, 3, Col(c, 0.6f));
        Gfx.Rect(x + T - 3, y, 3, T, Col(c, 0.6f));
    }

    static void Rivets(float x, float y, Color c)
    {
        Gfx.Rect(x + 4, y + 4, 3, 3, c);
        Gfx.Rect(x + T - 7, y + 4, 3, 3, c);
        Gfx.Rect(x + 4, y + T - 7, 3, 3, c);
        Gfx.Rect(x + T - 7, y + T - 7, 3, 3, c);
    }

    void DrawTile(Tile tl, int c, int r, float x, float y, double time)
    {
        int w = World - 1;
        var above = Get(c, r - 1);
        bool openAbove = !Solid(above) && above != Tile.Lava;
        bool openBelow = !IsSolid(c, r + 1);

        switch (tl)
        {
            case Tile.Ground:
                if (Theme == Theme.Overworld)
                {
                    Gfx.Rect(x, y, T, T, Col(Dirt[w]));
                    if (Hash.F(c * 7 + r * 13, 5) > 0.45f)
                        Gfx.Rect(x + 4 + Hash.F(c, r) * 20, y + 14 + Hash.F(r, c) * 12, 5, 4, Col(Dirt[w], 0.75f));
                    if (openAbove)
                    {
                        Gfx.Rect(x, y, T, 9, Col(Grass[w]));
                        Gfx.Rect(x, y + 9, T, 3, Col(Grass[w], 0.7f));
                        Gfx.Rect(x + (c % 3) * 8 + 3, y + 12, 4, 3, Col(Grass[w], 0.7f));
                    }
                }
                else if (Theme == Theme.Underground)
                {
                    Gfx.Rect(x, y, T, T, Gfx.C(62, 76, 108));
                    Gfx.Rect(x, y + T - 3, T, 3, Gfx.C(44, 54, 80));
                    Gfx.Rect(x + T - 3, y, 3, T, Gfx.C(44, 54, 80));
                    if (openAbove) Gfx.Rect(x, y, T, 4, Gfx.C(110, 130, 170));
                }
                else
                {
                    var mortar = Gfx.C(60, 58, 68);
                    Gfx.Rect(x, y, T, T, Gfx.C(92, 90, 100));
                    Gfx.Rect(x, y + 15, T, 2, mortar);
                    Gfx.Rect(x + (r % 2 == 0 ? 15 : 5), y, 2, 15, mortar);
                    Gfx.Rect(x + (r % 2 == 0 ? 5 : 22), y + 17, 2, 15, mortar);
                    if (openAbove) Gfx.Rect(x, y, T, 3, Gfx.C(130, 128, 140));
                }
                break;

            case Tile.Island:
                Gfx.Rect(x, y, T, T, Col(Dirt[w]));
                if (openAbove)
                {
                    Gfx.Rect(x, y, T, 10, Col(Grass[w]));
                    Gfx.Rect(x, y + 10, T, 3, Col(Grass[w], 0.7f));
                }
                if (openBelow)
                {
                    Gfx.Rect(x, y + T - 6, T, 6, Col(Dirt[w], 0.65f));
                    if (c % 2 == 0) Gfx.Rect(x + 10, y + T, 3, 6 + (c % 3) * 3, Col(Grass[w], 0.6f));
                }
                break;

            case Tile.Brick:
            {
                var bc = BrickRgb;
                var m = Col(bc, 0.55f);
                Gfx.Rect(x, y, T, T, Col(bc));
                Gfx.Rect(x, y, T, 2, m);
                Gfx.Rect(x, y + 15, T, 2, m);
                Gfx.Rect(x + 15, y + 2, 2, 13, m);
                Gfx.Rect(x + 6, y + 17, 2, 15, m);
                Gfx.Rect(x + 24, y + 17, 2, 15, m);
                break;
            }

            case Tile.Stone:
                StoneBlock(x, y, Theme == Theme.Castle ? (120, 118, 130) : (150, 150, 160));
                break;

            case Tile.Mystery:
            {
                Gfx.Rect(x, y, T, T, Gfx.C(120, 80, 20));
                Gfx.Rect(x + 2, y + 2, T - 4, T - 4, Gfx.C(240, 180, 50));
                float pulse = 0.5f + 0.5f * MathF.Sin((float)time * 4f + c);
                var glyph = Gfx.C(255, 255, (int)(150 + 100 * pulse));
                Gfx.Tri(x + 16, y + 7, x + 8, y + 16, x + 24, y + 16, glyph);
                Gfx.Tri(x + 8, y + 16, x + 16, y + 25, x + 24, y + 16, glyph);
                Rivets(x, y, Gfx.C(150, 100, 25));
                break;
            }

            case Tile.Used:
                Gfx.Rect(x, y, T, T, Gfx.C(90, 65, 45));
                Gfx.Rect(x + 2, y + 2, T - 4, T - 4, Gfx.C(150, 115, 80));
                Rivets(x, y, Gfx.C(95, 70, 50));
                break;

            case Tile.Lava:
                Gfx.Rect(x, y, T, T, Gfx.C(210, 60, 20));
                if (above != Tile.Lava)
                {
                    float wave = MathF.Sin((float)time * 3f + c * 0.9f) * 3f;
                    Gfx.Rect(x, y - 10, T, 10, Gfx.C(255, 120, 40, 50));
                    Gfx.Rect(x, y + 4 + wave, T, 8, Gfx.C(255, 150, 40));
                    Gfx.Rect(x, y + 2 + wave, T, 3, Gfx.C(255, 220, 120));
                }
                break;

            case Tile.Gate:
                Gfx.Rect(x, y, T, T, Gfx.C(30, 28, 36));
                for (int i = 0; i < 4; i++) Gfx.Rect(x + 3 + i * 8, y, 4, T, Gfx.C(120, 118, 135));
                Gfx.Rect(x, y + 12, T, 4, Gfx.C(90, 88, 100));
                break;

            case Tile.Stump:
                if (Theme == Theme.Underground || Theme == Theme.Castle)
                {
                    StoneBlock(x, y, (110, 120, 150));
                    break;
                }
                Gfx.Rect(x, y, T, T, Gfx.C(122, 80, 45));
                Gfx.Rect(x + 6 + (c % 2) * 10, y, 3, T, Gfx.C(95, 60, 32));
                if (openAbove)
                {
                    Gfx.Rect(x, y, T, 8, Gfx.C(200, 160, 105));
                    Gfx.Rect(x + (c % 2 == 0 ? 20 : 0), y + 2, 12, 3, Gfx.C(160, 120, 75));
                }
                break;
        }
    }
}
