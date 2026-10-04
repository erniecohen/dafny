using System;

namespace Microsoft.Dafny;

/// <summary>Construct only the selected backend; B3 never invokes the Boogie engine factory.</summary>
public static class VerificationBackendFactory {
  public static IVerificationBackend Create(DafnyOptions options, Func<IVerificationBackend> createBoogie) =>
    options.GetOrOptionDefault(B3OptionBag.VerificationBackend) switch {
      B3OptionBag.Backend.Boogie => createBoogie(),
      B3OptionBag.Backend.B3 => new B3VerificationBackend(options),
      _ => throw new ArgumentException("Unknown verification backend")
    };
}
