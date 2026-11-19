#nullable enable

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DafnyCore.Options;
using Microsoft.Dafny;
using Tomlyn;
using Tomlyn.Helpers;
using Tomlyn.Model;

namespace DafnyCore;

// Model class for the .doo file format for Dafny libraries.
// Contains the validation logic for safely consuming libraries as well.
public class DooFile {
  public const string Extension = ".doo";

  // FileVersion is shared by fork revisions. Contracts over compiler-owned references
  // require the full product version, whose revision identifies their equations.
  private static string IntegerBitwiseReferenceVersion => typeof(DooFile).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

  private const string ProgramFileEntry = "program";

  private const string ManifestFileEntry = "manifest.toml";

  public class ManifestData {
    public const string CurrentDooFileVersion = "1.0";
    public string DooFileVersion { get; set; }

    public string DafnyVersion { get; set; }

    public string? IntegerBitwiseReferenceVersion { get; set; }

    public string? SolverIdentifier { get; set; }
    public string? SolverVersion { get; set; }

    public Dictionary<string, object> Options { get; set; }

    /// <summary>
    /// Present only in a library built under --modular-termination; null, and so absent from the
    /// manifest, otherwise.
    /// </summary>
    public ModularTerminationManifest? ModularTermination { get; set; }

    public RecursiveModuleManifest? RecursiveModules { get; set; }

    static ManifestData() {
      CommonOptionBag.EnsureStaticConstructorHasRun();
    }

    public ManifestData() {
      // Only for TOML deserialization!
      DooFileVersion = null!;
      DafnyVersion = null!;
      Options = null!;
    }

    public ManifestData(DafnyOptions options) {
      DooFileVersion = CurrentDooFileVersion;
      DafnyVersion = options.VersionNumber;
      IntegerBitwiseReferenceVersion = options.Get(CommonOptionBag.IntegerBitwise)
        ? DooFile.IntegerBitwiseReferenceVersion : null;

      SolverIdentifier = options.SolverIdentifier;
      // options.SolverVersion may be null (if --no-verify is used for example)
      SolverVersion = options.SolverVersion?.ToString();

      Options = new Dictionary<string, object>();
      foreach (var option in OptionRegistry.GlobalOptions.Concat(OptionRegistry.ModuleOptions)) {
        if (option == CommonOptionBag.Libraries) {
          // We don't want to serialize the FileInfo objects of this option
          // For now we add an option specific exception here so we do not need to change
          // the option registration system.
          // When improve soundness through https://github.com/dafny-lang/dafny/issues/5335
          // Then we'll get back to this
          continue;
        }
        var optionValue = options.Get((dynamic)option);
        // Missing means the default in CheckAndGetLibraryOptions. Keep default-false feature and
        // legacy options byte-compatible with older manifests; presence is never proof of verification.
        if ((option == CommonOptionBag.LegacyNoModularTermination ||
             option == CommonOptionBag.MemberInvariants || option == CommonOptionBag.RecursiveModules ||
             option == CommonOptionBag.IntegerBitwise) && optionValue is false) {
          continue;
        }
        if (option == CommonOptionBag.QuantifierSyntax) {
          switch (optionValue) {
            case QuantifierSyntaxOptions.Version4:
              optionValue = "4";
              break;
            case QuantifierSyntaxOptions.Version3:
              optionValue = "3";
              break;
          }
        }
        Options.Add(option.Name, optionValue);
      }
    }

    public static ManifestData Read(TextReader reader) {
      return Toml.ToModel<ManifestData>(reader.ReadToEnd(), null, new TomlModelOptions());
    }

    public void Write(TextWriter writer) {
      var content = Toml.FromModel(this, new TomlModelOptions() {
        ConvertToToml = obj => {
          if (obj is Enum) {
            TomlFormatHelper.ToString(obj.ToString()!, TomlPropertyDisplayKind.Default);
            return obj.ToString();
          }

          return obj;
        }
      }).Replace("\r\n", "\n");
      writer.Write(content);
    }
  }

  public ManifestData Manifest { get; set; }

  public string ProgramText { get; set; }

  /// <summary>
  /// The termination-contracts.json entry of a library built under --modular-termination, or null.
  /// </summary>
  public string? TerminationContractsText { get; set; }

  public static async Task<DooFile> Read(string path) {
    using var archive = ZipFile.Open(path, ZipArchiveMode.Read);
    return await Read(archive);
  }

  public static async Task<DooFile> Read(Stream stream) {
    using var archive = new ZipArchive(stream);
    return await Read(archive);
  }

  private static async Task<DooFile> Read(ZipArchive archive) {

    var manifestEntry = archive.GetEntry(ManifestFileEntry);
    if (manifestEntry == null) {
      throw new ArgumentException(".doo file missing manifest entry");
    }

    ManifestData manifest;
    await using (var manifestStream = manifestEntry.Open()) {
      manifest = ManifestData.Read(new StreamReader(manifestStream, Encoding.UTF8));
    }

    var programTextEntry = archive.GetEntry(ProgramFileEntry);
    if (programTextEntry == null) {
      throw new ArgumentException(".doo file missing program text entry");
    }

    string programText;
    await using (var programTextStream = programTextEntry.Open()) {
      var reader = new StreamReader(programTextStream, Encoding.UTF8);
      programText = await reader.ReadToEndAsync();
    }

    var result = new DooFile(manifest, programText);
    var contractsEntry = archive.GetEntry(ModularTerminationRecords.ContractsEntry);
    if (contractsEntry != null) {
      await using var contractsStream = contractsEntry.Open();
      result.TerminationContractsText = await new StreamReader(contractsStream, Encoding.UTF8).ReadToEndAsync();
    }
    return result;
  }

  public DooFile(Program dafnyProgram, bool canonicalWildcards = false) {
    if (dafnyProgram.Options.Get(CommonOptionBag.UserDefinedOperators)) {
      throw new InvalidOperationException("--user-defined-operators does not support .doo output in this milestone");
    }
    var tw = new StringWriter {
      NewLine = "\n"
    };
    var pr = new Printer(tw, dafnyProgram.Options, PrintModes.Serialization,
      new PrintFlags(CanonicalWildcards: canonicalWildcards));
    // afterResolver is false because we don't yet have a way to safely skip resolution
    // when reading the program back into memory.
    // It's probably worth serializing a program in a more efficient way first
    // before adding that feature.
    pr.PrintProgram(dafnyProgram, false);
    ProgramText = tw.ToString();
    Manifest = new ManifestData(dafnyProgram.Options);
  }

  public DooFile(ManifestData manifest, string programText) {
    Manifest = manifest;
    ProgramText = programText;
  }

  /// <summary>
  /// Returns the options as specified by the DooFile
  /// </summary>
  public DafnyOptions? Validate(ErrorReporter reporter, Uri file, DafnyOptions options, IOrigin origin) {
    if (options.Get(CommonOptionBag.UserDefinedOperators)) {
      reporter.Error(MessageSource.Project, "operator_input", origin,
        "--user-defined-operators does not support .doo input in this milestone");
      return null;
    }
    if (!options.UsingNewCli) {
      reporter.Error(MessageSource.Project, origin,
        $"cannot load {options.GetPrintPath(file.LocalPath)}: .doo files cannot be used with the legacy CLI");
      return null;
    }

    var recursive = Manifest.RecursiveModules;
    if (Manifest.Options == null) {
      reporter.Error(MessageSource.Project, origin, "cannot load a .doo library whose manifest has no options table");
      return null;
    }
    var recordsGroups = Manifest.Options.TryGetValue(CommonOptionBag.RecursiveModules.Name, out var enabled) &&
                        enabled is true;
    if (recursive != null) {
      if (!options.Get(CommonOptionBag.RecursiveModules) && !recordsGroups) {
        reporter.Error(MessageSource.Project, origin,
          "cannot load a recursive-module library without --recursive-modules");
        return null;
      }
      if (options.Get(CommonOptionBag.RecursiveModules) &&
          (!recordsGroups || recursive.Schema != 1 || recursive.Groups == null ||
          recursive.Groups.Any(g => g.Members == null || g.Members.Count < 2) ||
          recursive.ProgramSha256 != ModularTerminationRecords.Sha256(ProgramText) ||
          recursive.ManifestSha256 != RecursiveModuleManifest.ManifestDigest(Manifest) ||
          recursive.RecordSha256 != recursive.Digest())) {
        reporter.Error(MessageSource.Project, origin,
          "cannot load a recursive-module library: its group record is missing, unsupported or does not match its retained program; rebuild the library");
        return null;
      }
    }

    if (!ModularTermination.RulesEnabled(options) &&
        (Manifest.ModularTermination != null || Manifest.DooFileVersion == ModularTerminationRecords.RecordedDooFileVersion)) {
      reporter.Error(MessageSource.Project, origin,
        $"cannot load {options.GetPrintPath(file.LocalPath)}: it was built with --modular-termination and is checked only under that option; use --modular-termination, or rebuild the library without it");
      return null;
    }

    if (options.VersionNumber != Manifest.DafnyVersion) {
      reporter.Error(MessageSource.Project, origin,
        $"cannot load {options.GetPrintPath(file.LocalPath)}: it was built with Dafny {Manifest.DafnyVersion}, which cannot be used by Dafny {options.VersionNumber}");
      return null;
    }

    var libraryOptions = CheckAndGetLibraryOptions(reporter, file, options, origin, Manifest.Options);
    if (libraryOptions != null && libraryOptions.Get(CommonOptionBag.IntegerBitwise) &&
        Manifest.IntegerBitwiseReferenceVersion != IntegerBitwiseReferenceVersion) {
      reporter.Error(MessageSource.Project, origin,
        $"cannot load {options.GetPrintPath(file.LocalPath)}: integer-bitwise reference ABI does not match this Dafny product version; rebuild the library with this version");
      return null;
    }
    return libraryOptions;
  }

  public static DafnyOptions? CheckAndGetLibraryOptions(ErrorReporter reporter,
    Uri libraryFile,
    DafnyOptions options, IOrigin origin,
    IDictionary<string, object> libraryOptions) {
    var result = new DafnyOptions(options);
    var success = true;

    var relevantOptions = options.Options.OptionArguments.Keys.ToHashSet();
    foreach (var option in OptionRegistry.GlobalOptions.Concat(OptionRegistry.ModuleOptions)) {
      // It's important to only look at the options the current command uses,
      // because other options won't be initialized to the correct default value.
      // See CommandRegistry.Create().
      if (!relevantOptions.Contains(option)) {
        continue;
      }
      var localValue = options.Get(option);

      object? libraryValue;
      if (libraryOptions.TryGetValue(option.Name, out var manifestValue)) {
        var printTomlValue = DafnyProject.PrintTomlOptionToCliValue(libraryFile, manifestValue, option);
        var parseResult = option.Parse(printTomlValue.ToArray());
        if (parseResult.Errors.Any()) {
          reporter.Error(MessageSource.Project, origin, $"could not parse value '{manifestValue}' for option '{option.Name}' that has type '{option.ValueType.Name}'");
          return null;
        }
        // By using the dynamic keyword, we can use the generic version of GetValueForOption which does type conversion,
        // which is sadly not accessible without generics.
        libraryValue = parseResult.GetValueForOption((dynamic)option);
      } else {
        // This else can occur because Tomlyn will drop aggregate properties with no values.
        // When this happens, use the default value
        libraryValue = option.Parse("").GetValueForOption(option)!;
      }

      result.Options.OptionArguments[option] = libraryValue;
      result.ApplyBinding(option);
      var prefix = $"cannot load {options.GetPrintPath(libraryFile.LocalPath)}";
      var check = OptionRegistry.GlobalCheck(option);
      if (option == BoogieOptionBag.BoogieArguments && relevantOptions.Contains(BoogieOptionBag.LegacyBoogieLibraryCheck) &&
          options.Get(BoogieOptionBag.LegacyBoogieLibraryCheck)) {
        check = OptionCompatibility.CheckOptionMatches;
      }
      var checkpasses = check?.Invoke(reporter, origin, prefix, option, localValue, libraryValue) ?? true;
      success = success && checkpasses;
    }

    if (!success) {
      return null;
    }

    return result;
  }

  public void Write(ConcreteSyntaxTree wr) {
    var manifestWr = wr.NewFile(ManifestFileEntry);
    using var manifestWriter = new StringWriter();
    Manifest.Write(manifestWriter);
    manifestWr.Write(manifestWriter.ToString().Replace("\r\n", "\n"));

    var programTextWr = wr.NewFile(ProgramFileEntry);
    programTextWr.Write(ProgramText);

    if (TerminationContractsText != null) {
      wr.NewFile(ModularTerminationRecords.ContractsEntry).Write(TerminationContractsText);
    }
  }

  public void Write(string path) {
    // Delete first, we don't want to merge with existing zips
    File.Delete(path);

    using var archive = ZipFile.Open(path, ZipArchiveMode.Create);

    var manifest = archive.CreateEntry(ManifestFileEntry);
    using (var manifestStream = manifest.Open()) {
      using var manifestWriter = new StreamWriter(manifestStream, Encoding.UTF8);
      Manifest.Write(manifestWriter);
    }

    var programText = archive.CreateEntry(ProgramFileEntry);
    using (var programTextStream = programText.Open()) {
      using var programTextWriter = new StreamWriter(programTextStream, Encoding.UTF8);
      programTextWriter.Write(ProgramText);
    }

    if (TerminationContractsText != null) {
      var contracts = archive.CreateEntry(ModularTerminationRecords.ContractsEntry);
      using var contractsStream = contracts.Open();
      using var contractsWriter = new StreamWriter(contractsStream, Encoding.UTF8);
      contractsWriter.Write(TerminationContractsText);
    }
  }

  /// <summary>
  /// Under --modular-termination, admits this library only if it carries a complete record, issued by
  /// this build of Dafny under the current semantic options, whose digests match the retained program
  /// and whose stored termination contracts are exactly those recomputed from it.  Reports the first
  /// mismatch and returns false otherwise.
  /// </summary>
  private static string Short(string? digest) => digest == null ? "(none)" : digest[..Math.Min(12, digest.Length)];

  public async Task<bool> AdmitModularTermination(ErrorReporter reporter, Uri file, DafnyOptions options,
    DafnyOptions libraryOptions, IOrigin origin) {
    var name = file.Scheme == "dllresource" ? Path.GetFileName(file.LocalPath) : options.GetPrintPath(file.LocalPath);
    var prefix = $"cannot load {name} under --modular-termination";

    bool Reject(string reason) {
      reporter.Error(MessageSource.Project, "modular_termination_admission", origin, $"{prefix}: {reason}");
      return false;
    }

    var record = Manifest.ModularTermination;
    if (record == null) {
      if (file.Scheme == "dllresource") {
        return Reject("the bundled standard library is not certified under --modular-termination; " +
                      "use --standard-libraries:false, and pass with --library the certified standard-library core " +
                      $"built from Source/DafnyStandardLibraries/modular-termination (see {ModularTerminationRecords.IssueLink})");
      }
      return Reject(Manifest.DooFileVersion == ModularTerminationRecords.RecordedDooFileVersion
        ? $"its manifest has no modular termination record; {ModularTerminationRecords.RebuildInstruction}"
        : $"it has no modular termination record, because it was built without --modular-termination; {ModularTerminationRecords.RebuildInstruction}");
    }
    if (Manifest.DooFileVersion != ModularTerminationRecords.RecordedDooFileVersion ||
        !SupportedSchema(record) ||
        record.ContractSchema != ModularTerminationManifest.CurrentContractSchema) {
      return Reject($"its record has an unsupported format (doo_file_version {Manifest.DooFileVersion}, schema {record.Schema}, contract_schema {record.ContractSchema}); {ModularTerminationRecords.RebuildInstruction}");
    }
    if (record.RecordSha256 != ModularTerminationRecords.RecordSha256(Manifest.DooFileVersion, record)) {
      return Reject("its record does not match its digest; the library was modified after it was built");
    }
    if (!record.Enabled || record.Status != ModularTerminationManifest.CompleteStatus) {
      return Reject($"its record does not state a complete verification (status '{record.Status}'); {ModularTerminationRecords.RebuildInstruction}");
    }
    if (record.Premises is { Count: > 0 } && PremiseRefusal(record, options) is { } premiseRefusal) {
      return Reject(premiseRefusal);
    }
    if (record.RuntimePremises is { Count: > 0 } && RuntimePremiseRefusal(record, options) is { } runtimeRefusal) {
      return Reject(runtimeRefusal);
    }
    if (ModularTerminationRecords.UnsupportedConfiguration(options) is { } unsupported) {
      return Reject(unsupported);
    }
    if (record.ForkBuildId != ModularTerminationRecords.ForkBuildId(options)) {
      return Reject($"it was verified by a different build of Dafny or of its prelude; {ModularTerminationRecords.RebuildInstruction} using this one");
    }
    var solverSha256 = ModularTerminationRecords.SolverSha256(options);
    if (solverSha256 == null) {
      return Reject($"no Z3 binary was found to compare with the one that verified it (Z3 {record.SolverVersion}); use --solver-path");
    }
    if (record.SolverSha256 != solverSha256) {
      var current = ModularTerminationRecords.Solver(options);
      return Reject($"it was verified with a different Z3 binary (Z3 {record.SolverVersion}, sha256 {Short(record.SolverSha256)}) " +
                    $"than this run uses (Z3 {current?.Version}, sha256 {Short(solverSha256)}); use the same solver, or rebuild the library with this one");
    }
    if (record.ProgramSha256 != ModularTerminationRecords.Sha256(ProgramText)) {
      return Reject("its program does not match the program its record certifies; the library was modified after it was built");
    }
    var recordedOptions = record.SemanticOptions ?? new Dictionary<string, string>();
    if (record.SemanticOptionsSha256 != ModularTerminationRecords.SemanticOptionsSha256(recordedOptions)) {
      return Reject("its recorded semantic options do not match their digest; the library was modified after it was built");
    }
    var currentOptions = ModularTerminationRecords.SemanticOptions(options);
    foreach (var (option, value) in currentOptions) {
      var recorded = recordedOptions.GetValueOrDefault(option);
      if (recorded != value) {
        return Reject($"it was verified with {option} '{recorded ?? "(not recorded)"}', but this run uses '{value}'; use the same value, or rebuild the library with it");
      }
    }
    foreach (var option in recordedOptions.Keys.Where(k => !currentOptions.ContainsKey(k))) {
      return Reject($"its record names an unknown semantic option {option}; {ModularTerminationRecords.RebuildInstruction}");
    }
    if (TerminationContractsText == null) {
      return Reject($"its {ModularTerminationRecords.ContractsEntry} entry is missing");
    }
    if (record.ContractsSha256 != ModularTerminationRecords.Sha256(TerminationContractsText)) {
      return Reject($"its {ModularTerminationRecords.ContractsEntry} entry does not match its record; the library was modified after it was built");
    }
    var (contracts, error) = await ModularTerminationContracts.Compute(libraryOptions, ProgramText, file);
    if (contracts == null) {
      return Reject($"its program does not parse: {error}");
    }
    if (contracts.EntryText != TerminationContractsText) {
      var module = FirstContractDifference(contracts, TerminationContractsText);
      return Reject($"its stored termination contracts are not those of its program{(module != null ? $" (module {module})" : "")}; the library was modified after it was built");
    }
    var exports = record.Exports ?? [];
    if (!exports.Select(e => (e.Module, e.ContractSha256)).SequenceEqual(contracts.Exports.Select(e => (e.Module, e.ContractSha256)))) {
      return Reject("the exports listed in its record are not the contracts of its program; the library was modified after it was built");
    }
    return true;
  }

  /// <summary>
  /// Schema 1 is a record without premises; schema 2 (milestone M5b) one with at least one premise and the
  /// fields that name its trust list.
  /// </summary>
  private static bool SupportedSchema(ModularTerminationManifest record) {
    var hasPremiseFields = record.TrustList != null || record.TrustListSha256 != null || record.PremisesSha256 != null ||
                           record.Premises != null;
    var ghostValid = !hasPremiseFields || record.TrustList != null && record.TrustListSha256 != null &&
      record.PremisesSha256 != null && record.Premises is { Count: > 0 };
    var hasRuntimeFields = record.RuntimeTrustList != null || record.RuntimeTrustListSha256 != null ||
      record.RuntimePremisesSha256 != null || record.RuntimePremises != null;
    return record.Schema switch {
      ModularTerminationManifest.CurrentSchema => !hasPremiseFields && !hasRuntimeFields,
      ModularTerminationManifest.PremisesSchema => hasPremiseFields && ghostValid && !hasRuntimeFields,
      ModularTerminationManifest.RuntimePremisesSchema => ghostValid &&
        record.RuntimeTrustList == ModularTerminationRuntimeTrust.StandardLibrary &&
        record.RuntimeTrustListSha256 != null && record.RuntimePremisesSha256 != null &&
        record.RuntimePremises is { Count: > 0 },
      _ => false
    };
  }

  private static string? RuntimePremiseRefusal(ModularTerminationManifest record, DafnyOptions options) {
    if (ModularTerminationRuntimeTrust.Selected(options) != record.RuntimeTrustList) {
      return $"it assumes runtime contract {record.RuntimePremises![0].Id}, including termination; " +
        $"use {ModularTerminationRuntimeTrust.Option} to opt in to the separate runtime trust list";
    }
    var premises = record.RuntimePremises!;
    if (record.RuntimeTrustListSha256 != ModularTerminationRuntimeTrust.Sha256() ||
        record.RuntimePremisesSha256 != ModularTerminationTrustList.PremisesSha256(premises) ||
        !premises.Select(p => p.Id).SequenceEqual(premises.Select(p => p.Id).Order(StringComparer.Ordinal).Distinct())) {
      return "its runtime premise list or digest differs from the reviewed list; rebuild the library";
    }
    foreach (var premise in premises) {
      if (ModularTerminationRuntimeTrust.Mismatch(premise) is { } mismatch) {
        return mismatch;
      }
    }
    return null;
  }

  /// <summary>
  /// Why a record with trusted premises is not admitted, or null: the client must have selected the same
  /// trust list, and each premise must be an entry of it.  The premises are bound to the program by the
  /// producer, which matched them against the stored program (ModularTerminationProducer), and to the
  /// record by its digest.
  /// </summary>
  private static string? PremiseRefusal(ModularTerminationManifest record, DafnyOptions options) {
    var premises = record.Premises!;
    var selected = ModularTerminationTrustList.Selected(options);
    if (selected != record.TrustList) {
      return $"it relies on the trusted premise {premises[0].Id} of the {record.TrustList} trust list" +
             (premises.Count > 1 ? $" and {premises.Count - 1} more" : "") +
             $"; use --modular-termination-trust:{record.TrustList} to admit it";
    }
    if (record.TrustListSha256 != ModularTerminationTrustList.Sha256(selected)) {
      return $"it was built with a different version of the {selected} trust list; {ModularTerminationRecords.RebuildInstruction} using this one";
    }
    if (record.PremisesSha256 != ModularTerminationTrustList.PremisesSha256(premises) ||
        !premises.Select(p => p.Id).SequenceEqual(premises.Select(p => p.Id).Order(StringComparer.Ordinal).Distinct())) {
      return "its premise list does not match its digest; the library was modified after it was built";
    }
    foreach (var premise in premises) {
      var entry = ModularTerminationTrustList.Find(selected, premise);
      if (entry == null || entry.Kind != premise.Kind || entry.StatementSha256 != premise.StatementSha256 ||
          entry.ContextSha256 != premise.ContextSha256) {
        return $"its premise {premise.Id} is not an entry of the {selected} trust list; the library was modified after it was built";
      }
    }
    return null;
  }

  private static string? FirstContractDifference(ModularTerminationContracts computed, string storedText) {
    var stored = new Dictionary<string, string>();
    try {
      using var document = System.Text.Json.JsonDocument.Parse(storedText);
      foreach (var entry in document.RootElement.GetProperty("contracts").EnumerateArray()) {
        stored[entry.GetProperty("module").GetString()!] = entry.GetProperty("interface").GetString()!;
      }
    } catch (Exception e) when (e is System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException) {
      return null;
    }
    return computed.Interfaces.Keys.Concat(stored.Keys).Order(StringComparer.Ordinal)
      .FirstOrDefault(m => computed.Interfaces.GetValueOrDefault(m) != stored.GetValueOrDefault(m));
  }

}
