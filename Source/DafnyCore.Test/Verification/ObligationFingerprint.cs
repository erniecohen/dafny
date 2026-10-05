using Microsoft.Boogie;

namespace DafnyCore.Test.Verification;

// Emit the actual Boogie input. No Lit, boxing, can-call, layer, guard, role,
// scope marker or trigger information is erased by this diagnostic fingerprint.
internal static class ObligationFingerprint {
  public static string Emit(IEnumerable<Program> programs) {
    var result = new StringWriter();
    foreach (var program in programs) {
      using var writer = new TokenTextWriter(result, Microsoft.Dafny.DafnyOptions.Default);
      program.Emit(writer);
    }
    return result.ToString();
  }
}
