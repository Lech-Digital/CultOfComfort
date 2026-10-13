using System.Diagnostics;
using CultOfComfort.Sandbox;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;

// Usage: dotnet run --project tools/CultOfComfort.Sandbox -- [--seed N] [--budget N] [--waves N] [--no-sluice] [--out path] [--open]
//        dotnet run --project tools/CultOfComfort.Sandbox -- pace [errands|builder|rush] [--hours N] [--verbose]
if (args.FirstOrDefault() == "pace")
{
    PaceBot.Run(args.Skip(1).ToArray());
    return;
}

var seed = ulong.Parse(ArgValue("--seed") ?? "947");
var budget = int.Parse(ArgValue("--budget") ?? "54");
var waves = int.Parse(ArgValue("--waves") ?? "4");
var output = ArgValue("--out") ?? Path.Combine("artifacts", $"night-{seed}.html");
var useSluice = !args.Contains("--no-sluice");

var layout = DemoScenario.CreateLayout();
var setup = new BattleSetup(layout, seed, budget, waves, []);

Console.WriteLine("CULT OF COMFORT — Night Sandbox");
Console.WriteLine($"seed {seed} · budget {budget} · {waves} waves · balance {BalanceTable.Default.Version} · layout {layout.ComputeHash():x16}");
Console.WriteLine();

// 1. Client: play the Night live, recording frames and the input log.
var battle = new Battle(setup);
var recorder = new ReplayRecorder();
PrintMap(layout, battle.Path);

while (!battle.IsFinished)
{
    if (useSluice)
    {
        DemoScenario.AutoPlay(battle);
    }

    battle.Step();
    recorder.Capture(battle);
}

var result = battle.Result!;
Console.WriteLine($"Outcome   : {result.Outcome} {new string('★', result.Stars)}{new string('☆', 3 - result.Stars)}");
Console.WriteLine($"Hearth    : {result.HearthHealth}/{result.HearthMaxHealth}");
Console.WriteLine($"Enemies   : {result.EnemiesDefeated} defeated, {result.EnemiesLeaked} reached the Hearth (of {battle.TotalEnemies})");
Console.WriteLine($"Duration  : {result.TicksElapsed / (double)BalanceTable.TicksPerSecond:0.0}s ({result.TicksElapsed} ticks)");
Console.WriteLine($"Input log : {(battle.InputLog.Count == 0 ? "(none)" : string.Join(", ", battle.InputLog.Select(i => $"{i.Type}@{i.Tick}")))}");
Console.WriteLine($"State hash: {result.StateHashHex}");
Console.WriteLine();

// 2. Server: replay from the submitted input log only, then try a tampered log.
var replayed = Battle.Simulate(setup with { Inputs = battle.InputLog.ToList() });
var match = replayed == result;
Console.WriteLine($"Server replay   : {replayed.StateHashHex} → {(match ? "MATCH, rewards granted" : "MISMATCH, server result wins")}");

string? tamperedHash = null;
if (battle.InputLog.Count > 0)
{
    var tamperedLog = battle.InputLog.Select((input, i) => i == 0 ? input with { Tick = Math.Max(0, input.Tick - 20) } : input).ToList();
    var tampered = Battle.Simulate(setup with { Inputs = tamperedLog });
    tamperedHash = tampered.StateHashHex;
    Console.WriteLine($"Tampered replay : {tamperedHash} → {(tampered.StateHash == result.StateHash ? "undetected" : "detected (first sluice moved 1s earlier)")}");
}

// 3. Bake the replay into the HTML viewer.
var template = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "viewer.html"));
var html = recorder.BuildHtml(template, layout, battle, setup, new ServerVerdict(replayed.StateHashHex, match, tamperedHash));
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output, html);
Console.WriteLine();
Console.WriteLine($"Replay viewer: {Path.GetFullPath(output)}");

if (args.Contains("--open"))
{
    Process.Start(new ProcessStartInfo(Path.GetFullPath(output)) { UseShellExecute = true });
}

string? ArgValue(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static void PrintMap(BattleLayout layout, IReadOnlyList<GridPosition> path)
{
    var onPath = path.ToHashSet();
    for (var y = 0; y < layout.Height; y++)
    {
        var row = new char[layout.Width];
        for (var x = 0; x < layout.Width; x++)
        {
            var p = new GridPosition(x, y);
            var tower = layout.Towers.FirstOrDefault(t => t.Position == p);
            row[x] = p == layout.Spawn ? 'S'
                : p == layout.Hearth ? 'H'
                : layout.Obstacles.Contains(p) ? '#'
                : tower != default ? (tower.Type == TowerType.SlurryChute ? 'C' : 'F')
                : onPath.Contains(p) ? '·'
                : ' ';
        }

        Console.WriteLine("  " + string.Join(' ', row));
    }

    Console.WriteLine($"  S spawn · H Hearth · C Slurry Chute · F Sapling Flinger · # rock · path {path.Count - 1} tiles");
    Console.WriteLine();
}
