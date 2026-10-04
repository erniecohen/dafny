// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Dafny;
using Bpl = Microsoft.Boogie;

namespace DafnyB3.Census;

internal static class CensusMain {
  private static readonly JsonSerializerOptions Json = new() {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
    PropertyNameCaseInsensitive = true
  };

  public static async Task<int> Main(string[] args) {
    if (args.Length is < 2 or > 4) {
      Console.Error.WriteLine("Usage: DafnyB3.Census <repository-root> <output.json> [source-revision] [dependency-source-revision]");
      return 2;
    }
    var root = Path.GetFullPath(args[0]);
    var manifest = Path.Combine(root, "Source/DafnyB3.Census/corpus.json");
    var entries = JsonSerializer.Deserialize<List<CorpusEntry>>(await File.ReadAllTextAsync(manifest), Json)!;
    var results = new List<CaseResult>();
    foreach (var entry in entries) {
      var result = await Run(root, entry);
      results.Add(result);
      Console.WriteLine($"{entry.Id}: {result.Stage}; {result.Modules.Count} translated module(s)");
    }
    var assembly = typeof(DafnyOptions).Assembly;
    var report = new {
      SchemaVersion = 1,
      SourceRevision = args.Length > 2 ? args[2] : "unrecorded",
      DependencySourceRevision = args.Length > 3 ? args[3] : "unrecorded",
      SourceDirty = Environment.GetEnvironmentVariable("DAFNY_CENSUS_SOURCE_DIRTY"),
      CollectorSourceSha256 = Hash(await File.ReadAllBytesAsync(Path.Combine(root, "Source/DafnyB3.Census/Program.cs"))),
      CollectorAssemblySha256 = Hash(File.ReadAllBytes(typeof(CensusMain).Assembly.Location)),
      DafnyAssemblyVersion = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion,
      DafnyAssemblySha256 = Hash(File.ReadAllBytes(assembly.Location)),
      BoogieSourceRevision = "73a0e214a87df85fc058270268c1d0706fd05bc9",
      BoogiePackage = "3.5.5-review.37e4435d",
      CorpusManifestSha256 = Hash(await File.ReadAllBytesAsync(manifest)),
      PreludeSha256 = Hash(await File.ReadAllBytesAsync(Path.Combine(root, "Source/DafnyCore/DafnyPrelude.bpl"))),
      CapturePoint = "Dafny translation followed by Boogie resolution/typechecking; before engine task creation, preprocessing, VC generation or proving",
      VerificationExecuted = false,
      Counting = "Unique reachable object identities per module; graph aliases are counted once. Static obligations describe emitted syntax, not executed checks or proof results.",
      Traversal = "Instance fields of Boogie objects and materialized containers; tokens and numbered metadata omitted; no reflection property getters or desugaring. A small whitelist of ordinary list/backing accessors supplies summary counts.",
      Cases = results,
      Aggregate = Inventory.Sum(results.SelectMany(r => r.Modules).Select(m => m.Inventory)),
      ExpectationsMatched = results.All(r => r.Stage == r.ExpectedStage)
    };
    var output = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, Json) + "\n");
    return report.ExpectationsMatched ? 0 : 1;
  }

  private static async Task<CaseResult> Run(string root, CorpusEntry entry) {
    var result = new CaseResult { Id = entry.Id, Path = entry.Path, ExpectedStage = entry.ExpectedStage,
      Categories = entry.Categories, IntendedVerification = entry.IntendedVerification };
    var input = Path.Combine(root, entry.Path);
    if (!File.Exists(input)) { result.Stage = "missing-input"; return result; }
    var bytes = await File.ReadAllBytesAsync(input);
    result.SourceSha256 = Hash(bytes);
    Microsoft.Dafny.Type.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.DafnyPrelude = Path.Combine(root, "Source/DafnyCore/DafnyPrelude.bpl");
    result.Options = new Dictionary<string, string> {
      ["type-system-refresh"] = entry.TypeSystemRefresh.ToString().ToLowerInvariant(),
      ["general-newtypes"] = entry.TypeSystemRefresh.ToString().ToLowerInvariant(),
      ["general-traits"] = entry.GeneralTraits,
      ["type-encoding"] = options.TypeEncodingMethod.ToString(),
      ["additional-axioms"] = "false"
    };
    options.Set(CommonOptionBag.TypeSystemRefresh, entry.TypeSystemRefresh);
    options.Set(CommonOptionBag.GeneralNewtypes, entry.TypeSystemRefresh);
    options.Set(CommonOptionBag.GeneralTraits, entry.GeneralTraits == "full"
      ? CommonOptionBag.GeneralTraitsOptions.Full : CommonOptionBag.GeneralTraitsOptions.Datatype);
    var reporter = new BatchErrorReporter(options);
    var phase = "parse";
    try {
      var parsed = await ProgramParser.Parse(Encoding.UTF8.GetString(bytes), new Uri(input), reporter);
      if (reporter.HasErrors) { result.Stage = "parse-rejected"; return result; }
      phase = "resolution";
      await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
      result.CardinalityAdmitted = ReadCardinalityReceipt(parsed.Program);
      if (reporter.HasErrors) { result.Stage = "resolution-rejected"; return result; }
      if (!result.CardinalityAdmitted) { result.Stage = "cardinality-not-admitted"; return result; }
      phase = "translation";
      foreach (var translated in BoogieGenerator.Translate(parsed.Program, reporter)) {
        var module = new ModuleResult { Name = translated.Item1 };
        result.Modules.Add(module);
        var boogie = translated.Item2;
        var sink = new BoogieErrorSink();
        phase = "boogie-resolution";
        module.ResolutionErrorCount = boogie.Resolve(options, sink);
        phase = "boogie-typecheck";
        if (module.ResolutionErrorCount == 0) { module.TypecheckErrorCount = boogie.Typecheck(options, sink); }
        module.Diagnostics = sink.Errors;
        module.Inventory = Inventory.Capture(boogie);
        module.Units = module.ResolutionErrorCount == 0
          ? boogie.Implementations.Select(DescribeUnit).OrderBy(u => u.Name).ToList() : [];
      }
      result.Stage = reporter.HasErrors ? "translation-rejected" : result.Modules.Count == 0 ? "no-translated-modules"
        : result.Modules.Any(m => m.ResolutionErrorCount != 0) ? "boogie-resolution-rejected"
        : result.Modules.Any(m => m.TypecheckErrorCount != 0) ? "boogie-typecheck-rejected" : "typed-pre-vc";
    } catch (Exception ex) {
      result.Stage = phase + "-exception";
      result.Exception = ex.GetType().FullName + ": " + ex.Message.Replace(root, "<repository>");
    } finally {
      result.Diagnostics = reporter.AllMessages.Select(d => new Diagnostic {
        Phase = d.Source.ToString(), Level = d.Level.ToString(), ErrorId = d.ErrorId,
        Message = d.Message.Replace(root, "<repository>")
      }).ToList();
    }
    return result;
  }

  private static bool ReadCardinalityReceipt(Microsoft.Dafny.Program program) {
    // These two internal auto-properties are observed through their backing fields,
    // without running a property getter or rerunning validation. A changed layout fails.
    var receiptField = typeof(Microsoft.Dafny.Program).GetField("<CardinalityValidationReceipt>k__BackingField",
      BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Cardinality receipt field changed");
    var receipt = receiptField.GetValue(program);
    if (receipt == null) { return false; }
    var succeededField = receipt.GetType().GetField("<Succeeded>k__BackingField",
      BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Cardinality receipt result field changed");
    return (bool)succeededField.GetValue(receipt)!;
  }

  private static UnitResult DescribeUnit(Bpl.Implementation implementation) {
    var commands = implementation.Blocks.SelectMany(b => b.Cmds).SelectMany(Flatten).ToList();
    var assertions = commands.OfType<Bpl.AssertCmd>().ToList();
    var calls = commands.OfType<Bpl.CallCmd>().ToList();
    return new UnitResult {
      Name = implementation.Name, Structured = implementation.StructuredStmts != null,
      Blocks = implementation.Blocks.Count, Commands = commands.Count,
      DirectAssertCommands = assertions.Count,
      CheckedExitClauses = implementation.Proc.Ensures.Count(e => !e.Free),
      FreeExitClauses = implementation.Proc.Ensures.Count(e => e.Free),
      CheckedEntryClauses = implementation.Proc.Requires.Count(r => !r.Free),
      FreeEntryClauses = implementation.Proc.Requires.Count(r => r.Free),
      CallSites = calls.Select(c => new CallSite {
        Target = c.Proc.Name, Free = c.IsFree,
        CheckedRequires = c.IsFree ? 0 : c.Proc.Requires.Count(r => !r.Free),
        FreeRequires = c.Proc.Requires.Count(r => r.Free),
        OutputCount = c.Outs.Count, ModifiedGlobals = c.Proc.Modifies.Count
      }).ToList(),
      AssertionDescriptions = assertions.GroupBy(a => a.Description?.GetType().Name ?? "none")
        .ToDictionary(g => g.Key, g => g.Count()).OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value)
    };
  }

  private static IEnumerable<Bpl.Cmd> Flatten(Bpl.Cmd command) {
    yield return command;
    if (command is Bpl.StateCmd state) {
      foreach (var child in state.Cmds.SelectMany(Flatten)) { yield return child; }
    }
    // CallCmd is left intact. GetDesugaring would mutate the observation point.
  }

  internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

internal sealed class CorpusEntry {
  public string Id { get; set; } = "";
  public string Path { get; set; } = "";
  public string ExpectedStage { get; set; } = "";
  public string[] Categories { get; set; } = [];
  public string GeneralTraits { get; set; } = "datatype";
  public bool TypeSystemRefresh { get; set; } = true;
  public string IntendedVerification { get; set; } = "not-executed";
}
internal sealed class Diagnostic {
  public string Phase { get; set; } = "";
  public string Level { get; set; } = "";
  public string? ErrorId { get; set; }
  public string Message { get; set; } = "";
}
internal sealed class CaseResult {
  public string Id { get; set; } = "";
  public string Path { get; set; } = "";
  public string SourceSha256 { get; set; } = "";
  public string Stage { get; set; } = "pending";
  public string ExpectedStage { get; set; } = "";
  public string[] Categories { get; set; } = [];
  public string IntendedVerification { get; set; } = "";
  public bool CardinalityAdmitted { get; set; }
  public Dictionary<string, string> Options { get; set; } = [];
  public string? Exception { get; set; }
  public List<Diagnostic> Diagnostics { get; set; } = [];
  public List<ModuleResult> Modules { get; set; } = [];
}
internal sealed class ModuleResult {
  public string Name { get; set; } = "";
  public int ResolutionErrorCount { get; set; }
  public int TypecheckErrorCount { get; set; }
  public List<string> Diagnostics { get; set; } = [];
  public Inventory Inventory { get; set; } = new();
  public List<UnitResult> Units { get; set; } = [];
}
internal sealed class UnitResult {
  public string Name { get; set; } = "";
  public bool Structured { get; set; }
  public int Blocks { get; set; }
  public int Commands { get; set; }
  public int DirectAssertCommands { get; set; }
  public int CheckedEntryClauses { get; set; }
  public int FreeEntryClauses { get; set; }
  public int CheckedExitClauses { get; set; }
  public int FreeExitClauses { get; set; }
  public Dictionary<string, int> AssertionDescriptions { get; set; } = [];
  public List<CallSite> CallSites { get; set; } = [];
}
internal sealed class CallSite {
  public string Target { get; set; } = "";
  public bool Free { get; set; }
  public int CheckedRequires { get; set; }
  public int FreeRequires { get; set; }
  public int OutputCount { get; set; }
  public int ModifiedGlobals { get; set; }
}
internal sealed class BoogieErrorSink : Bpl.IErrorSink {
  public List<string> Errors { get; } = [];
  public void Error(Bpl.IToken token, string message) => Errors.Add($"{token.line}:{token.col}: {message}");
}

internal sealed class Inventory {
  public SortedDictionary<string, int> NodeKinds { get; set; } = [];
  public SortedDictionary<string, int> Attributes { get; set; } = [];
  public SortedDictionary<string, int> InterpretedSymbols { get; set; } = [];
  public SortedDictionary<string, int> EnumValues { get; set; } = [];
  public SortedDictionary<string, int> OmittedObjectKinds { get; set; } = [];
  public SortedDictionary<string, int> OmittedFields { get; set; } = [];
  public int WhereClauses { get; set; }
  public int ResidualTypeParameters { get; set; }
  public int TypeApplications { get; set; }
  public SortedDictionary<string, int> TriggerGroupSizes { get; set; } = [];
  public SortedDictionary<string, int> BitvectorWidths { get; set; } = [];
  public SortedDictionary<string, int> LiteralKinds { get; set; } = [];

  public static Inventory Capture(Bpl.Program program) {
    var inventory = new Inventory();
    var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
    Walk(program);
    return inventory;

    void Walk(object? value) {
      if (value == null) { return; }
      var type = value.GetType();
      if (value is string || type.IsPrimitive || type.IsEnum || value is decimal) { return; }
      if (!seen.Add(value)) { return; }
      if (value is Bpl.IToken) { Add(inventory.OmittedObjectKinds, "source-token"); return; }
      if (value is IDictionary dictionary) {
        foreach (DictionaryEntry item in dictionary) { Walk(item.Key); Walk(item.Value); }
        return;
      }
      if (value is IEnumerable sequence && type.Namespace?.StartsWith("System.Collections", StringComparison.Ordinal) == true || value is Array) {
        foreach (var item in (IEnumerable)value) { Walk(item); }
        return;
      }
      if (type.Namespace?.StartsWith("Microsoft.Boogie", StringComparison.Ordinal) != true ||
          !type.Assembly.GetName().Name!.StartsWith("Boogie", StringComparison.Ordinal)) {
        Add(inventory.OmittedObjectKinds, type.FullName ?? type.Name); return;
      }
      Add(inventory.NodeKinds, type.Name);
      if (value is Bpl.QKeyValue attribute) {
        Add(inventory.Attributes, attribute.Key);
        if (attribute.Key is "builtin" or "bvbuiltin" or "smt_option") {
          Add(inventory.InterpretedSymbols, attribute.Key + ":" + string.Join("=", attribute.Params.OfType<string>()));
        }
      }
      if (value is Bpl.TypedIdent { WhereExpr: not null }) { inventory.WhereClauses++; }
      if (value is Bpl.DeclWithFormals declaration) { inventory.ResidualTypeParameters += declaration.TypeParameters.Count; }
      if (value is Bpl.BinderExpr binder) { inventory.ResidualTypeParameters += binder.TypeParameters.Count; }
      if (value is Bpl.MapType map) { inventory.ResidualTypeParameters += map.TypeParameters.Count; }
      if (value is Bpl.CtorType constructor) { inventory.TypeApplications += constructor.Arguments.Count; }
      if (value is Bpl.SimpleTypeParamInstantiation instantiation) { inventory.TypeApplications += instantiation.FormalTypeParams.Count; }
      if (value is Bpl.TypeSynonymDecl synonym) { inventory.ResidualTypeParameters += synonym.TypeParameters.Count; }
      if (value is Bpl.BvType bitvector) { Add(inventory.BitvectorWidths, bitvector.Bits.ToString()); }
      if (value is Bpl.LiteralExpr literal) { Add(inventory.LiteralKinds, literal.Val.GetType().FullName ?? literal.Val.GetType().Name); }
      if (value is Bpl.Trigger trigger) { Add(inventory.TriggerGroupSizes, trigger.Tr.Count.ToString()); }
      for (var owner = type; owner != null && owner != typeof(object); owner = owner.BaseType) {
        foreach (var field in owner.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(f => f.Name)) {
          var key = owner.Name + "." + field.Name;
          if (field.Name is "InternalNumberedMetadata" or "_tok") { Add(inventory.OmittedFields, key); continue; }
          var child = field.GetValue(value);
          if (child is Enum enumeration) { Add(inventory.EnumValues, key + "=" + enumeration); }
          Walk(child);
        }
      }
    }
  }

  public static Inventory Sum(IEnumerable<Inventory> inventories) {
    var sum = new Inventory();
    foreach (var item in inventories) {
      Merge(sum.NodeKinds, item.NodeKinds); Merge(sum.Attributes, item.Attributes);
      Merge(sum.InterpretedSymbols, item.InterpretedSymbols); Merge(sum.EnumValues, item.EnumValues);
      Merge(sum.OmittedObjectKinds, item.OmittedObjectKinds); Merge(sum.OmittedFields, item.OmittedFields);
      Merge(sum.TriggerGroupSizes, item.TriggerGroupSizes);
      Merge(sum.BitvectorWidths, item.BitvectorWidths); Merge(sum.LiteralKinds, item.LiteralKinds);
      sum.WhereClauses += item.WhereClauses; sum.ResidualTypeParameters += item.ResidualTypeParameters;
      sum.TypeApplications += item.TypeApplications;
    }
    return sum;
  }
  private static void Add(SortedDictionary<string, int> target, string key, int count = 1) => target[key] = target.GetValueOrDefault(key) + count;
  private static void Merge(SortedDictionary<string, int> target, SortedDictionary<string, int> source) {
    foreach (var (key, count) in source) { Add(target, key, count); }
  }
}
