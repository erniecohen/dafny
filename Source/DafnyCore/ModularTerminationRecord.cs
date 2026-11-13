#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Dafny;

namespace DafnyCore;

/// <summary>
/// The [modular_termination] table of a .doo manifest built under --modular-termination
/// (https://github.com/erniecohen/dafny/issues/211, milestones 3 and 4).  It records that the
/// library's own program was completely verified by this build of Dafny, under the recorded
/// semantic options, and the public termination contract of each top-level module, both the ones
/// the library exports and the ones it was verified against.
/// </summary>
public class ModularTerminationManifest {
  public const long CurrentSchema = 1;
  public const long CurrentContractSchema = 1;
  public const string CompleteStatus = "complete";

  public long Schema { get; set; }
  public bool Enabled { get; set; }
  public string Status { get; set; } = null!;
  public long ContractSchema { get; set; }
  public string ForkBuildId { get; set; } = null!;
  public string ProgramSha256 { get; set; } = null!;
  public string SemanticOptionsSha256 { get; set; } = null!;
  public string ContractsSha256 { get; set; } = null!;
  public Dictionary<string, string>? SemanticOptions { get; set; }
  public List<ModularTerminationContractRef>? Exports { get; set; }
  public List<ModularTerminationContractRef>? Imports { get; set; }
}

public class ModularTerminationContractRef {
  public string Module { get; set; } = null!;
  public string ContractSha256 { get; set; } = null!;

  public ModularTerminationContractRef() { }

  public ModularTerminationContractRef(string module, string contractSha256) {
    Module = module;
    ContractSha256 = contractSha256;
  }
}

/// <summary>
/// The evidence that a --modular-termination library build verified its own program completely.
/// It is never serialized; LibraryBackend turns it into a record only when Failure is null.
/// </summary>
public sealed class ModularTerminationCompletion {
  public string? Failure { get; private init; }
  public string ProgramText { get; private init; } = null!;
  public ModularTerminationContracts Contracts { get; private init; } = null!;
  public IReadOnlyList<ModularTerminationContractRef> Imports { get; private init; } = null!;
  public SortedDictionary<string, string> SemanticOptions { get; private init; } = null!;

  public static ModularTerminationCompletion Failed(string failure) => new() { Failure = failure };

  public static ModularTerminationCompletion Complete(string programText, ModularTerminationContracts contracts,
    IReadOnlyList<ModularTerminationContractRef> imports, SortedDictionary<string, string> semanticOptions) =>
    new() {
      ProgramText = programText,
      Contracts = contracts,
      Imports = imports,
      SemanticOptions = semanticOptions
    };

  public ModularTerminationManifest CreateRecord() {
    return new ModularTerminationManifest {
      Schema = ModularTerminationManifest.CurrentSchema,
      Enabled = true,
      Status = ModularTerminationManifest.CompleteStatus,
      ContractSchema = ModularTerminationManifest.CurrentContractSchema,
      ForkBuildId = ModularTerminationRecords.ForkBuildId,
      ProgramSha256 = ModularTerminationRecords.Sha256(ProgramText),
      SemanticOptionsSha256 = ModularTerminationRecords.SemanticOptionsSha256(SemanticOptions),
      ContractsSha256 = ModularTerminationRecords.Sha256(Contracts.EntryText),
      SemanticOptions = new Dictionary<string, string>(SemanticOptions),
      Exports = Contracts.Exports.ToList(),
      Imports = Imports.ToList()
    };
  }
}

/// <summary>
/// The public termination contract of each top-level module of a retained program: the module
/// printed with method, lemma, iterator and by-method bodies omitted.  Function bodies and constant
/// definitions stay, since a client can reveal or unfold them.
/// </summary>
public sealed class ModularTerminationContracts {
  public const string DefaultModuleKey = "_default";

  private static readonly JsonSerializerOptions Json = new() {
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
  };

  public SortedDictionary<string, string> Interfaces { get; }
  public string EntryText { get; }
  public IReadOnlyList<ModularTerminationContractRef> Exports { get; }

  public ModularTerminationContracts(SortedDictionary<string, string> interfaces) {
    Interfaces = interfaces;
    Exports = interfaces.Select(kv => new ModularTerminationContractRef(kv.Key, ModularTerminationRecords.Sha256(kv.Value))).ToList();
    var sb = new StringBuilder();
    sb.Append("{\n  \"schema\": ").Append(ModularTerminationManifest.CurrentContractSchema).Append(",\n  \"contracts\": [");
    var first = true;
    foreach (var (module, text) in interfaces) {
      sb.Append(first ? "\n" : ",\n");
      first = false;
      sb.Append("    {\"module\": ").Append(JsonSerializer.Serialize(module, Json))
        .Append(", \"contract_sha256\": \"").Append(ModularTerminationRecords.Sha256(text))
        .Append("\", \"interface\": ").Append(JsonSerializer.Serialize(text, Json)).Append('}');
    }
    sb.Append(first ? "]\n}\n" : "\n  ]\n}\n");
    EntryText = sb.ToString();
  }

  /// <summary>
  /// Parses a retained program and computes its contracts.  Returns null, with the parse errors in
  /// "errors", when the text does not parse.
  /// </summary>
  public static async Task<(ModularTerminationContracts? Contracts, string? Error)> Compute(DafnyOptions options,
    string programText, Uri uri) {
    var parseOptions = new DafnyOptions(options, true) {
      DafnyPrintFile = null
    };
    var reporter = new BatchErrorReporter(parseOptions);
    var parsed = await ProgramParser.Parse(programText, uri, reporter);
    if (reporter.HasErrors) {
      var first = reporter.AllMessagesByLevel[ErrorLevel.Error].First();
      return (null, first.Message);
    }
    var program = parsed.Program;
    var interfaces = new SortedDictionary<string, string>(StringComparer.Ordinal);

    void Add(string key, IEnumerable<TopLevelDecl> decls, IEnumerable<IOrigin>? prefixIds) {
      var wr = new StringWriter { NewLine = "\n" };
      var printer = new Printer(wr, parseOptions, PrintModes.Everything, new PrintFlags(InterfaceOnly: true));
      printer.PrintTopLevelDecls(program.Compilation, decls, 0, prefixIds);
      var text = wr.ToString();
      if (text.Trim().Length == 0) {
        return;
      }
      interfaces[key] = interfaces.TryGetValue(key, out var previous) ? previous + text : text;
    }

    var def = program.DefaultModuleDef;
    Add(DefaultModuleKey, def.TopLevelDecls.Where(d => d is not ModuleDecl), null);
    foreach (var md in def.TopLevelDecls.OfType<ModuleDecl>()) {
      Add(md.Name, [md], null);
    }
    foreach (var prefixed in def.PrefixNamedModules) {
      var key = string.Concat(prefixed.Parts.Select(p => p.val + ".")) + prefixed.Module.Name;
      Add(key, [prefixed.Module], prefixed.Parts);
    }
    return (new ModularTerminationContracts(interfaces), null);
  }
}

/// <summary>
/// Decides whether a library build under --modular-termination verified its own program completely.
/// The expected inventory is every Boogie implementation generated for the program; each must have
/// been reported by the verifier, with every one of its verification conditions Valid.  A Bounded,
/// failed, unknown, timed-out or out-of-resource result, a skipped implementation, a filter or a
/// declared exemption leaves the build without a record.
/// </summary>
public static class ModularTerminationProducer {
  public static async Task<ModularTerminationCompletion> Complete(Program program,
    IReadOnlyList<Tuple<string, Microsoft.Boogie.Program>> boogiePrograms,
    Microsoft.Boogie.PipelineOutcome outcome, bool verified) {
    var options = program.Options;
    if (ModularTerminationRecords.UnsupportedConfiguration(options) is { } unsupported) {
      return ModularTerminationCompletion.Failed(unsupported);
    }
    if (options.UserConstrainedProcsToCheck || options.ProcsToCheck.Count != 0 || options.ProcsToIgnore.Count != 0) {
      return ModularTerminationCompletion.Failed("verification was restricted to some procedures");
    }
    if (outcome != Microsoft.Boogie.PipelineOutcome.VerificationCompleted || !verified) {
      return ModularTerminationCompletion.Failed("its verification did not succeed");
    }
    if (program.Compilation.Includes.Count != 0 && !options.VerifyAllModules) {
      return ModularTerminationCompletion.Failed("it includes files that this build did not verify (use --verify-included-files, or pass them as inputs)");
    }
    if (program.AfterParsingClone == null) {
      return ModularTerminationCompletion.Failed("its parsed program was not retained");
    }

    var urisToVerify = program.Compilation.UrisToVerify ?? new HashSet<Uri>();
    foreach (var node in program.DefaultModule.Descendants()) {
      if (node.Origin?.Uri == null || !urisToVerify.Contains(node.Origin.Uri)) {
        continue;
      }
      if (node is IAttributeBearingDeclaration attributed) {
        var verify = true;
        if (Attributes.ContainsBool(attributed.Attributes, "verify", ref verify) && !verify) {
          return ModularTerminationCompletion.Failed($"{node.Origin.Uri.Segments.LastOrDefault()}({node.Origin.line},{node.Origin.col + 1}) is marked {{:verify false}}");
        }
        if (Attributes.Find(attributed.Attributes, "only") != null) {
          return ModularTerminationCompletion.Failed($"{node.Origin.Uri.Segments.LastOrDefault()}({node.Origin.line},{node.Origin.col + 1}) uses {{:only}}");
        }
      }
      var decreases = node switch {
        MethodOrConstructor m => m.Decreases,
        IteratorDecl i => i.Decreases,
        LoopStmt l => l.Decreases,
        _ => null
      };
      if (decreases?.Expressions?.Any(e => e is WildcardExpr) == true) {
        return ModularTerminationCompletion.Failed($"{node.Origin.Uri.Segments.LastOrDefault()}({node.Origin.line},{node.Origin.col + 1}) uses decreases *, which does not establish termination");
      }
    }

    if (options.Printer is not DafnyConsolePrinter printer) {
      return ModularTerminationCompletion.Failed("the verifier's per-implementation results are not available");
    }
    var correct = new Dictionary<string, int>();
    foreach (var entry in printer.VerificationResults) {
      var allValid = entry.Result.Outcome == VC.VcOutcome.Correct &&
                     entry.Result.VCResults.All(vc => vc.Outcome == Microsoft.Boogie.SolverOutcome.Valid);
      if (!allValid) {
        return ModularTerminationCompletion.Failed($"the verification of {entry.Implementation.Name} was not conclusive ({entry.Result.Outcome})");
      }
      correct[entry.Implementation.Name] = correct.GetValueOrDefault(entry.Implementation.Name) + 1;
    }
    var expected = new Dictionary<string, int>();
    foreach (var (_, boogieProgram) in boogiePrograms) {
      foreach (var impl in boogieProgram.Implementations) {
        expected[impl.VerboseName] = expected.GetValueOrDefault(impl.VerboseName) + 1;
      }
    }
    foreach (var (name, count) in expected.OrderBy(kv => kv.Key, StringComparer.Ordinal)) {
      if (correct.GetValueOrDefault(name) < count) {
        return ModularTerminationCompletion.Failed($"{name} was not verified");
      }
    }

    var programText = new DooFile(program.AfterParsingClone).ProgramText;
    var uri = new Uri("transcript:///modular-termination-retained.dfy");
    var (reparsedText, reparseError) = await RoundTrip(options, programText, uri);
    if (reparsedText == null) {
      return ModularTerminationCompletion.Failed($"its retained program does not parse: {reparseError}");
    }
    if (WithoutNameLine(reparsedText) != WithoutNameLine(programText)) {
      return ModularTerminationCompletion.Failed("its retained program does not print back to itself");
    }
    var (contracts, contractError) = await ModularTerminationContracts.Compute(options, programText, uri);
    if (contracts == null) {
      return ModularTerminationCompletion.Failed($"its retained program does not parse: {contractError}");
    }
    return ModularTerminationCompletion.Complete(programText, contracts, program.Compilation.ModularTerminationImports,
      ModularTerminationRecords.SemanticOptions(options));
  }

  /// <summary>
  /// The printer starts a program with a comment naming it, which differs between the original and
  /// the reparsed program, and leaves a blank line for each skipped library module, which may end it.
  /// </summary>
  private static string WithoutNameLine(string text) {
    var body = text.StartsWith("// ") ? text[(text.IndexOf('\n') + 1)..] : text;
    return body.TrimEnd('\n');
  }

  private static async Task<(string? Text, string? Error)> RoundTrip(DafnyOptions options, string programText, Uri uri) {
    var parseOptions = new DafnyOptions(options, true) {
      DafnyPrintFile = null
    };
    var reporter = new BatchErrorReporter(parseOptions);
    var parsed = await ProgramParser.Parse(programText, uri, reporter);
    if (reporter.HasErrors) {
      return (null, reporter.AllMessagesByLevel[ErrorLevel.Error].First().Message);
    }
    var tw = new StringWriter { NewLine = "\n" };
    new Printer(tw, parseOptions, PrintModes.Serialization).PrintProgram(parsed.Program, false);
    return (tw.ToString(), null);
  }
}

public static class ModularTerminationRecords {
  public const string ContractsEntry = "termination-contracts.json";
  public const string RecordedDooFileVersion = "2.0";
  public const string IssueLink = "https://github.com/erniecohen/dafny/issues/211";
  public const string RebuildInstruction = "rebuild it with 'dafny build --target:lib --modular-termination'";

  public static string Sha256(string text) {
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
  }

  /// <summary>
  /// Identifies the build of the verifier: the module version IDs of the Dafny and Boogie assemblies
  /// that parse, resolve, translate and verify a program, including the embedded prelude.
  /// </summary>
  public static readonly string ForkBuildId = ComputeForkBuildId();

  private static string ComputeForkBuildId() {
    var assemblies = new HashSet<Assembly> {
      typeof(DooFile).Assembly,
      typeof(Microsoft.Boogie.Program).Assembly,
      typeof(Microsoft.Boogie.ExecutionEngine).Assembly,
      typeof(Microsoft.Boogie.VCGenOptions).Assembly,
    };
    var lines = assemblies
      .Select(a => $"{a.GetName().Name}:{a.ManifestModule.ModuleVersionId:N}\n")
      .OrderBy(l => l, StringComparer.Ordinal);
    return Sha256(string.Concat(lines));
  }

  /// <summary>
  /// The options that change how a retained program is parsed, resolved or translated into proof
  /// obligations, or which axioms the obligations may use.  A library and its client must agree on
  /// all of them exactly.
  /// </summary>
  public static IEnumerable<Option> SemanticOptionList => [
    CommonOptionBag.ModularTermination,
    CommonOptionBag.LegacyDecreasesOrder,
    CommonOptionBag.LegacyNoncanonicalMapPayloads,
    CommonOptionBag.LegacyUnguardedChar,
    CommonOptionBag.TypeSystemRefresh,
    CommonOptionBag.GeneralTraits,
    CommonOptionBag.GeneralNewtypes,
    CommonOptionBag.BitvectorMapKeys,
    CommonOptionBag.ContextualLambdaDomains,
    CommonOptionBag.ContextualQuantifierDomains,
    CommonOptionBag.ConservativeMatchInference,
    CommonOptionBag.DatatypeSubsetConversions,
    CommonOptionBag.AncestorQualifiedModuleIds,
    CommonOptionBag.SynonymRefinesAbstractType,
    CommonOptionBag.InvariantConstructorInference,
    CommonOptionBag.UserDefinedOperators,
    CommonOptionBag.AdditionalAxioms,
    CommonOptionBag.MapComprehensionDomainTriggers,
    CommonOptionBag.HeapFreeLambdaFacts,
    CommonOptionBag.StatementExpressionConclusions,
    CommonOptionBag.ConsistentObligationChecks,
    CommonOptionBag.RawPointers,
    CommonOptionBag.UnicodeCharacters,
    CommonOptionBag.RelaxDefiniteAssignment,
    CommonOptionBag.Referrers,
    CommonOptionBag.ManualTriggerOption,
    CommonOptionBag.ManualLemmaInduction,
    CommonOptionBag.DisableNonLinearArithmetic,
    CommonOptionBag.EnforceDeterminism,
    CommonOptionBag.DefaultFunctionOpacity,
    CommonOptionBag.QuantifierSyntax,
    Function.FunctionSyntaxOption,
    MethodOrConstructor.ReadsClausesOnMethods,
    MethodOrFunction.AllowExternalContracts,
    PrintStmt.TrackPrintEffectsOption,
    BoogieOptionBag.BoogieArguments,
  ];

  /// <summary>
  /// Returns why the current configuration cannot issue or admit a portable record, or null.
  /// </summary>
  public static string? UnsupportedConfiguration(DafnyOptions options) {
    if (OptionValue(options, CommonOptionBag.Prelude) != null) {
      return "a custom --prelude is not part of the recorded verifier build";
    }
    if (OptionValue(options, CommonOptionBag.PluginOption) is IEnumerable plugins && plugins.Cast<object>().Any()) {
      return "plugins are not part of the recorded verifier build";
    }
    return null;
  }

  public static SortedDictionary<string, string> SemanticOptions(DafnyOptions options) {
    var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
    foreach (var option in SemanticOptionList) {
      result[option.Name] = Format(OptionValue(options, option));
    }
    return result;
  }

  public static string SemanticOptionsSha256(IDictionary<string, string> semanticOptions) {
    var canonical = semanticOptions.OrderBy(kv => kv.Key, StringComparer.Ordinal)
      .Select(kv => $"{JsonSerializer.Serialize(kv.Key)}={JsonSerializer.Serialize(kv.Value)}\n");
    return Sha256(string.Concat(canonical));
  }

  private static object? OptionValue(DafnyOptions options, Option option) {
    if (options.Options.OptionArguments.TryGetValue(option, out var value)) {
      return value;
    }
    return option.Parse("").GetValueForOption(option);
  }

  private static string Format(object? value) {
    return value switch {
      null => "",
      bool b => b ? "true" : "false",
      string s => s,
      IEnumerable e => string.Join(" ", e.Cast<object?>().Select(Format)),
      _ => value.ToString() ?? ""
    };
  }
}
