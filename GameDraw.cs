using Raylib_cs;

namespace PipQuest;

public partial class Game
{
    readonly Player preview = new();

    void Draw()
    {
        Raylib.ClearBackground(Gfx.Black);
        switch (state)
        {
            case GameState.Title: DrawTitle(); break;
            case GameState.Intro: DrawIntro(); break;
            case GameState.GameOver: DrawGameOver(); break;
            case GameState.Victory: DrawVictory(); break;
            default: DrawWorld(); break;
        }
    }

    void DrawWorld()
    {
        Gfx.Cam = (int)MathF.Floor(CamX);
        Level.DrawBackground(clock);
        foreach (var e in Entities) if (e.DrawBehind) e.Draw(this, clock);
        Level.DrawTiles(clock);
        foreach (var e in Entities) if (!e.DrawBehind) e.Draw(this, clock);
        bool enteredPortal = state == GameState.Clear && stateT > 0.35f;
        if (!enteredPortal) Player.Draw(clock);
        foreach (var p in Particles) p.Draw();

        DrawHud();
        if (ActiveBoss != null && !ActiveBoss.Dead) DrawBossBar(ActiveBoss);

        if (paused)
        {
            Raylib.DrawRectangle(0, 0, ScreenW, ScreenH, Gfx.C(0, 0, 0, 140));
            Gfx.TextCenter("PAUSED", 220, 48, Gfx.White);
            Gfx.TextCenter("Press P to resume", 285, 22, Gfx.White);
        }
        if (state == GameState.Clear)
        {
            Gfx.TextCenter("STAGE CLEAR!", 200, 52, Gfx.C(255, 220, 90));
            Gfx.TextCenter($"Time bonus: {(int)MathF.Ceiling(Time)} x 50", 270, 22, Gfx.White);
        }
    }

    void DrawHud()
    {
        Raylib.DrawRectangle(0, 0, ScreenW, 36, Gfx.C(0, 0, 0, 120));
        Gfx.Text($"SCORE {Score:D7}", 16, 9, 20, Gfx.White);

        int saved = Gfx.Cam;
        Gfx.Cam = 0;
        GemPickup.DrawGem(250, 18, (float)clock);
        Gfx.Cam = saved;
        Gfx.Text($"x {Gems:D2}", 266, 9, 20, Gfx.White);

        Gfx.Text($"WORLD {World}-{Stage}", 400, 9, 20, Gfx.White);
        int tm = (int)MathF.Ceiling(Time);
        Gfx.Text($"TIME {tm:D3}", 590, 9, 20, tm <= 60 ? Gfx.C(255, 110, 110) : Gfx.White);
        Gfx.Text($"LIVES {Lives}", 770, 9, 20, Gfx.White);
        if (Player.Power == PowerState.Ember) Gfx.Text("EMBER", 880, 9, 20, Gfx.C(255, 160, 60));
    }

    void DrawBossBar(Boss b)
    {
        int bw = 320, bx = (ScreenW - bw) / 2, by = 46;
        Raylib.DrawRectangle(bx - 3, by - 3, bw + 6, 18, Gfx.C(0, 0, 0, 180));
        Raylib.DrawRectangle(bx, by, (int)(bw * Math.Max(0, b.Hp) / (float)b.MaxHp), 12, Gfx.C(220, 60, 50));
        Gfx.TextCenter("STONE WARDEN", by + 18, 18, Gfx.White);
    }

    void DrawPreviewPip(float centerX, float bottom, PowerState power)
    {
        preview.Power = power;
        preview.Spawn(centerX, bottom);
        preview.OnGround = true;
        int saved = Gfx.Cam;
        Gfx.Cam = 0;
        preview.Draw(clock);
        Gfx.Cam = saved;
    }

    void DrawTitle()
    {
        Gfx.Cam = (int)titleCam;
        titleLevel.DrawBackground(clock);
        titleLevel.DrawTiles(clock);
        Raylib.DrawRectangle(0, 0, ScreenW, ScreenH, Gfx.C(0, 0, 0, 120));

        Gfx.TextCenter("PIP'S QUEST", 60, 72, Gfx.C(255, 215, 90));
        Gfx.TextCenter("An original 2D platformer  -  8 worlds, 32 stages", 145, 22, Gfx.White);

        string[] lines =
        {
            "Move: Arrow keys / A D",
            "Jump: Space / Z / Up / W   (hold for a higher jump)",
            "Run: hold Shift or X      Throw embers when powered: X / K / F",
            "Stomp enemies from above. Punch blocks from below.",
            "Pause: P      Quit: Esc",
        };
        for (int i = 0; i < lines.Length; i++)
            Gfx.TextCenter(lines[i], 200 + i * 30, 20, Gfx.C(230, 230, 230));

        DrawPreviewPip(ScreenW / 2f, 420f, PowerState.Big);
        if ((int)(clock * 2) % 2 == 0) Gfx.TextCenter("Press ENTER to start", 460, 28, Gfx.White);
    }

    void DrawIntro()
    {
        Gfx.TextCenter($"WORLD {World}-{Stage}", 160, 48, Gfx.White);
        Gfx.TextCenter($"{PipQuest.Level.WorldNames[World - 1]}  -  {StageNames[Stage]}", 225, 24, Gfx.C(255, 215, 90));
        DrawPreviewPip(ScreenW / 2f - 40, 320, Player.Power);
        Gfx.Text($"x {Lives}", ScreenW / 2 - 10, 292, 28, Gfx.White);
        if (Stage == 4)
            Gfx.TextCenter("A guardian waits at the end of this fortress...", 380, 20, Gfx.C(255, 140, 120));
        if (checkpointReached)
            Gfx.TextCenter("Resuming from the checkpoint lantern", 410, 18, Gfx.C(200, 200, 200));
    }

    void DrawGameOver()
    {
        Gfx.TextCenter("GAME OVER", 160, 64, Gfx.C(255, 90, 90));
        Gfx.TextCenter($"Score: {Score}", 250, 26, Gfx.White);
        if (stateT > 1f)
        {
            Gfx.TextCenter($"Press ENTER to continue from World {World}-1", 320, 22, Gfx.White);
            Gfx.TextCenter("(score resets)   -   Esc to quit", 352, 20, Gfx.C(200, 200, 200));
        }
    }

    void DrawVictory()
    {
        Raylib.DrawRectangleGradientV(0, 0, ScreenW, ScreenH, Gfx.C(20, 20, 60), Gfx.C(80, 40, 90));
        Gfx.Cam = 0;
        foreach (var p in Particles) p.Draw();
        Gfx.TextCenter("THE OBSIDIAN KEEP HAS FALLEN!", 140, 40, Gfx.C(255, 215, 90));
        Gfx.TextCenter("Pip conquered all 8 worlds.", 205, 26, Gfx.White);
        Gfx.TextCenter($"Final score: {Score}", 255, 28, Gfx.White);
        DrawPreviewPip(ScreenW / 2f, 400f, PowerState.Ember);
        if (stateT > 2f) Gfx.TextCenter("Press ENTER to return to the title screen", 450, 22, Gfx.White);
    }
}
