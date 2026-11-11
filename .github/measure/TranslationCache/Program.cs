using System.Diagnostics;
using System.Text.Json;
using DafnyDriver.Commands;
using Microsoft.Dafny;

// Translation-only benchmark. No Boogie preparation or solver process is requested.
// Each invocation has its own process-local table. Run each scenario in five fresh
// processes. Supply the Std source directory (or a directory of multi-module inputs).
if (args.Length != 2 || args[1] is not ("cold" or "warm" or "edit")) {
  Console.Error.WriteLine("usage: TranslationCache <source-directory> cold|warm|edit");
  return 2;
}
var root = Path.GetFullPath(args[0]);
var scenario = args[1];
var sources = Directory.GetFiles(root, "*.dfy", SearchOption.AllDirectories)
  .Where(p => !p.Contains(Path.DirectorySeparatorChar + "TargetSpecific" + Path.DirectorySeparatorChar))
  .Order(StringComparer.Ordinal).ToArray();
var cache = new ModuleTranslationCache();
var translatedModules = new List<string>();
var scratch = Path.Combine(Path.GetTempPath(), "dafny-translation-measure-" + Guid.NewGuid());
Directory.CreateDirectory(scratch);
var extra = Path.Combine(scratch, "unrelated.dfy");
await File.WriteAllTextAsync(extra, "module Unrelated { function F(x: int): int { x + 1 } }\n");
try {
  if (scenario != "cold") { await Run(); }
  if (scenario == "edit") {
    await File.WriteAllTextAsync(extra, "module Unrelated { function F(x: int): int { x + 2 } }\n");
  }
  var hits = cache.Hits;
  var translations = cache.Translations;
  var bypasses = cache.Bypasses;
  var cpu = Process.GetCurrentProcess().TotalProcessorTime;
  var watch = Stopwatch.StartNew();
  var modules = await Run();
  watch.Stop();
  cpu = Process.GetCurrentProcess().TotalProcessorTime - cpu;
  Console.WriteLine(JsonSerializer.Serialize(new {
    scenario, modules, wallSeconds = watch.Elapsed.TotalSeconds, cpuSeconds = cpu.TotalSeconds,
    solverCalls = 0, solverVersionProbes = 0, hits = cache.Hits - hits, translations = cache.Translations - translations,
    bypasses = cache.Bypasses - bypasses, cache.LastBypassReason, cache.EntryCount, cache.RetainedTextBytes,
    translatedModules
  }));
} finally {
  Directory.Delete(scratch, true);
}
return 0;

async Task<int> Run() {
  translatedModules.Clear();
  var diagnostics = new StringWriter();
  var options = new DafnyOptions(TextReader.Null, diagnostics, diagnostics);
  options.ApplyDefaultOptionsWithoutSettingsDefault();
  options.Set(CommonOptionBag.UnicodeCharacters, true);
  options.Set(CommonOptionBag.ResolutionThreads, 1);
  options.Set(CommonOptionBag.TranslationCache, true);
  options.Set(CommonOptionBag.TypeSystemRefresh, true);
  options.Set(CommonOptionBag.GeneralTraits, CommonOptionBag.GeneralTraitsOptions.Datatype);
  options.Set(CommonOptionBag.GeneralNewtypes, true);
  options.Set(Method.ReadsClausesOnMethods, true);
  options.EnforcePrintEffects = true;
  options.ForbidNondeterminism = true;
  foreach (var source in sources.Append(extra)) { options.CliRootSourceUris.Add(new Uri(source)); }
  var cli = CliCompilation.Create(options);
  using var compilation = cli.Compilation;
  // Resolution normally probes the solver version. This benchmark stops before
  // preparation and explicitly disables that probe, so no solver binary is needed.
  compilation.ShouldProcessSolverOptions = false;
  cli.Start();
  var resolution = await cli.Resolution;
  if (resolution == null || resolution.HasErrors) { throw new Exception(diagnostics.ToString()); }
  var program = resolution.ResolvedProgram;
  var modules = BoogieGenerator.VerifiableModules(program).ToArray();
  await DafnyMain.LargeStackFactory.StartNew(() => {
    Microsoft.Dafny.Type.ResetScopes();
    foreach (var module in modules) {
      var flags = new BoogieGenerator.TranslatorFlags(options) { PreludeText = program.PreludeText };
      cache.Translate(program, module, flags, default, out var reused);
      if (!reused) { translatedModules.Add(module.FullName); }
    }
  });
  return modules.Length;
}
