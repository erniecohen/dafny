#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DafnyDriver.Commands;

namespace Microsoft.Dafny;

/// <summary>
/// Informational B3-only metadata beside a produced library. Existing .doo/.dtr readers,
/// serialized formats and dependency trust rules do not consume this file.
/// </summary>
internal sealed record B3CompilationProvenance(string Backend, string Disposition,
  bool VerificationAttempted, int PreparedModuleCount, long CheckingUnitCount, string InventoryHash,
  string? Reason, string SourceProgram, B3CompilationInputProvenance[] Inputs, string ScopeBoundary) {
  internal static B3CompilationProvenance Capture(CliVerificationReceipt receipt, Program program,
    PreparedCliInputs inputs) => new("b3", receipt.Disposition.ToString(), receipt.VerificationAttempted,
      receipt.ModuleCount, receipt.UnitCount, receipt.InventoryHash, receipt.Reason, program.FullName,
      inputs.RootFiles.Select(file => new B3CompilationInputProvenance(file.Uri.ToString(),
        file.ShouldNotVerify, file.ShouldNotCompile)).ToArray(),
      "Current source verification policy only. Trusted .doo inputs, source-library exclusions and " +
      "existing verification attributes are not new proofs. This file grants no library trust and " +
      "contains no observed solver version or end-to-end soundness certificate.");

  internal async Task WriteLibraryAsync(string libraryPath, CancellationToken cancellationToken) {
    await using var library = new FileStream(libraryPath, FileMode.Open, FileAccess.Read, FileShare.Read,
      65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
    var length = library.Length;
    var digest = await SHA256.HashDataAsync(library, cancellationToken);
    if (library.Position != length || library.Length != length) {
      throw new IOException("The produced library changed while recording informational provenance");
    }
    var document = new {
      InformationalOnly = true,
      Format = "dafny-b3-compilation-info-1",
      Library = new { Bytes = length, Sha256 = Convert.ToHexString(digest).ToLowerInvariant() },
      Compilation = this
    };
    await File.WriteAllTextAsync(libraryPath + ".b3-verification.json",
      JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
  }
}

internal sealed record B3CompilationInputProvenance(string Uri, bool FilePolicyExcludesVerification,
  bool FilePolicyExcludesCompilation);
