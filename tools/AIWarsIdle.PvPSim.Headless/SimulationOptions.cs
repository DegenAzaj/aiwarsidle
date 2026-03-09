namespace AIWarsIdle.PvPSim.Headless;

internal enum SimulationVariant
{
    Baseline,
    NoUnderdog,
    NoMaintenance,
    NoUnderdogNoMaintenance
}

internal sealed class SimulationOptions
{
    public int Runs { get; private set; } = 2000;
    public int DurationMinutes { get; private set; } = 180;
    public int StepSeconds { get; private set; } = 60;
    public int Radius { get; private set; } = 3;
    public int Factions { get; private set; } = 4;
    public int Seed { get; private set; } = 12345;
    public bool Compare { get; private set; } = true;
    public SimulationVariant Variant { get; private set; } = SimulationVariant.Baseline;

    public static SimulationOptions Parse(string[] args)
    {
        var options = new SimulationOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            var next = i + 1 < args.Length ? args[i + 1] : null;

            switch (arg)
            {
                case "--runs":
                    options.Runs = ParsePositiveInt(arg, next);
                    i++;
                    break;
                case "--duration-minutes":
                    options.DurationMinutes = ParsePositiveInt(arg, next);
                    i++;
                    break;
                case "--step-seconds":
                    options.StepSeconds = ParsePositiveInt(arg, next);
                    i++;
                    break;
                case "--radius":
                    options.Radius = ParsePositiveInt(arg, next);
                    i++;
                    break;
                case "--factions":
                    options.Factions = ParseRangeInt(arg, next, min: 2, max: 6);
                    i++;
                    break;
                case "--seed":
                    options.Seed = ParseInt(arg, next);
                    i++;
                    break;
                case "--variant":
                    options.Variant = ParseVariant(next);
                    options.Compare = false;
                    i++;
                    break;
                case "--no-compare":
                    options.Compare = false;
                    break;
                case "--compare":
                    options.Compare = true;
                    break;
                case "--help":
                case "-h":
                    PrintHelpAndExit();
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        if (options.StepSeconds > (options.DurationMinutes * 60))
        {
            throw new ArgumentException("--step-seconds must be <= total duration.");
        }

        return options;
    }

    private static int ParsePositiveInt(string name, string? raw)
    {
        var value = ParseInt(name, raw);
        if (value <= 0) throw new ArgumentException($"{name} must be > 0.");
        return value;
    }

    private static int ParseRangeInt(string name, string? raw, int min, int max)
    {
        var value = ParseInt(name, raw);
        if (value < min || value > max) throw new ArgumentException($"{name} must be in range {min}..{max}.");
        return value;
    }

    private static int ParseInt(string name, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) throw new ArgumentException($"{name} requires a value.");
        if (!int.TryParse(raw, out var value)) throw new ArgumentException($"{name} requires an integer value.");
        return value;
    }

    private static SimulationVariant ParseVariant(string? raw)
    {
        return raw?.ToLowerInvariant() switch
        {
            "baseline" => SimulationVariant.Baseline,
            "no-underdog" => SimulationVariant.NoUnderdog,
            "no-maintenance" => SimulationVariant.NoMaintenance,
            "no-both" => SimulationVariant.NoUnderdogNoMaintenance,
            _ => throw new ArgumentException("--variant must be one of: baseline, no-underdog, no-maintenance, no-both")
        };
    }

    private static void PrintHelpAndExit()
    {
        Console.WriteLine("AIWarsIdle PvP headless simulator");
        Console.WriteLine("Options:");
        Console.WriteLine("  --runs <n>");
        Console.WriteLine("  --duration-minutes <n>");
        Console.WriteLine("  --step-seconds <n>");
        Console.WriteLine("  --radius <n>");
        Console.WriteLine("  --factions <2..6>");
        Console.WriteLine("  --seed <n>");
        Console.WriteLine("  --variant <baseline|no-underdog|no-maintenance|no-both>");
        Console.WriteLine("  --compare / --no-compare");
        Environment.Exit(0);
    }
}
