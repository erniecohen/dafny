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
using AuditAssumption = Microsoft.Dafny.Auditor.Assumption;
using AuditAssumptionDescription = Microsoft.Dafny.Auditor.AssumptionDescription;

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
  public string SolverVersion { get; set; } = null!;
  public string SolverSha256 { get; set; } = null!;
  public string ProgramSha256 { get; set; } = null!;
  public string SemanticOptionsSha256 { get; set; } = null!;
  public string ContractsSha256 { get; set; } = null!;
  public Dictionary<string, string>? SemanticOptions { get; set; }
  public List<ModularTerminationContractRef>? Exports { get; set; }
  public List<ModularTerminationContractRef>? Imports { get; set; }

  /// <summary>
  /// The digest of every other field of the record, and of the manifest's doo_file_version, so that no
  /// part of the record, its import list included, can be changed or removed on its own.
  /// </summary>
  public string RecordSha256 { get; set; } = null!;
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
  public string ForkBuildId { get; private init; } = null!;
  public ModularTerminationSolver Solver { get; private init; } = null!;

  public static ModularTerminationCompletion Failed(string failure) => new() { Failure = failure };

  public static ModularTerminationCompletion Complete(string programText, ModularTerminationContracts contracts,
    IReadOnlyList<ModularTerminationContractRef> imports, SortedDictionary<string, string> semanticOptions,
    string forkBuildId, ModularTerminationSolver solver) =>
    new() {
      ProgramText = programText,
      Contracts = contracts,
      Imports = imports,
      SemanticOptions = semanticOptions,
      ForkBuildId = forkBuildId,
      Solver = solver
    };

  public ModularTerminationManifest CreateRecord() {
    var record = new ModularTerminationManifest {
      Schema = ModularTerminationManifest.CurrentSchema,
      Enabled = true,
      Status = ModularTerminationManifest.CompleteStatus,
      ContractSchema = ModularTerminationManifest.CurrentContractSchema,
      ForkBuildId = ForkBuildId,
      SolverVersion = Solver.Version,
      SolverSha256 = Solver.Sha256,
      ProgramSha256 = ModularTerminationRecords.Sha256(ProgramText),
      SemanticOptionsSha256 = ModularTerminationRecords.SemanticOptionsSha256(SemanticOptions),
      ContractsSha256 = ModularTerminationRecords.Sha256(Contracts.EntryText),
      SemanticOptions = new Dictionary<string, string>(SemanticOptions),
      Exports = Contracts.Exports.ToList(),
      Imports = Imports.ToList()
    };
    record.RecordSha256 = ModularTerminationRecords.RecordSha256(ModularTerminationRecords.RecordedDooFileVersion, record);
    return record;
  }
}

/// <summary>
/// The Z3 binary that verified a library, or that a client would verify with: its version and the
/// SHA-256 of its bytes.
/// </summary>
public sealed record ModularTerminationSolver(string Version, string Sha256);

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
  /// <summary>
  /// The URI under which the stored program is parsed, resolved and verified again.
  /// </summary>
  private static readonly Uri StoredProgramUri = new("transcript:///modular-termination-retained.dfy");

  /// <param name="inputs">The files of this build: its libraries are loaded again, with the stored
  /// program in place of the sources, to verify the stored program.</param>
  public static async Task<ModularTerminationCompletion> Complete(Program program,
    IReadOnlyList<Tuple<string, Microsoft.Boogie.Program>> boogiePrograms,
    Microsoft.Boogie.PipelineOutcome outcome, bool verified, IReadOnlyList<DafnyFile> inputs) {
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
    if (options.Get(CommonOptionBag.AllowAxioms)) {
      return ModularTerminationCompletion.Failed("it was built with --allow-axioms, which admits unchecked premises");
    }
    if (UnsupportedSource(program) is { } unsupportedSource) {
      return ModularTerminationCompletion.Failed(unsupportedSource);
    }
    if (AssumptionIn(program) is { } assumption) {
      return ModularTerminationCompletion.Failed(assumption);
    }
    if (options.Printer is not DafnyConsolePrinter printer) {
      return ModularTerminationCompletion.Failed("the verifier's per-implementation results are not available");
    }
    if (InventoryFailure(printer, boogiePrograms) is { } inventoryFailure) {
      return ModularTerminationCompletion.Failed(inventoryFailure);
    }

    var programText = new DooFile(program.AfterParsingClone).ProgramText;
    var (reparsedText, reparseError) = await RoundTrip(options, programText, StoredProgramUri);
    if (reparsedText == null) {
      return ModularTerminationCompletion.Failed($"its retained program does not parse: {reparseError}");
    }
    if (WithoutNameLine(reparsedText) != WithoutNameLine(programText)) {
      return ModularTerminationCompletion.Failed("its retained program does not print back to itself");
    }
    // The record certifies the program that is stored, so that program is the one that must verify:
    // resolve and verify it again, with the same libraries and options, and take its inventory,
    // contracts and imports from it.  This covers every way the printed program can differ from the
    // verified one.
    var (stored, storedFailure) = await VerifyStored(options, programText, inputs);
    if (stored == null) {
      return ModularTerminationCompletion.Failed(storedFailure!);
    }
    var (contracts, contractError) = await ModularTerminationContracts.Compute(options, programText, StoredProgramUri);
    if (contracts == null) {
      return ModularTerminationCompletion.Failed($"its retained program does not parse: {contractError}");
    }
    if (ModularTerminationRecords.Solver(options) is not { } solver) {
      return ModularTerminationCompletion.Failed("the Z3 binary that verified it could not be identified");
    }
    return ModularTerminationCompletion.Complete(programText, contracts, stored.Compilation.ModularTerminationImports,
      ModularTerminationRecords.SemanticOptions(options), ModularTerminationRecords.ForkBuildId(options), solver);
  }

  /// <summary>
  /// Parses, resolves and verifies the stored program, with the build's libraries and a quiet copy of
  /// its options, and checks it as the build itself was checked: no assumption, and every Boogie
  /// implementation verified with every verification condition Valid.  Returns the stored program,
  /// or why it cannot be recorded.
  /// </summary>
  private static async Task<(Program? Program, string? Failure)> VerifyStored(DafnyOptions options, string programText,
    IReadOnlyList<DafnyFile> inputs) {
    var storedOptions = new DafnyOptions(options, true) {
      DafnyPrintFile = null,
      DafnyPrintResolvedFile = null,
      PrintFile = null,
      XmlSink = null,
    };
    var printer = new DafnyConsolePrinter(storedOptions);
    storedOptions.Printer = printer;
    var reporter = new BatchErrorReporter(storedOptions);
    string FirstError() => reporter.AllMessagesByLevel[ErrorLevel.Error].FirstOrDefault()?.Message ?? "unknown error";

    var files = inputs.Where(f => f.ShouldNotVerify)
      .Append(DafnyFile.FromText(storedOptions, StoredProgramUri, programText)).ToList();
    var parsed = await new ProgramParser(Microsoft.Extensions.Logging.Abstractions.NullLogger<ProgramParser>.Instance,
      OnDiskFileSystem.Instance).ParseFiles("the stored program", files, reporter, System.Threading.CancellationToken.None);
    var stored = parsed.Program;
    if (reporter.HasErrors) {
      return (null, $"its stored program does not parse: {FirstError()}");
    }
    if (DafnyMain.Resolve(stored) != null || reporter.HasErrors) {
      return (null, $"its stored program does not resolve: {FirstError()}");
    }
    if (UnsupportedSource(stored) is { } unsupported) {
      return (null, $"in its stored program, {unsupported}");
    }
    if (AssumptionIn(stored) is { } assumption) {
      return (null, $"in its stored program, {assumption}");
    }
    var boogiePrograms = await DafnyMain.LargeStackFactory.StartNew(() =>
      BoogieGenerator.Translate(stored, reporter).ToList());
    if (reporter.HasErrors) {
      return (null, $"its stored program cannot be translated for verification: {FirstError()}");
    }
    using var engine = Microsoft.Boogie.ExecutionEngine.CreateWithoutSharedCache(storedOptions);
    var verified = true;
    foreach (var (moduleName, boogieProgram) in boogiePrograms) {
      var (outcome, stats) = await await DafnyMain.LargeStackFactory.StartNew(() =>
        DafnyMain.BoogieOnce(reporter, storedOptions, TextWriter.Null, engine, "modular-termination-retained.dfy",
          moduleName, boogieProgram, "modular_termination_stored"));
      verified &= outcome == Microsoft.Boogie.PipelineOutcome.VerificationCompleted &&
                  DafnyMain.IsBoogieVerified(outcome, stats);
    }
    if (InventoryFailure(printer, boogiePrograms) is { } inventoryFailure) {
      return (null, $"its stored program does not verify: {inventoryFailure}");
    }
    if (!verified || reporter.HasErrors) {
      return (null, reporter.HasErrors
        ? $"its stored program does not verify: {FirstError()}"
        : "its stored program does not verify");
    }
    return (stored, null);
  }

  /// <summary>
  /// What this milestone does not support in the library's own program, or null: iterators, whose
  /// generated members the assumption inventory reports as axioms, and the attributes that mark a
  /// declaration as generated, which only Dafny may write.
  /// </summary>
  private static string? UnsupportedSource(Program program) {
    var urisToVerify = program.Compilation.UrisToVerify ?? new HashSet<Uri>();
    foreach (var node in program.DefaultModule.Descendants()) {
      if (node.Origin?.Uri == null || !urisToVerify.Contains(node.Origin.Uri)) {
        continue;
      }
      if (node is IteratorDecl iterator) {
        return $"{Where(node.Origin)} declares iterator '{iterator.Name}'; iterators are not yet supported under --modular-termination";
      }
      var attributes = node switch {
        Attributes a => [a],
        IAttributeBearingDeclaration d => d.Attributes.AsEnumerable(),
        _ => []
      };
      foreach (var attribute in attributes) {
        if (attribute is UserSuppliedAttributes or UserSuppliedAtAttribute &&
            attribute.Name is "auto_generated" or "opaque_reveal") {
          return $"{Where(node.Origin)} uses {{:{attribute.Name}}}, which only Dafny may write under --modular-termination";
        }
      }
    }
    return null;
  }

  private static string Where(IOrigin origin) => $"{origin.Uri.Segments.LastOrDefault()}({origin.line},{origin.col + 1})";

  /// <summary>
  /// The first assumption in the library's own program, or null.
  /// </summary>
  private static string? AssumptionIn(Program program) {
    var urisToVerify = program.Compilation.UrisToVerify ?? new HashSet<Uri>();
    bool Owned(IOrigin? origin) => origin?.Uri != null && urisToVerify.Contains(origin.Uri);
    // Dafny's own assumption inventory, the one `dafny audit` reports: every assumption it finds in the
    // library's own program refuses the record.
    foreach (var assumption in program.Assumptions(null!)) {
      if (Owned(assumption.Tok) && AuditExemption(assumption) == null) {
        return $"{Where(assumption.Tok)} has an assumption that 'dafny audit' reports: {AuditAssumption.UpdateVerbatim(assumption.desc.Issue, "", "").TrimEnd('.')}";
      }
    }
    // What the inventory does not report: premises outside method and function bodies, bodyless members
    // without a postcondition (whose termination is still assumed), extern members without contracts,
    // and the verification exemptions.
    foreach (var node in program.DefaultModule.Descendants()) {
      if (!Owned(node.Origin) || OpaqueMemberRewriter.IsGeneratedRevealLemma(node)) {
        continue;
      }
      if (UncheckedPremise(node) is { } premise) {
        return $"{Where(node.Origin!)} {premise}";
      }
    }
    return null;
  }

  /// <summary>
  /// Why the verifier's results do not cover every Boogie implementation generated for the program with
  /// every verification condition Valid, or null.
  /// </summary>
  private static string? InventoryFailure(DafnyConsolePrinter printer,
    IReadOnlyList<Tuple<string, Microsoft.Boogie.Program>> boogiePrograms) {
    var correct = new Dictionary<string, int>();
    foreach (var entry in printer.VerificationResults) {
      var allValid = entry.Result.Outcome == VC.VcOutcome.Correct &&
                     entry.Result.VCResults.All(vc => vc.Outcome == Microsoft.Boogie.SolverOutcome.Valid);
      if (!allValid) {
        return $"the verification of {entry.Implementation.Name} was not conclusive ({entry.Result.Outcome})";
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
        return $"{name} was not verified";
      }
    }
    return null;
  }

  /// <summary>
  /// Why an assumption that the auditor reports does not stop a record, or null when it does.  Only one is
  /// exempt: {:termination false} on a trait, which under --modular-termination is a checked extensibility
  /// marker (dispatch is checked against the global bounds recorded in the contract), not an assumption.
  /// </summary>
  private static string? AuditExemption(AuditAssumption assumption) {
    return assumption.desc == AuditAssumptionDescription.HasTerminationFalseAttribute
      ? "{:termination false} is checked under --modular-termination"
      : null;
  }

  /// <summary>
  /// Why a node of the library's own program is not checked by its verification, or null.  The
  /// per-implementation inventory cannot see these: an exempted, assumed, external or bodyless
  /// declaration either produces no verification condition or one that holds by assumption.
  /// </summary>
  private static string? UncheckedPremise(INode node) {
    if (node is IAttributeBearingDeclaration attributed) {
      var verify = true;
      if (Attributes.ContainsBool(attributed.Attributes, "verify", ref verify) && !verify) {
        return "is marked {:verify false}";
      }
      if (Attributes.Find(attributed.Attributes, "only") != null) {
        return "uses {:only}";
      }
      if (Attributes.Find(attributed.Attributes, "axiom") != null) {
        return "is marked {:axiom}, an unchecked premise";
      }
      if (Attributes.Find(attributed.Attributes, "extern") != null) {
        return "is marked {:extern}, whose implementation is not verified";
      }
      if (Attributes.Find(attributed.Attributes, "AssumeCrossModuleTermination") != null) {
        return "is marked @AssumeCrossModuleTermination, an unchecked termination premise";
      }
    }
    switch (node) {
      case AssumeStmt:
        return "uses assume, an unchecked premise";
      case ForallStmt { Body: null }:
        return "has a forall statement without a body, an unchecked premise";
      case OneBodyLoopStmt { Body: null }:
        return "has a loop without a body, an unchecked premise";
      case MethodOrConstructor { Body: null } method when IsConcrete(method):
        return $"declares {method.WhatKind} '{method.Name}' without a body, an unchecked premise";
      case Function { Body: null } function when IsConcrete(function):
        return $"declares {function.WhatKind} '{function.Name}' without a body, an unchecked premise";
    }
    var decreases = node switch {
      MethodOrConstructor m => m.Decreases,
      IteratorDecl i => i.Decreases,
      LoopStmt l => l.Decreases,
      _ => null
    };
    if (decreases?.Expressions?.Any(e => e is WildcardExpr) == true) {
      return "uses decreases *, which does not establish termination";
    }
    return null;
  }

  /// <summary>
  /// A member that must have a body: not an instance member of a trait, which its implementations
  /// supply, and not in an abstract module, whose refinements supply the bodies.  A static member of a
  /// trait has no implementations and must have a body.
  /// </summary>
  private static bool IsConcrete(MemberDecl member) {
    return member.EnclosingClass is not TraitDecl || member.IsStatic
      ? member.EnclosingClass?.EnclosingModuleDefinition is not { ModuleKind: ModuleKindEnum.Abstract }
      : false;
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

  public static string RecordFailureMessage(string failure) {
    return $"cannot build a library under --modular-termination: {failure}. A library built with --modular-termination " +
           "records that its own program was completely verified; build it without --no-verify, --boogie arguments, " +
           "{:verify false}, {:only}, decreases *, assumptions (anything 'dafny audit' reports, bodyless members, --allow-axioms) " +
           "or {:extern}, and with every verification task succeeding";
  }

  public static string Sha256(string text) {
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
  }

  /// <summary>
  /// Identifies the build of the verifier: the module version IDs of DafnyCore and of every Dafny and
  /// Boogie assembly it references, directly or transitively (parser, resolver, translator, VC
  /// generation, VC expressions and the SMT-LIB prover interface among them), and the SHA-256 of the
  /// bytes of the prelude file that translation reads, which is not part of any assembly.  It changes
  /// whenever one of these assemblies or the prelude changes; an assembly rebuilt from identical
  /// inputs by a deterministic build keeps its module version ID.  The solver is recorded separately.
  /// </summary>
  public static string ForkBuildId(DafnyOptions options) {
    var preludePath = BoogieGenerator.PreludePath(options);
    var prelude = File.Exists(preludePath) ? FileSha256(preludePath) : "missing";
    return Sha256(AssemblyIdentity.Value + $"prelude:{prelude}\n");
  }

  private static readonly Lazy<string> AssemblyIdentity = new(() => {
    var seen = new Dictionary<string, Assembly>(StringComparer.Ordinal);
    var pending = new Stack<Assembly>();
    pending.Push(typeof(DooFile).Assembly);
    while (pending.Count != 0) {
      var assembly = pending.Pop();
      var name = assembly.GetName().Name ?? "";
      if (!seen.TryAdd(name, assembly)) {
        continue;
      }
      foreach (var reference in assembly.GetReferencedAssemblies()) {
        if (reference.Name != null && IsVerifierAssembly(reference.Name) && !seen.ContainsKey(reference.Name)) {
          pending.Push(Assembly.Load(reference));
        }
      }
    }
    return string.Concat(seen.OrderBy(kv => kv.Key, StringComparer.Ordinal)
      .Select(kv => $"{kv.Key}:{kv.Value.ManifestModule.ModuleVersionId:N}\n"));
  });

  private static bool IsVerifierAssembly(string name) {
    return name.StartsWith("Boogie.", StringComparison.Ordinal) || name.StartsWith("Dafny", StringComparison.Ordinal);
  }

  private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> FileDigests = new();

  private static string FileSha256(string path) {
    return FileDigests.GetOrAdd(path, p => {
      using var stream = File.OpenRead(p);
      return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    });
  }

  /// <summary>
  /// The Z3 binary that this run uses, found as the verifier finds it: --solver-path, then the z3
  /// directory next to Dafny, then the PATH.  Null when there is none.
  /// </summary>
  public static string? SolverPath(DafnyOptions options) {
    const string proverPath = "PROVER_PATH=";
    var explicitPath = options.ProverOptions.Find(o => o.StartsWith(proverPath));
    if (explicitPath != null) {
      var path = explicitPath[proverPath.Length..];
      return File.Exists(path) ? path : null;
    }
    var isUnix = Environment.OSVersion.Platform is PlatformID.Unix or PlatformID.MacOSX;
    var dafnyBinDir = Path.GetDirectoryName(typeof(DafnyOptions).Assembly.Location) ?? "";
    var local = Path.Combine(dafnyBinDir, "z3", "bin", isUnix ? $"z3-{DafnyOptions.DefaultZ3Version}" : $"z3-{DafnyOptions.DefaultZ3Version}.exe");
    if (File.Exists(local)) {
      return local;
    }
    return Environment.GetEnvironmentVariable("PATH")?.Split(isUnix ? ':' : ';')
      .Select(dir => Path.Combine(dir, isUnix ? "z3" : "z3.exe")).FirstOrDefault(File.Exists);
  }

  public static string? SolverSha256(DafnyOptions options) {
    return SolverPath(options) is { } path ? FileSha256(path) : null;
  }

  private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> SolverVersions = new();

  /// <summary>
  /// The solver's identity; its version is probed, so this is used only where the solver runs anyway
  /// or a diagnostic needs it.
  /// </summary>
  public static ModularTerminationSolver? Solver(DafnyOptions options) {
    if (SolverPath(options) is not { } path) {
      return null;
    }
    var version = SolverVersions.GetOrAdd(path, p => DafnyOptions.GetZ3Version(p)?.ToString() ?? "unknown");
    return new ModularTerminationSolver(version, FileSha256(path));
  }

  /// <summary>
  /// The canonical text of a record, without its own digest.  Each line is a key and its values,
  /// separated by tabs, with backslash, tab and newline escaped.
  /// </summary>
  public static string RecordSha256(string dooFileVersion, ModularTerminationManifest record) {
    var sb = new StringBuilder();
    void Line(params string?[] parts) {
      sb.Append(string.Join("\t", parts.Select(p => (p ?? "").Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n"))));
      sb.Append('\n');
    }
    Line("doo_file_version", dooFileVersion);
    Line("schema", record.Schema.ToString(System.Globalization.CultureInfo.InvariantCulture));
    Line("enabled", record.Enabled ? "true" : "false");
    Line("status", record.Status);
    Line("contract_schema", record.ContractSchema.ToString(System.Globalization.CultureInfo.InvariantCulture));
    Line("fork_build_id", record.ForkBuildId);
    Line("solver_version", record.SolverVersion);
    Line("solver_sha256", record.SolverSha256);
    Line("program_sha256", record.ProgramSha256);
    Line("semantic_options_sha256", record.SemanticOptionsSha256);
    Line("contracts_sha256", record.ContractsSha256);
    foreach (var (option, value) in (record.SemanticOptions ?? new()).OrderBy(kv => kv.Key, StringComparer.Ordinal)) {
      Line("option", option, value);
    }
    foreach (var export in record.Exports ?? []) {
      Line("export", export.Module, export.ContractSha256);
    }
    foreach (var import in record.Imports ?? []) {
      Line("import", import.Module, import.ContractSha256);
    }
    return Sha256(sb.ToString());
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
    CommonOptionBag.LegacyAutoGeneratedAttributes,
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
    CommonOptionBag.AllowAxioms,
    CommonOptionBag.TestAssumptions,
    // Recorded, not refused: the refusal of assumptions above reads Dafny's assumption inventory, not
    // its warnings, so --allow-warnings cannot hide one; recording it keeps the record faithful.
    CommonOptionBag.AllowWarnings,
    CommonOptionBag.MapComprehensionDomainTriggers,
    CommonOptionBag.HeapFreeLambdaFacts,
    CommonOptionBag.StatementExpressionConclusions,
    CommonOptionBag.TraitRankDecreases,
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
    if (!options.IsUsingZ3()) {
      return "only Z3 is supported as the recorded solver";
    }
    if (OptionValue(options, BoogieOptionBag.BoogieArguments) is IEnumerable<string> boogieArguments &&
        boogieArguments.SelectMany(BoogieOptionBag.SplitArguments)
          .FirstOrDefault(argument => !AllowedBoogieArguments.Contains(argument)) is { } refused) {
      return $"--boogie argument '{refused}' is not allowed under --modular-termination, because it can change what " +
             "verification checks (see the allowed list in the Reference Manual)";
    }
    return null;
  }

  /// <summary>
  /// The --boogie arguments allowed under --modular-termination: those reviewed as unable to weaken a
  /// proof.  None is allowed yet; any other argument (/loopUnroll, /noVerify, /proc, /proverOpt, ...)
  /// could make an unproved program verify.
  /// </summary>
  public static readonly IReadOnlySet<string> AllowedBoogieArguments = new HashSet<string>();

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
