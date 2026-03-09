using AIWarsIdle.PvPSim.Headless;

var options = SimulationOptions.Parse(args);
var simulator = new SimulationBatchRunner();
var reports = options.Compare
    ? simulator.RunComparison(options)
    : new[] { simulator.Run(options) };

SimulationReportPrinter.Print(reports, options);
