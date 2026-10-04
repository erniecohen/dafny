using System.Runtime.Versioning;
using System.Text.Json;

namespace B3AlcGate;

// Separate future build-time StartupObject only. Existing Program.Main is unchanged.
// No native arguments, solver, cgroup or verification option can be forwarded here.
[SupportedOSPlatform("linux")]
internal static class UnavailableMetadataProgram {
  public static int Main(string[] arguments) {
    if (!OperatingSystem.IsLinux()) { Console.Error.WriteLine("Linux-only fixed nonproof controls."); return 2; }
    if (arguments.Length != 2 || arguments[0] != "--inputs" || !Path.IsPathFullyQualified(arguments[1])) {
      Console.Error.WriteLine("Expected exactly --inputs ABSOLUTE_JSON_PATH."); return 2;
    }
    // Loader/JIT work is not cancellable. A dedicated bounded background watchdog
    // terminates this disposable host on deadline; timeout never emits a passed receipt.
    using var completed = new ManualResetEventSlim(false);
    var watchdog = new Thread(() => { if (!completed.Wait(TimeSpan.FromSeconds(45))) { Environment.Exit(124); } }) {
      IsBackground = true, Name = "fixed-nonproof-control-watchdog"
    };
    watchdog.Start();
    try {
      using var json = NativeProofSmokeControls.ReadJson(arguments[1], 65536);
      var inputs = json.RootElement.Deserialize<UnavailableControlInputs>(new JsonSerializerOptions {
        PropertyNameCaseInsensitive = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
      }) ?? throw new InvalidDataException("Missing fixed nonproof inputs.");
      return UnavailableMetadataControls.Run(inputs);
    } catch (Exception exception) {
      Console.Error.WriteLine(exception.GetType().Name + ": " + exception.Message); return 1;
    } finally { completed.Set(); if (!watchdog.Join(TimeSpan.FromSeconds(1))) { Environment.Exit(125); } }
  }
}
