using System;
using System.Collections.Generic;
using DafnyCore.Options;

namespace Microsoft.Dafny;

public class CompilationData(
  ErrorReporter errorReporter,
  List<Include> includes,
  IList<Uri> rootSourceUris,
  ISet<Uri> alreadyVerifiedRoots,
  ISet<Uri> alreadyCompiledRoots) {
  public DafnyOptions Options => ErrorReporter.Options;
  public ErrorReporter ErrorReporter { get; } = errorReporter;
  public IList<Uri> RootSourceUris { get; } = rootSourceUris;

  public ISet<Uri> AlreadyVerifiedRoots { get; } = alreadyVerifiedRoots;
  public ISet<Uri> AlreadyCompiledRoots { get; } = alreadyCompiledRoots;

  public List<Include> Includes = includes;
  // TODO move to DocumentAfterParsing once that's used by the CLI
  [FilledInDuringResolution]
  public ISet<Uri> UrisToVerify;
  // TODO move to DocumentAfterParsing once that's used by the CLI
  [FilledInDuringResolution]
  public ISet<Uri> UrisToCompile;

  public TranslationRecord AlreadyTranslatedRecord { get; set; }

  /// <summary>
  /// Under --modular-termination, the termination contract of each module exported by an admitted
  /// .doo library, by module name.  A library built from this compilation records them as its imports.
  /// </summary>
  public IReadOnlyList<DafnyCore.ModularTerminationContractRef> ModularTerminationImports { get; set; } = [];

  /// <summary>
  /// Under --modular-termination, the trusted premises recorded by the admitted .doo libraries, with the
  /// library each comes from and its trust list (https://github.com/erniecohen/dafny/issues/211, M5b).
  /// </summary>
  public IReadOnlyList<(string Library, string TrustList, DafnyCore.ModularTerminationPremise Premise)>
    ModularTerminationPremises { get; set; } = [];
}