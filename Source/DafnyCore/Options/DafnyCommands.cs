using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using DafnyCore;
using Microsoft.Dafny.Plugins;

namespace Microsoft.Dafny;

public static class DafnyCommands {

  public static Argument<FileInfo> FileArgument { get; }
  public static Argument<List<FileInfo>> FilesArgument { get; }

  static DafnyCommands() {

    FileArgument = new Argument<FileInfo>("file", "Dafny input file or Dafny project file");
    FilesArgument = new("file", r => {
      return r.Tokens.Where(t => !string.IsNullOrEmpty(t.Value)).Select(t => new FileInfo(t.Value)).ToList();
    }, false, "Dafny input files and/or a Dafny project file");
  }

  public static readonly IReadOnlyList<Option> VerificationOptions = new Option[] {
    CommonOptionBag.AdditionalAxioms,
    CommonOptionBag.ProgressOption,
    CommonOptionBag.RelaxDefiniteAssignment,
    BoogieOptionBag.VerificationTimeLimit,
    CommonOptionBag.VerifyIncludedFiles,
    CommonOptionBag.ManualLemmaInduction,
    CommonOptionBag.ConsistentObligationChecks,
    BoogieOptionBag.SolverPath,
    BoogieOptionBag.ArithmeticSolver,
    CommonOptionBag.DisableNonLinearArithmetic,
    BoogieOptionBag.IsolateAssertions,
    BoogieOptionBag.BoogieArguments,
    BoogieOptionBag.LegacyBoogieLibraryCheck,
    CommonOptionBag.VerificationLogFormat,
    BoogieOptionBag.SolverResourceLimit,
    BoogieOptionBag.SolverOption,
    BoogieOptionBag.SolverOptionHelp,
    BoogieOptionBag.SolverPlugin,
    BoogieOptionBag.SolverLog,
    CommonOptionBag.JsonOutput,
    CommonOptionBag.JsonDiagnostics,
    BoogieOptionBag.VerificationErrorLimit,
    CommonOptionBag.DefaultFunctionOpacity,
    CommonOptionBag.WarnContradictoryAssumptions,
    CommonOptionBag.WarnRedundantAssumptions,
    CommonOptionBag.SuggestProofRefactoring,
    CommonOptionBag.AnalyzeProofs,
    CommonOptionBag.NoTimeStampForCoverageReport,
    CommonOptionBag.VerificationCoverageReport,
    CommonOptionBag.ExtractCounterexample,
    CommonOptionBag.ManualTriggerOption,
    CommonOptionBag.MapComprehensionDomainTriggers,
    CommonOptionBag.HeapFreeLambdaFacts,
    CommonOptionBag.FunctionExtensionalityAxiom,
    CommonOptionBag.InlinedBodyCallPermissions,
    CommonOptionBag.DetectWitnessMatchingLoops,
    CommonOptionBag.StatementExpressionConclusions,
    CommonOptionBag.QuantifiedCheckCallFacts,
    CommonOptionBag.TraitRankDecreases,
    CommonOptionBag.ShowProofObligationExpressions
  }.ToList();

  public static readonly IReadOnlyList<Option> TranslationOptions = new Option[] {
    BoogieOptionBag.NoVerify,
    BoogieOptionBag.HiddenNoVerify,
    CommonOptionBag.OptimizeErasableDatatypeWrapper,
    CommonOptionBag.CsReplacementTypes,
    CommonOptionBag.LegacyCsDatatypeEquality,
    CommonOptionBag.PythonReorderReplacements,
    CommonOptionBag.TestAssumptions,
    DeveloperOptionBag.Bootstrapping,
    CommonOptionBag.AddCompileSuffix,
    CommonOptionBag.SystemModule,
    IExecutableBackend.TranslationRecords,
    ModuleDefinition.LegacyModuleNames,
    CommonOptionBag.TranslateStandardLibrary,
  }.Concat(VerificationOptions).ToList();

  public static readonly IReadOnlyList<Option> ExecutionOptions = new Option[] {
    CommonOptionBag.Target,
    CommonOptionBag.ProjectOutput,
    CommonOptionBag.SpillTranslation,
    CommonOptionBag.InternalIncludeRuntimeOptionForExecution,
    CommonOptionBag.ExecutionCoverageReport
  }.Concat(TranslationOptions).ToList();

  public static readonly IReadOnlyList<Option> ConsoleOutputOptions = new List<Option>(new Option[] {
    Snippets.ShowSnippets,
    DeveloperOptionBag.PrintOption,
    DeveloperOptionBag.ResolvedPrint,
    DeveloperOptionBag.SplitPrint,
    DeveloperOptionBag.PassivePrint,
    DeveloperOptionBag.BoogiePrint,
    Printer.PrintMode,
    CommonOptionBag.AllowWarnings,
    CommonOptionBag.WarnAsErrors,
    CommonOptionBag.InputType
  });

  public static readonly IReadOnlyList<Option> ParserOptions = new List<Option>(new Option[] {
    CommonOptionBag.StdIn,
    CommonOptionBag.Verbose,
    BoogieOptionBag.Cores,
    CommonOptionBag.Libraries,
    CommonOptionBag.AllowDeprecation,
    CommonOptionBag.PluginOption,
    CommonOptionBag.Prelude,
    Function.FunctionSyntaxOption,
    CommonOptionBag.QuantifierSyntax,
    CommonOptionBag.UnicodeCharacters,
    CommonOptionBag.RawPointers,
    CommonOptionBag.UseBaseFileName,
    CommonOptionBag.EmitUncompilableCode,
    CommonOptionBag.GeneralTraits,
    CommonOptionBag.GeneralNewtypes,
    CommonOptionBag.ExtendedNewtypeBases,
    CommonOptionBag.MemberInvariants,
    CommonOptionBag.TypeSystemRefresh,
    CommonOptionBag.ResolutionThreads,
    CommonOptionBag.UserDefinedOperators,
    CommonOptionBag.BitvectorMapKeys,
    CommonOptionBag.ContextualLambdaDomains,
    CommonOptionBag.ContextualQuantifierDomains,
    CommonOptionBag.ConservativeMatchInference,
    CommonOptionBag.DatatypeSubsetConversions,
    CommonOptionBag.AncestorQualifiedModuleIds,
    CommonOptionBag.SynonymRefinesAbstractType,
    CommonOptionBag.RefinementRenaming,
    CommonOptionBag.CopyImports,
    CommonOptionBag.DecreasesOrders,
    CommonOptionBag.LegacyDecreasesOrder,
    CommonOptionBag.LegacyReadingFunctionsInDatatypes,
    CommonOptionBag.LegacyNoncanonicalMapPayloads,

    CommonOptionBag.LegacyUnguardedChar,
    CommonOptionBag.LegacyAutoGeneratedAttributes,
    CommonOptionBag.LegacyFirstPhaseConstReads,
    CommonOptionBag.LegacyAllocatedInLambdas,
    CommonOptionBag.LegacyElidedOverrideChecks,
    CommonOptionBag.LegacySkeletonModifyingCalls,
    CommonOptionBag.LegacyInheritedConstructorFields,
    CommonOptionBag.LegacyFunctionStoreLocations,
    CommonOptionBag.LegacyEarlyYieldParameterCheck,
    CommonOptionBag.LegacyReplacementCycles,
    CommonOptionBag.LegacyGenericFunctionStores,
    CommonOptionBag.LegacyInheritedTerminationChecks,
    CommonOptionBag.LegacyUncheckedImportBinding,
    CommonOptionBag.LegacyStoredFunctionCallbacks,
    CommonOptionBag.LegacyInheritedNameCapture,
    CommonOptionBag.UncheckedInvariantExtension,
    CommonOptionBag.ModularTermination,
    CommonOptionBag.ModularTerminationTrust,
    CommonOptionBag.InvariantConstructorInference,
    CommonOptionBag.TypeInferenceDebug,
    CommonOptionBag.NewTypeInferenceDebug,
    Method.ReadsClausesOnMethods,
    CommonOptionBag.UseStandardLibraries,
    CommonOptionBag.LogLevelOption,
    CommonOptionBag.LogLocation,
    CommonOptionBag.CheckSourceLocationConsistency,
    CommonOptionBag.Referrers
  });

  /// <summary>
  /// The options that a command which loads libraries but does not verify (resolve, audit, doc, extract and
  /// format) adds to its own.  Under --modular-termination, admitting a library checks the Z3 binary that
  /// verified it, so such a command must be able to choose that binary as dafny verify does
  /// (https://github.com/erniecohen/dafny/issues/358).  The verifying commands have --solver-path in
  /// VerificationOptions.
  /// </summary>
  public static readonly IReadOnlyList<Option> LibraryAdmissionOptions = new Option[] {
    BoogieOptionBag.SolverPath
  };

  public static IReadOnlyList<Option> ResolverOptions = new List<Option>(new Option[] {
    CommonOptionBag.PipelineProfile,
    CommonOptionBag.PipelineProfileHeap,
    CommonOptionBag.PipelineProfileGcEvents,
    CommonOptionBag.PipelineProfileHold,
    CommonOptionBag.ShowHints,
    CommonOptionBag.WarnShadowing,
    CommonOptionBag.WarnMissingConstructorParenthesis,
    PrintStmt.TrackPrintEffectsOption,
    CommonOptionBag.AllowAxioms,
    CommonOptionBag.WarnUncheckedTermination,
    CommonOptionBag.LegacyNoUncheckedTerminationWarning,
    CommonOptionBag.EnforceDeterminism,
    MethodOrFunction.AllowExternalContracts,
    DafnyProject.FindProjectOption,
    CommonOptionBag.PrintDiagnosticsRanges,
    CommonOptionBag.WaitForDebugger,
    CommonOptionBag.IgnoreIndentation
  }).Concat(ParserOptions).ToList();
}
