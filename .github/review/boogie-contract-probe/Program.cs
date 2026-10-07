using Microsoft.Boogie;
var options = new CommandLineOptions(Console.Out, new ConsolePrinter()) {
  RunningBoogieFromCommandLine = true
};
if (!options.Parse(args) || options.Files.Count == 0) { return 2; }
using var engine = ExecutionEngine.CreateWithoutSharedCache(options);
return await engine.ProcessFiles(Console.Out, options.Files) ? 0 : 1;
