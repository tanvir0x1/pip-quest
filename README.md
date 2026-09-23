# Pip's Quest

An original 2D side-scrolling platformer in C#, in the spirit of classic 8-bit platformers.
Hero, enemies, items, and art are all original, drawn with simple shapes (no image files needed).

## Run it

Requirements: the .NET 8 SDK (https://dotnet.microsoft.com/download).

```
cd PipQuest
dotnet run
```

The first run downloads the `Raylib-cs` 7.0.2 NuGet package, which bundles raylib's native libraries
for Windows, Linux and macOS.

## Controls

| Action | Keys |
|---|---|
| Move | Arrow keys or A / D |
| Jump (hold for higher) | Space, Z, Up or W |
| Run | Hold Left Shift or X |
| Throw embers (Ember power only) | X, K or F |
| Pause | P |
| Quit | Esc |
| Start / continue | Enter |

## What's in the game

- 8 worlds x 4 stages = 32 stages: Overworld, Caverns, Skyways (floating islands + moving platforms),
  and a Fortress (lava, rotating fire bars, low ceilings) ending in a boss fight.
- Each world has its own colour palette and name; difficulty scales by world
  (wider gaps, more and tougher enemies, longer and faster fire bars, tougher boss).
- Power states: small -> Big (Berry) -> Ember (throw bouncing fireballs). Getting hit drops one level.
- Enemies: Blob (stompable), Spiky (can't be stomped; turns at ledges), Hopper (jumps at you),
  Bat (flies in a wave), and the Stone Warden boss.
- Mystery blocks, breakable bricks (when big), hidden items in bricks, gems (100 = extra life),
  heart 1-ups, stomp combos, checkpoint lanterns, time limit + time bonus, lives, continues.

## File layout

| File | Purpose |
|---|---|
| `Program.cs` | Entry point |
| `Game.cs` | Main loop, state machine, level loading, collisions, scoring |
| `GameDraw.cs` | HUD, title, intro, pause, game-over and victory screens |
| `Level.cs` | Tile grid, palettes, background parallax, tile drawing |
| `LevelGenerator.cs` | Seeded generator that builds all 32 stages |
| `Player.cs` | Movement physics, jumping, power-ups, hero drawing |
| `Entities.cs` | Entity base class and enemies |
| `Objects.cs` | Gems, power-ups, fireballs, rocks, platforms, fire bars, portal, checkpoint |
| `Boss.cs` | The Stone Warden |
| `Core.cs` | Tile collision, input, drawing helpers, particles |

## Tuning

- Jump/run feel: constants at the top of `Player.cs`.
- Level length and difficulty: `LevelGenerator.cs` (widths in `Build`, chunk weights in `BuildGround`,
  `BuildSky`, `BuildCastle`). Levels are seeded by world/stage, so the same stage always looks the same.
- Want hand-made levels instead? Fill a `Level`'s `Tiles` array and `Spawns` list yourself and
  return it from `LevelGenerator.Build` for that world/stage.

## Known limitations

- No sound or music yet (raylib supports audio, so it can be added).
- The generator follows jump-reach rules, but not every one of the 32 stages has been play-tested,
  so some may be harder than intended or need tweaks.
