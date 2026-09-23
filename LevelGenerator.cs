namespace PipQuest;

/// <summary>
/// Builds every stage from a fixed seed, so World 3-2 is always the same layout.
/// Stage 1 = overworld, 2 = caverns, 3 = sky islands, 4 = fortress with a boss.
/// Difficulty (gap widths, enemy mix/density, fire-bar length/speed) scales with the world number.
/// </summary>
public static class LevelGenerator
{
    public static Level Build(int world, int stage)
    {
        var theme = stage switch
        {
            1 => Theme.Overworld,
            2 => Theme.Underground,
            3 => Theme.Sky,
            _ => Theme.Castle,
        };
        int width = theme switch
        {
            Theme.Castle => 200 + world * 8,
            Theme.Sky => 190 + world * 10,
            _ => 200 + world * 10,
        };
        var level = new Level(width, 17, theme, world, stage);
        var b = new LevelBuilder(level, new Random(world * 7919 + stage * 131 + 17), world);
        switch (theme)
        {
            case Theme.Sky: b.BuildSky(); break;
            case Theme.Castle: b.BuildCastle(); break;
            default: b.BuildGround(); break;
        }
        b.PlaceCheckpoint();
        return level;
    }
}

internal class LevelBuilder
{
    readonly Level L;
    readonly Random R;
    readonly int world;
    readonly float t; // 0 in world 1 .. 1 in world 8

    int x;                 // next column to write
    int ground = 14;       // row of the walkable surface
    int minGround = 10;    // highest the ground is allowed to rise
    int ceilingRows;       // solid rows at the top (caverns / fortress)
    int powerCount;
    bool heartPlaced;

    public LevelBuilder(Level level, Random rng, int world)
    {
        L = level; R = rng; this.world = world;
        t = (world - 1) / 7f;
    }

    // ------------------------------------------------------------ helpers

    int Rand(int lo, int hi) => R.Next(lo, hi + 1); // inclusive
    bool Chance(double p) => R.NextDouble() < p;

    int Pick(params double[] weights)
    {
        double total = 0;
        foreach (var w in weights) total += w;
        double roll = R.NextDouble() * total;
        for (int i = 0; i < weights.Length; i++)
        {
            roll -= weights[i];
            if (roll < 0) return i;
        }
        return weights.Length - 1;
    }

    Tile GroundTile => L.Theme == Theme.Sky ? Tile.Island : Tile.Ground;

    void Ceil(int col)
    {
        for (int r = 0; r < ceilingRows; r++) L.Set(col, r, Tile.Stone);
    }

    void Column(int col)
    {
        for (int r = ground; r < L.Height; r++) L.Set(col, r, GroundTile);
        Ceil(col);
    }

    void Flat(int n)
    {
        for (int i = 0; i < n; i++) { Column(x); x++; }
    }

    void Gap(int n)
    {
        for (int i = 0; i < n; i++)
        {
            Ceil(x);
            if (L.Theme == Theme.Castle)
            {
                L.Set(x, L.Height - 2, Tile.Lava);
                L.Set(x, L.Height - 1, Tile.Lava);
            }
            x++;
        }
    }

    void StoneColumn(int h)
    {
        Column(x);
        for (int r = ground - h; r < ground; r++) L.Set(x, r, Tile.Stone);
        x++;
    }

    void Add(SpawnKind k, int col, int row, float p1 = 0, float p2 = 0) =>
        L.Spawns.Add(new Spawn(k, col, row, p1, p2));

    SpawnKind PickEnemy()
    {
        if (world < 2) return SpawnKind.Blob;
        return Pick(1.0, 0.3 + t * 0.4, 0.25 + t * 0.6) switch
        {
            0 => SpawnKind.Blob,
            1 => SpawnKind.Hopper,
            _ => SpawnKind.Spiky,
        };
    }

    void Enemies(int from, int to, int row, double pOne, double pTwo)
    {
        if (to < from) return;
        if (Chance(pOne)) Add(PickEnemy(), Rand(from, to), row);
        if (Chance(pTwo)) Add(PickEnemy(), Rand(from, to), row);
    }

    void GemLine(int from, int to, int row)
    {
        for (int c = from; c <= to; c++) Add(SpawnKind.Gem, c, row);
    }

    void GemArc(int from, int count, int baseRow)
    {
        for (int i = 0; i < count; i++)
        {
            float k = count == 1 ? 0.5f : i / (float)(count - 1);
            Add(SpawnKind.Gem, from + i, baseRow - (int)MathF.Round(MathF.Sin(k * MathF.PI) * 2f));
        }
    }

    void Block(int col, int row, bool mysteryBias)
    {
        bool mystery = Chance(mysteryBias ? 0.5 : 0.3);
        L.Set(col, row, mystery ? Tile.Mystery : Tile.Brick);
        if (mystery)
        {
            if (powerCount < 4 && Chance(0.2)) { L.Contents[(col, row)] = ItemKind.Power; powerCount++; }
        }
        else if (!heartPlaced && Chance(0.04)) { L.Contents[(col, row)] = ItemKind.Heart; heartPlaced = true; }
        else if (Chance(0.07)) L.Contents[(col, row)] = ItemKind.Gem; // hidden gem in a brick
    }

    void PowerBlock(int col, int row)
    {
        L.Set(col, row, Tile.Mystery);
        L.Contents[(col, row)] = ItemKind.Power;
        powerCount++;
    }

    // ------------------------------------------------------------ overworld + caverns

    public void BuildGround()
    {
        bool under = L.Theme == Theme.Underground;
        ceilingRows = under ? 3 : 0;
        minGround = under ? 12 : 10;
        ground = 14;

        Flat(12);
        L.StartCol = 3;
        L.StartRow = ground - 1;

        // Opening set-piece: always offers a power-up.
        int s = x;
        Flat(10);
        int row = ground - 4;
        L.Set(s + 2, row, Tile.Mystery);
        L.Set(s + 4, row, Tile.Brick);
        PowerBlock(s + 5, row);
        L.Set(s + 6, row, Tile.Brick);
        L.Set(s + 7, row, Tile.Mystery);
        Add(SpawnKind.Blob, s + 8, ground - 1);

        int end = L.Width - 40;
        while (x < end)
        {
            switch (Pick(20, 20, 12 + t * 10, 10, 10, 7, 6, world >= 2 ? 8 : 0, world >= 3 ? 6 : 0))
            {
                case 0: ChunkFlatEnemies(); break;
                case 1: ChunkBlockRow(); break;
                case 2: ChunkPit(); break;
                case 3: ChunkStep(); break;
                case 4: ChunkStumps(); break;
                case 5: ChunkStairs(); break;
                case 6: ChunkGems(); break;
                case 7: ChunkLedge(); break;
                default: ChunkBats(); break;
            }
        }

        // Finale: a staircase, then a run-up to the portal.
        Flat(3);
        for (int h = 1; h <= 4; h++) StoneColumn(h);
        StoneColumn(4);
        while (x < L.Width) Flat(1);
        Add(SpawnKind.Goal, L.Width - 8, ground - 1);
    }

    void ChunkFlatEnemies()
    {
        int n = Rand(6, 10), s = x;
        Flat(n);
        Enemies(s + 2, s + n - 2, ground - 1, 0.55 + t * 0.35, 0.15 + t * 0.4);
        if (Chance(0.35)) GemLine(s + 2, s + 4, ground - 3);
    }

    void ChunkBlockRow()
    {
        int n = Rand(9, 12), s = x;
        Flat(n);
        int row = ground - 4, bx = s + Rand(1, 3), len = Rand(3, 5);
        for (int i = 0; i < len; i++) Block(bx + i, row, false);
        if (Chance(0.45) && row - 4 >= ceilingRows + 1)
        {
            int count = Math.Min(3, len);
            int ux = bx + Rand(0, len - count);
            for (int i = 0; i < count; i++) Block(ux + i, row - 4, true);
        }
        if (powerCount < 2 && Chance(0.3)) PowerBlock(bx + len / 2, row);
        Enemies(s + 1, s + n - 2, ground - 1, 0.5 + t * 0.3, t * 0.3);
    }

    void ChunkPit()
    {
        Flat(2);
        int w = Rand(2, 2 + (int)MathF.Round(t * 3)); // max 5 tiles in world 8
        int s = x;
        Gap(w);
        if (Chance(0.4)) GemArc(s, w, ground - 3);
        int ng = Math.Clamp(ground + Rand(-1, 1), minGround, 14);
        if (w >= 4 && ng < ground) ng = ground; // never make a long jump uphill
        ground = ng;
        Flat(2);
    }

    void ChunkStep()
    {
        int d = Rand(1, 2) * (Chance(0.5) ? 1 : -1);
        ground = Math.Clamp(ground + d, minGround, 14);
        int n = Rand(4, 7), s = x;
        Flat(n);
        Enemies(s + 1, s + n - 1, ground - 1, 0.4, 0);
    }

    void ChunkStumps()
    {
        int n = Rand(10, 13), s = x;
        Flat(n);
        int count = Rand(1, 2);
        for (int i = 0; i < count; i++)
        {
            int c = s + 2 + i * 6;
            int h = Rand(2, world >= 4 ? 4 : 3);
            for (int k = 0; k < 2; k++)
                for (int r = ground - h; r < ground; r++) L.Set(c + k, r, Tile.Stump);
            if (Chance(0.5)) Add(SpawnKind.Gem, c, ground - h - 2);
        }
        if (count == 2) Enemies(s + 5, s + 7, ground - 1, 0.7, 0);
        else Enemies(s + 5, s + n - 2, ground - 1, 0.6, 0);
    }

    void ChunkStairs()
    {
        Flat(1);
        int h = Rand(3, 4);
        for (int i = 1; i <= h; i++) StoneColumn(i);
        if (Chance(0.6)) Gap(Rand(1, 2 + (int)MathF.Round(t)));
        for (int i = h; i >= 1; i--) StoneColumn(i);
        Flat(2);
    }

    void ChunkGems()
    {
        int n = Rand(7, 9), s = x;
        Flat(n);
        GemArc(s + 1, n - 2, ground - 3);
    }

    void ChunkLedge()
    {
        Flat(2);
        int w = Rand(6, 8), s = x;
        Gap(w);
        int lx = s + (w - 3) / 2;
        for (int i = 0; i < 3; i++) L.Set(lx + i, ground - 3, Tile.Stone);
        GemLine(lx, lx + 2, ground - 5);
        Flat(2);
    }

    void ChunkBats()
    {
        int n = Rand(9, 12), s = x;
        Flat(n);
        Add(SpawnKind.Bat, s + n / 2, ground - 4);
        if (Chance(0.3 + t * 0.3)) Add(SpawnKind.Bat, s + n - 2, ground - 6);
    }

    // ------------------------------------------------------------ sky islands

    void IslandColumn(int c, int row)
    {
        L.Set(c, row, Tile.Island);
        L.Set(c, row + 1, Tile.Island);
    }

    void Island(int col, int w, int row, bool decorate)
    {
        for (int i = 0; i < w; i++) IslandColumn(col + i, row);
        for (int i = 1; i < w - 1; i++) L.Set(col + i, row + 2, Tile.Island);
        if (!decorate) return;
        if (w >= 5 && Chance(0.45 + t * 0.3)) Add(PickEnemy(), col + w / 2, row - 1);
        if (Chance(0.4)) GemLine(col + 1, col + w - 2, row - 2);
        if (w >= 4 && row - 4 >= 3 && Chance(0.3)) Block(col + w / 2, row - 4, true);
    }

    public void BuildSky()
    {
        ceilingRows = 0;
        int row = 12;
        Island(0, 12, row, false);
        L.StartCol = 3;
        L.StartRow = row - 1;
        PowerBlock(7, row - 4);
        x = 12;

        int end = L.Width - 40;
        while (x < end)
        {
            double roll = R.NextDouble();
            if (roll < 0.2)
            {
                // Wide gap bridged by a sideways-moving platform at island height.
                int gap = Rand(7, 9 + (int)MathF.Round(t * 2));
                Add(SpawnKind.PlatformH, x, row, (x + gap - 3) * Level.T, 70f + t * 40f);
                if (Chance(0.5)) GemLine(x + 3, x + gap - 4, row - 3);
                x += gap;
                int w = Rand(4, 7);
                Island(x, w, row, true);
                x += w;
            }
            else if (roll < 0.3 && row >= 11)
            {
                // Elevator up to a much higher island.
                int newRow = row - Rand(4, 5);
                Add(SpawnKind.PlatformV, x, row, newRow * Level.T, 60f + t * 30f);
                x += 3;
                row = newRow;
                int w = Rand(5, 7);
                Island(x, w, row, true);
                x += w;
            }
            else
            {
                int newRow = Math.Clamp(row + Rand(-3, 3), 7, 13);
                int rise = row - newRow;
                int gap = rise >= 2 ? Rand(2, 3) : Rand(2, 3 + (int)MathF.Round(t * 2));
                if (world >= 2 && Chance(0.25 + t * 0.35))
                    Add(SpawnKind.Bat, x + gap / 2, Math.Min(row, newRow) - 3);
                x += gap;
                row = newRow;
                int w = Rand(3, 7);
                Island(x, w, row, true);
                x += w;
            }
        }

        x += 2;
        while (x < L.Width) { IslandColumn(x, row); x++; }
        Add(SpawnKind.Goal, L.Width - 7, row - 1);
    }

    // ------------------------------------------------------------ fortress

    public void BuildCastle()
    {
        ceilingRows = 4;
        ground = 13;
        minGround = 13;

        Flat(10);
        L.StartCol = 2;
        L.StartRow = ground - 1;
        PowerBlock(6, ground - 4);

        const int arenaLen = 24;
        int end = L.Width - 60;
        while (x < end)
        {
            switch (Pick(20, 16, world >= 2 ? 10 : 4, 12, world >= 2 ? 10 : 0, 14, 10))
            {
                case 0: CastleFireBar(); break;
                case 1: CastleLavaPit(); break;
                case 2: CastleLavaSteps(); break;
                case 3: CastleLowCeiling(); break;
                case 4: CastleLavaPlatform(); break;
                case 5: CastleEnemyHall(); break;
                default: CastlePillars(); break;
            }
        }

        int s = x;
        Flat(4);
        PowerBlock(s + 1, ground - 4);

        int arenaStart = x;
        Flat(arenaLen);
        int gateCol = x;
        Column(x);
        for (int r = ceilingRows; r < ground; r++) L.Set(x, r, Tile.Gate);
        x++;
        while (x < L.Width) Flat(1);

        Add(SpawnKind.Boss, arenaStart + 17, ground - 1, arenaStart * Level.T, gateCol * Level.T);
        Add(SpawnKind.Goal, L.Width - 6, ground - 1);
    }

    void CastleFireBar()
    {
        int n = Rand(8, 10), s = x;
        Flat(n);
        int ac = s + n / 2, ar = ground - 3;
        L.Set(ac, ar, Tile.Stone);
        int len = 5 + (world >= 4 ? 1 : 0);
        float speed = (1.5f + t * 1.3f) * (Chance(0.5) ? 1f : -1f);
        Add(SpawnKind.FireBar, ac, ar, len, speed);
    }

    void CastleLavaPit()
    {
        Flat(2);
        Gap(Rand(2, 3 + (int)MathF.Round(t * 2)));
        Flat(2);
    }

    void CastleLavaSteps()
    {
        Flat(2);
        int w = Rand(7, 9), s = x;
        Gap(w);
        for (int k = 0; k < 2; k++)
        {
            L.Set(s + 2 + k, ground - 2, Tile.Stone);
            L.Set(s + w - 4 + k, ground - 2, Tile.Stone);
        }
        Flat(2);
    }

    void CastleLowCeiling()
    {
        int n = Rand(7, 10), s = x;
        Flat(n);
        for (int c = s + 1; c < s + n - 1; c++)
            for (int r = ceilingRows; r <= ground - 4; r++) L.Set(c, r, Tile.Stone);
        Enemies(s + 2, s + n - 3, ground - 1, 0.6, 0.2 + t * 0.3);
    }

    void CastleLavaPlatform()
    {
        Flat(2);
        int gap = Rand(8, 10), s = x;
        Gap(gap);
        Add(SpawnKind.PlatformH, s, ground, (s + gap - 3) * Level.T, 75f + t * 35f);
        Flat(2);
    }

    void CastleEnemyHall()
    {
        int n = Rand(8, 11), s = x;
        Flat(n);
        Enemies(s + 2, s + n - 2, ground - 1, 0.8, 0.4 + t * 0.4);
        if (Chance(0.4))
            for (int i = 0; i < 3; i++) Block(s + 3 + i, ground - 4, true);
    }

    void CastlePillars()
    {
        int n = Rand(9, 10), s = x;
        Flat(n);
        int h = Rand(2, 3);
        for (int r = ground - h; r < ground; r++) L.Set(s + 3, r, Tile.Stone);
        for (int r = ground - h - 1; r < ground; r++) L.Set(s + 7, r, Tile.Stone);
        Enemies(s + 4, s + 6, ground - 1, 0.5, 0);
    }

    // ------------------------------------------------------------ checkpoint

    int Surface(int c)
    {
        if (c < 0 || c >= L.Width) return -1;
        for (int r = 3; r < L.Height; r++)
        {
            var tl = L.Get(c, r);
            if ((tl == Tile.Ground || tl == Tile.Island) && !L.IsSolid(c, r - 1) && !L.IsSolid(c, r - 2))
                return r;
        }
        return -1;
    }

    bool NearHazard(int c)
    {
        foreach (var s in L.Spawns)
            if ((s.Kind == SpawnKind.FireBar || s.Kind == SpawnKind.Boss || s.Kind == SpawnKind.Goal ||
                 s.Kind == SpawnKind.PlatformH || s.Kind == SpawnKind.PlatformV) && Math.Abs(s.Col - c) < 6)
                return true;
        return false;
    }

    public void PlaceCheckpoint()
    {
        int mid = L.Width / 2;
        for (int d = 0; d < 40; d++)
        {
            foreach (int c in new[] { mid + d, mid - d })
            {
                int r = Surface(c);
                if (r < 0 || Surface(c - 1) != r || Surface(c + 1) != r || NearHazard(c)) continue;
                L.CheckCol = c;
                L.CheckRow = r - 1;
                Add(SpawnKind.Checkpoint, c, r - 1);
                return;
            }
        }
    }
}
