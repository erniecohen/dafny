using System.Globalization;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace B3AlcGate;

internal sealed record Control(string Name, string[] Arguments, int ExpectedExitCode, string RequiredOutput);
internal sealed record Fixtures(int SchemaVersion, Control[] Controls);
internal sealed record Receipt(int SchemaVersion, string Scope, string SourceManifestSha256,
  string HarnessAssemblySha256, int VisibleProcessors, string WorkingDirectory, string Culture, string UICulture,
  FrameworkWitness CommonFramework, string[] DefaultAssembliesAtStart, RunReceipt[] Runs, bool ControlsPassed);

internal static class Program {
  private static readonly JsonSerializerOptions Json = new() {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = true,
    MaxDepth = 16
  };

  public static int Main(string[] arguments) {
    var runs = new List<RunReceipt>();
    try {
      var options = Options(arguments);
      var fixturePath = options.GetValueOrDefault("--fixtures", Path.Combine(AppContext.BaseDirectory, "control-fixtures.json"));
      if (new FileInfo(fixturePath).Length > 8192) {
        throw new IOException("The static control fixture exceeded its bound.");
      }
      var fixtures = JsonSerializer.Deserialize<Fixtures>(File.ReadAllText(fixturePath), Json)
        ?? throw new InvalidDataException("No control fixtures.");
      if (fixtures.SchemaVersion != 1 || fixtures.Controls.Length != 2 ||
          !fixtures.Controls.Select(c => c.Name).SequenceEqual(["help", "malformed-command"]) ||
          fixtures.Controls.Any(c => c.ExpectedExitCode != (c.Name == "help" ? 0 : 1) || string.IsNullOrEmpty(c.RequiredOutput))) {
        throw new InvalidDataException("The two fixed non-verifying controls are required.");
      }
      var framework = Framework.Witness();
      var initial = AssemblyLoadContext.Default.Assemblies.Select(a => a.FullName ?? "").Order(StringComparer.Ordinal).ToArray();
      var products = new[] { new Product("baseline", options["--baseline"]), new Product("candidate", options["--candidate"]) };
      var passed = true;
      // Serial execution is intentional. Do not begin the next context until the previous
      // weak reference is dead and the host owns no residual child process.
      foreach (var product in products) {
        foreach (var control in fixtures.Controls) {
          var run = Runs.RunControl(product, control.Name, control.Arguments);
          runs.Add(run);
          if (run.Failure != null || run.ExitCode != control.ExpectedExitCode ||
              !(run.Output + run.ErrorOutput).Contains(control.RequiredOutput, StringComparison.Ordinal)) {
            passed = false;
            goto complete;
          }
        }
      }
      complete:
      var receipt = new Receipt(1, "prototype/non-verifying-controls",
        Framework.Sha256(Path.Combine(AppContext.BaseDirectory, "source-manifest.json")),
        Framework.Sha256(typeof(Program).Assembly.Location), Environment.ProcessorCount, Environment.CurrentDirectory,
        CultureInfo.CurrentCulture.Name, CultureInfo.CurrentUICulture.Name, framework, initial, runs.ToArray(), passed);
      var json = JsonSerializer.Serialize(receipt, Json);
      if (json.Length > 4194304) {
        throw new IOException("The control receipt exceeded its bound.");
      }
      File.WriteAllText(options["--receipt"], json + "\n");
      Console.WriteLine(passed ? "Four non-verifying isolation controls passed." : "Isolation control failed; no later control ran.");
      return passed ? 0 : 1;
    } catch (Exception exception) {
      Console.Error.WriteLine(exception.GetType().FullName + ": " + exception.Message);
      return 2;
    }
  }

  private static Dictionary<string, string> Options(string[] arguments) {
    if (arguments.Length is < 6 or > 8 || arguments.Length % 2 != 0) {
      throw new ArgumentException("Use --baseline DIR --candidate DIR --receipt PATH [--fixtures PATH]. Verification is not a CLI mode.");
    }
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var index = 0; index < arguments.Length; index += 2) {
      if (arguments[index] is not ("--baseline" or "--candidate" or "--receipt" or "--fixtures") ||
          string.IsNullOrWhiteSpace(arguments[index + 1]) || !options.TryAdd(arguments[index], arguments[index + 1])) {
        throw new ArgumentException("Unknown, missing, or duplicate prototype option.");
      }
    }
    foreach (var required in new[] { "--baseline", "--candidate", "--receipt" }) {
      if (!options.ContainsKey(required)) {
        throw new ArgumentException("Missing " + required);
      }
    }
    return options;
  }
}
