using System.Security.Cryptography;
using System.Text.Json;
using DafnyDriver.Commands;
using Microsoft.Dafny;
using static DafnyDriver.Test.ModernB3TestSupport;

namespace DafnyDriver.Test;

[Collection("Modern B3 CLI")]
public class ModernB3ProvenanceTest {
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task InformationalSidecarBindsActualArtifactWithoutChangingItOrGrantingTrust(bool skip) {
    using var source = new SourceFile();
    var options = Options();
    options.CliRootSourceUris.Add(new Uri(source.Path));
    if (skip) { Skip(options); }
    var (_, inputs) = await PreparedCliInputs.PrepareAsync(options, CancellationToken.None);
    var program = await Resolve(options);
    CliVerificationReceipt receipt;
    if (skip) { receipt = CliVerificationLedger.Disabled(program); }
    else {
      var module = Owner(program).ContainingModule;
      var ledger = new CliVerificationLedger(program, new[] { module }, Array.Empty<ICanVerify>());
      ledger.BeginPreparation(module);
      ledger.CompletePreparation(module, Array.Empty<IVerificationWorkItem>());
      ledger.ReleaseModule(module);
      receipt = ledger.Seal(true, CancellationToken.None);
    }
    var artifact = Path.Combine(source.Directory, "compiled.doo");
    var bytes = new byte[] { 1, 2, 3, 4, 5 }; // Metadata writer control; not a valid .doo or a proved library.
    File.WriteAllBytes(artifact, bytes);
    await B3CompilationProvenance.Capture(receipt, program, inputs!).WriteLibraryAsync(artifact, CancellationToken.None);
    Assert.Equal(bytes, File.ReadAllBytes(artifact));
    using var document = JsonDocument.Parse(File.ReadAllText(artifact + ".b3-verification.json"));
    var root = document.RootElement;
    Assert.True(root.GetProperty("InformationalOnly").GetBoolean());
    Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
      root.GetProperty("Library").GetProperty("Sha256").GetString());
    var compilation = root.GetProperty("Compilation");
    Assert.Equal(skip ? "Disabled" : "CompleteEmpty", compilation.GetProperty("Disposition").GetString());
    Assert.Equal(!skip, compilation.GetProperty("VerificationAttempted").GetBoolean());
    Assert.Equal(0, compilation.GetProperty("CheckingUnitCount").GetInt64());
    Assert.Contains("grants no library trust", compilation.GetProperty("ScopeBoundary").GetString());
    Assert.False(compilation.TryGetProperty("SolverVersion", out _));
  }
}
