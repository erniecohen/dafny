using System.CommandLine;
using System.IO;

namespace Microsoft.Dafny;

public static class B3OptionBag {
  public enum Backend { Boogie, B3 }
  public static readonly Option<Backend> VerificationBackend = new("--verification-backend", () => Backend.Boogie,
    "Select the verification backend: boogie (default) or experimental b3 for verify, modern build/run/test, and editor verification. B3 build/run/test require complete current-scope verification before compilation; ordinary --no-verify records that no proof was attempted. B3 translate and legacy compilation, unsupported constructs, and unsupported options fail explicitly.");
  public static readonly Option<FileInfo> Worker = new("--b3-worker",
    "Path to the packaged DafnyB3Host.dll and its adjacent build manifest. The default is b3/DafnyB3Host.dll beside Dafny.");

  static B3OptionBag() {
    OptionRegistry.RegisterOption(VerificationBackend, OptionScope.Cli);
    OptionRegistry.RegisterOption(Worker, OptionScope.Cli);
  }
}
