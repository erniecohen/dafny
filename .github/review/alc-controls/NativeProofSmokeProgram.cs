using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace B3AlcGate;

// Fixed CI-only build entrypoint. The ordinary Program and all nineteen qualified
// lifecycle files remain unchanged. No caller-provided Dafny argument is forwarded.
[SupportedOSPlatform("linux")]
internal static class NativeProofSmokeProgram {
  public static int Main(string[] args) {
    try {
      NativeProofSmokeControls.Require(OperatingSystem.IsLinux() && args.Length == 2 && args[0] == "--inputs",
        "fixed-ci-smoke-input-file-required");
      var options = new JsonSerializerOptions {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16
      };
      var input = JsonSerializer.Deserialize<ProofSmokeInputs>(NativeProofSmokeControls.ReadBytes(args[1], 65536), options)
        ?? throw new NativeProofSmokeControls.SmokeFailure("fixed-ci-inputs-empty");
      return NativeProofSmokeControls.Run(input);
    } catch (Exception error) {
      Console.Error.WriteLine("Fixed native proof smoke: " + error.GetType().Name + ": " + error.Message);
      return 1;
    }
  }
}
