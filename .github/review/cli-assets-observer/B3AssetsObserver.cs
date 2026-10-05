// Evidence-only SDK boundary observer. No copy/exclusion or CLR selection admission.
// Source-only checkpoint: exact Linux-x64 descriptor ABI and execution-image pins
// remain separate pre-execution review prerequisites (EXECUTION-PINS.json).
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Build.Framework;
using Microsoft.Win32.SafeHandles;

namespace B3AssetsObserver {
  public sealed class CaptureTask : ITask, ICancelableTask {
    public IBuildEngine BuildEngine { get; set; } = null!;
    public ITaskHost HostObject { get; set; } = null!;
    public string B3AssetsObserverRoot { get; set; } = "";
    public string B3AssetsObserverSlot { get; set; } = "";
    public string B3AssetsObserverSourceSeal { get; set; } = "";
    public string B3AssetsObserverRawAssetsPath { get; set; } = "";
    public string B3AssetsObserverPacksRoot { get; set; } = "";
    public string? B3AssetsObserverP00 { get; set; }
    public string? B3AssetsObserverP01 { get; set; }
    public string? B3AssetsObserverP02 { get; set; }
    public string? B3AssetsObserverP03 { get; set; }
    public string? B3AssetsObserverP04 { get; set; }
    public string? B3AssetsObserverP05 { get; set; }
    public string? B3AssetsObserverP06 { get; set; }
    public string? B3AssetsObserverP07 { get; set; }
    public string? B3AssetsObserverP08 { get; set; }
    public string? B3AssetsObserverP09 { get; set; }
    public string? B3AssetsObserverP10 { get; set; }
    public string? B3AssetsObserverP11 { get; set; }
    public string? B3AssetsObserverP12 { get; set; }
    public string? B3AssetsObserverP13 { get; set; }
    public string? B3AssetsObserverP14 { get; set; }
    public string? B3AssetsObserverP15 { get; set; }
    public string? B3AssetsObserverP16 { get; set; }
    public string? B3AssetsObserverP17 { get; set; }
    public string? B3AssetsObserverP18 { get; set; }
    public string? B3AssetsObserverP19 { get; set; }
    public string? B3AssetsObserverP20 { get; set; }
    public string? B3AssetsObserverP21 { get; set; }
    public string? B3AssetsObserverP22 { get; set; }
    public string? B3AssetsObserverP23 { get; set; }
    public string? B3AssetsObserverP24 { get; set; }
    public string? B3AssetsObserverP25 { get; set; }
    public string? B3AssetsObserverP26 { get; set; }
    public string? B3AssetsObserverP27 { get; set; }
    public string? B3AssetsObserverP28 { get; set; }
    public string? B3AssetsObserverP29 { get; set; }
    public string? B3AssetsObserverP30 { get; set; }
    public string? B3AssetsObserverP31 { get; set; }
    public string? B3AssetsObserverP32 { get; set; }
    public string? B3AssetsObserverP33 { get; set; }
    public string? B3AssetsObserverP34 { get; set; }
    public string? B3AssetsObserverP35 { get; set; }
    public string? B3AssetsObserverP36 { get; set; }
    public string? B3AssetsObserverP37 { get; set; }
    public string? B3AssetsObserverP38 { get; set; }
    public string? B3AssetsObserverP39 { get; set; }
    public string? B3AssetsObserverP40 { get; set; }
    public string? B3AssetsObserverP41 { get; set; }
    public string? B3AssetsObserverP42 { get; set; }
    public string? B3AssetsObserverP43 { get; set; }
    public string? B3AssetsObserverP44 { get; set; }
    public string? B3AssetsObserverP45 { get; set; }
    public string? B3AssetsObserverP46 { get; set; }
    public string? B3AssetsObserverP47 { get; set; }
    public string? B3AssetsObserverP48 { get; set; }
    public string? B3AssetsObserverP49 { get; set; }
    public string? B3AssetsObserverP50 { get; set; }
    public string? B3AssetsObserverP51 { get; set; }
    public string? B3AssetsObserverP52 { get; set; }
    public string? B3AssetsObserverP53 { get; set; }
    public string? B3AssetsObserverP54 { get; set; }
    public string? B3AssetsObserverP55 { get; set; }
    public string? B3AssetsObserverP56 { get; set; }
    public string? B3AssetsObserverP57 { get; set; }
    public string? B3AssetsObserverP58 { get; set; }
    public string? B3AssetsObserverP59 { get; set; }
    public string? B3AssetsObserverP60 { get; set; }
    public string? B3AssetsObserverP61 { get; set; }
    public string? B3AssetsObserverP62 { get; set; }
    public string? B3AssetsObserverP63 { get; set; }
    public string? B3AssetsObserverP64 { get; set; }
    public ITaskItem[]? B3AssetsObserverI00 { get; set; }
    public ITaskItem[]? B3AssetsObserverI01 { get; set; }
    public ITaskItem[]? B3AssetsObserverI02 { get; set; }
    public ITaskItem[]? B3AssetsObserverI03 { get; set; }
    public ITaskItem[]? B3AssetsObserverI04 { get; set; }
    public ITaskItem[]? B3AssetsObserverI05 { get; set; }
    public ITaskItem[]? B3AssetsObserverI06 { get; set; }
    public ITaskItem[]? B3AssetsObserverI07 { get; set; }
    public ITaskItem[]? B3AssetsObserverI08 { get; set; }
    public ITaskItem[]? B3AssetsObserverI09 { get; set; }
    public ITaskItem[]? B3AssetsObserverI10 { get; set; }
    public ITaskItem[]? B3AssetsObserverI11 { get; set; }
    public ITaskItem[]? B3AssetsObserverI12 { get; set; }
    public ITaskItem[]? B3AssetsObserverI13 { get; set; }
    public ITaskItem[]? B3AssetsObserverI14 { get; set; }
    public ITaskItem[]? B3AssetsObserverI15 { get; set; }
    public ITaskItem[]? B3AssetsObserverI16 { get; set; }
    public ITaskItem[]? B3AssetsObserverI17 { get; set; }
    public ITaskItem[]? B3AssetsObserverI18 { get; set; }
    public ITaskItem[]? B3AssetsObserverI19 { get; set; }
    public ITaskItem[]? B3AssetsObserverI20 { get; set; }
    public ITaskItem[]? B3AssetsObserverI21 { get; set; }
    public ITaskItem[]? B3AssetsObserverI22 { get; set; }
    public ITaskItem[]? B3AssetsObserverI23 { get; set; }
    public ITaskItem[]? B3AssetsObserverI24 { get; set; }
    public ITaskItem[]? B3AssetsObserverI25 { get; set; }
    public ITaskItem[]? B3AssetsObserverI26 { get; set; }
    public ITaskItem[]? B3AssetsObserverI27 { get; set; }
    public ITaskItem[]? B3AssetsObserverI28 { get; set; }
    public ITaskItem[]? B3AssetsObserverI29 { get; set; }
    public ITaskItem[]? B3AssetsObserverI30 { get; set; }
    public ITaskItem[]? B3AssetsObserverI31 { get; set; }
    public ITaskItem[]? B3AssetsObserverI32 { get; set; }
    public ITaskItem[]? B3AssetsObserverI33 { get; set; }
    public ITaskItem[]? B3AssetsObserverI34 { get; set; }
    public ITaskItem[]? B3AssetsObserverI35 { get; set; }
    public ITaskItem[]? B3AssetsObserverI36 { get; set; }
    public ITaskItem[]? B3AssetsObserverI37 { get; set; }
    public ITaskItem[]? B3AssetsObserverI38 { get; set; }
    public ITaskItem[]? B3AssetsObserverI39 { get; set; }
    public ITaskItem[]? B3AssetsObserverI40 { get; set; }
    public ITaskItem[]? B3AssetsObserverI41 { get; set; }
    public ITaskItem[]? B3AssetsObserverI42 { get; set; }
    public ITaskItem[]? B3AssetsObserverI43 { get; set; }
    public ITaskItem[]? B3AssetsObserverI44 { get; set; }
    public ITaskItem[]? B3AssetsObserverI45 { get; set; }
    private volatile bool cancelled;
    private readonly Stopwatch clock = new Stopwatch();
    private static readonly object Gate = new object();
    private static State? state;
    public void Cancel() { cancelled = true; }
    private void Tick() {
      if (cancelled || clock.ElapsedMilliseconds > 5000) throw new CaptureFault("deadline-or-cancellation");
    }
    public bool Execute() {
      clock.Start();
      if (!System.Threading.Monitor.TryEnter(Gate)) return true; // Missing slot remains incomplete.
      try {
        try {
          Tick();
          if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.ProcessArchitecture != Architecture.X64 || Marshal.SizeOf<Native.Stat>() != 144)
            throw new CaptureFault("fixed-linux-x64-abi-only");
          if (state == null) state = new State(B3AssetsObserverRoot, B3AssetsObserverSourceSeal, Tick);
          if (state.Root != B3AssetsObserverRoot || state.Seal != B3AssetsObserverSourceSeal)
            throw new CaptureFault("multiple-owner-root-or-source-seal");
          Capture(state);
        } catch (Exception e) {
          // Returning true means continuation only. Qualification never follows
          // this return value; a fault has its own accepted=false receipt.
          try { if (state != null) state.Fault(B3AssetsObserverSlot, e.GetType().Name); } catch { }
        }
      } finally { System.Threading.Monitor.Exit(Gate); }
      return true;
    }
    private static readonly string[] PropertyNames = { "MSBuildProjectFullPath", "MSBuildProjectDirectory", "MSBuildProjectName", "MSBuildVersion", "MSBuildToolsPath", "MSBuildSDKsPath", "RoslynTargetsPath", "MicrosoftNETBuildTasksAssembly", "TargetFramework", "TargetFrameworkIdentifier", "TargetFrameworkVersion", "_TargetFrameworkVersionWithoutV", "TargetFrameworkDirectory", "TargetPlatformIdentifier", "TargetPlatformVersion", "Configuration", "Platform", "DesignTimeBuild", "SkipResolvePackageAssets", "ProjectAssetsFile", "ProjectAssetsCacheFile", "ProjectDepsFilePath", "MicrosoftNETPlatformLibrary", "SelfContained", "RuntimeIdentifier", "RuntimeIdentifiers", "RuntimeIdentifierGraphPath", "CopyLocalLockFileAssemblies", "CopyLocalRuntimeTargetAssets", "UseAppHostFromAssetsFile", "EnsureRuntimePackageDependencies", "EnsureNETCoreAppRuntime", "MarkPackageReferencesAsExternallyResolved", "DisablePackageAssetsCache", "DisableLockFileFrameworks", "DisableTransitiveProjectReferences", "DisableTransitiveFrameworkReferences", "VerifyMatchingImplicitPackageVersion", "SatelliteResourceLanguages", "DefaultImplicitPackages", "GenerateErrorForMissingTargetingPacks", "_NuGetRestoreSupported", "NetCoreTargetingPackRoot", "PackageConflictPreferredPackages", "GenerateDependencyFile", "IncludeMainProjectInDepsFile", "IncludeFileVersionsInDependencyFile", "IncludeProjectsNotInAssetsFileInDepsFile", "AssemblyName", "TargetExt", "Version", "Language", "CompilerApiVersion", "EmitAssetsLogMessages", "DotNetAppHostExecutableNameWithoutExtension", "_DotNetAppHostExecutableNameWithoutExtension", "PackAsToolShimRuntimeIdentifiers", "ContinueOnError", "UsingNETSdkDefaults", "MSBuildAllProjects", "UseSharedCompilation", "CustomAfterMicrosoftCommonTargets", "GenerateAssemblyInfo", "OutputPath", "IntermediateOutputPath" };
    private static readonly string[] ItemNames = { "PackageReference", "RuntimeFramework", "ExpectedPlatformPackages", "_PackAsToolShimRuntimeIdentifiers", "FrameworkReference", "ResolvedTargetingPack", "ResolvedFrameworkReference", "ResolvedRuntimePack", "TransitiveFrameworkReference", "_UsedRuntimeFramework", "Reference", "Analyzer", "ResolvedCompileFileDefinitions", "RuntimeCopyLocalItems", "NativeCopyLocalItems", "ResourceCopyLocalItems", "RuntimeTargetsCopyLocalItems", "ReferenceCopyLocalPaths", "_ContentCopyLocalItems", "_ResolvedCopyLocalBuildAssets", "_NativeRestoredAppHostNETCore", "PlatformManifestsFromTargetingPacks", "PackageConflictPlatformManifests", "PackageConflictOverrides", "_RuntimeAssetsForConflictResolution", "_ReferencesWithoutConflicts", "_AnalyzersWithoutConflicts", "_ReferenceCopyLocalPathsWithoutConflicts", "_ConflictPackageFiles", "RuntimePackAsset", "ReferencePath", "ReferenceDependencyPaths", "ReferenceSatellitePaths", "_ReferenceAssemblies", "UserRuntimeAssembly", "IntermediateSatelliteAssembliesWithTargetPath", "DependencyFileCompilerOptions", "_ValidRuntimeIdentifierPlatformsForAssets", "_KnownRuntimeIdentiferPlatforms", "_KnownRuntimeIdentifierPlatformsForTargetFramework", "_ExcludedKnownRuntimeIdentiferPlatforms", "AssetsFilePackageFolder", "PackageDependencies", "ProjectReference", "RuntimeHostConfigurationOption", "_ApphostsForShimRuntimeIdentifiersResolvePackageAssets" };
    private static readonly string[] MetadataNames = { "FullPath", "Identity", "OriginalItemSpec", "HintPath", "ReferenceSourceTarget", "NuGetPackageId", "NuGetPackageVersion", "NuGetSourceType", "NuGetIsFrameworkReference", "Version", "FrameworkName", "FrameworkReferenceName", "FrameworkReferenceVersion", "TargetFramework", "TargetingPackFormat", "Profile", "Path", "PackageDirectory", "PathInPackage", "PackageConflictPreferredPackages", "OverriddenPackages", "CopyLocal", "CopyToPublishDirectory", "CopyToOutputDirectory", "DestinationSubPath", "DestinationSubDirectory", "TargetPath", "AssetType", "RuntimeIdentifier", "Culture", "ConflictItemType", "Private", "ExternallyResolved", "Resolved", "IsImplicitlyDefined", "Name", "Publish", "Aliases", "SourceType", "SourcePath", "AssemblyVersion", "FileVersion", "TargetFrameworkIdentifier", "TargetFrameworkVersion" };
    private static readonly string[] Slots = { "ResolvePackageAssets.before", "ResolvePackageAssets.after", "ResolveTargetingPackAssets.before", "ResolveTargetingPackAssets.after", "ResolveLockFileCopyLocalFiles.before", "ResolveLockFileCopyLocalFiles.after", "_HandlePackageFileConflicts.before", "_HandlePackageFileConflicts.after", "GenerateBuildDependencyFile.before", "GenerateBuildDependencyFile.after" };
    private string?[] Properties() { return new string?[] {
B3AssetsObserverP00, B3AssetsObserverP01, B3AssetsObserverP02, B3AssetsObserverP03, B3AssetsObserverP04, B3AssetsObserverP05, B3AssetsObserverP06, B3AssetsObserverP07, B3AssetsObserverP08, B3AssetsObserverP09, B3AssetsObserverP10, B3AssetsObserverP11, B3AssetsObserverP12, B3AssetsObserverP13, B3AssetsObserverP14, B3AssetsObserverP15, B3AssetsObserverP16, B3AssetsObserverP17, B3AssetsObserverP18, B3AssetsObserverP19, B3AssetsObserverP20, B3AssetsObserverP21, B3AssetsObserverP22, B3AssetsObserverP23, B3AssetsObserverP24, B3AssetsObserverP25, B3AssetsObserverP26, B3AssetsObserverP27, B3AssetsObserverP28, B3AssetsObserverP29, B3AssetsObserverP30, B3AssetsObserverP31, B3AssetsObserverP32, B3AssetsObserverP33, B3AssetsObserverP34, B3AssetsObserverP35, B3AssetsObserverP36, B3AssetsObserverP37, B3AssetsObserverP38, B3AssetsObserverP39, B3AssetsObserverP40, B3AssetsObserverP41, B3AssetsObserverP42, B3AssetsObserverP43, B3AssetsObserverP44, B3AssetsObserverP45, B3AssetsObserverP46, B3AssetsObserverP47, B3AssetsObserverP48, B3AssetsObserverP49, B3AssetsObserverP50, B3AssetsObserverP51, B3AssetsObserverP52, B3AssetsObserverP53, B3AssetsObserverP54, B3AssetsObserverP55, B3AssetsObserverP56, B3AssetsObserverP57, B3AssetsObserverP58, B3AssetsObserverP59, B3AssetsObserverP60, B3AssetsObserverP61, B3AssetsObserverP62, B3AssetsObserverP63, B3AssetsObserverP64 }; }
    private ITaskItem[]?[] Items() { return new ITaskItem[]?[] {
B3AssetsObserverI00, B3AssetsObserverI01, B3AssetsObserverI02, B3AssetsObserverI03, B3AssetsObserverI04, B3AssetsObserverI05, B3AssetsObserverI06, B3AssetsObserverI07, B3AssetsObserverI08, B3AssetsObserverI09, B3AssetsObserverI10, B3AssetsObserverI11, B3AssetsObserverI12, B3AssetsObserverI13, B3AssetsObserverI14, B3AssetsObserverI15, B3AssetsObserverI16, B3AssetsObserverI17, B3AssetsObserverI18, B3AssetsObserverI19, B3AssetsObserverI20, B3AssetsObserverI21, B3AssetsObserverI22, B3AssetsObserverI23, B3AssetsObserverI24, B3AssetsObserverI25, B3AssetsObserverI26, B3AssetsObserverI27, B3AssetsObserverI28, B3AssetsObserverI29, B3AssetsObserverI30, B3AssetsObserverI31, B3AssetsObserverI32, B3AssetsObserverI33, B3AssetsObserverI34, B3AssetsObserverI35, B3AssetsObserverI36, B3AssetsObserverI37, B3AssetsObserverI38, B3AssetsObserverI39, B3AssetsObserverI40, B3AssetsObserverI41, B3AssetsObserverI42, B3AssetsObserverI43, B3AssetsObserverI44, B3AssetsObserverI45 }; }
    internal sealed class Row {
      internal readonly ITaskItem Source;
      internal readonly string? Spec;
      internal readonly string?[] Metadata;
      internal Row(ITaskItem source, string? spec, string?[] metadata) {
        Source = source; Spec = spec; Metadata = metadata;
      }
    }
    private sealed class Snapshot {
      internal readonly string?[] Properties;
      internal readonly ITaskItem[]?[] Inputs;
      internal readonly Row[]?[] Rows = new Row[]?[46];
      internal long Units;
      internal int RowCount, Gets;
      internal Snapshot(string?[] properties, ITaskItem[]?[] inputs) { Properties = properties; Inputs = inputs; }
    }
    private void Value(Snapshot snap, string? value, bool imports = false) {
      Tick();
      if (value == null) return;
      if (value.Length > (imports ? 131072 : 16384)) throw new CaptureFault("string-limit");
      snap.Units = checked(snap.Units + value.Length);
      if (snap.Units > 2097152) throw new CaptureFault("frame-string-units-limit");
      // Check UTF16 and exact encoded size before retaining/encoding the value.
      Json.StringBytes(value, Tick);
    }
    private Snapshot TakeSnapshot(State st) {
      Tick();
      var properties = Properties();
      var inputs = Items();
      int total = 0;
      for (int i = 0; i < inputs.Length; i++) {
        int count = inputs[i]?.Length ?? 0;
        if (count > 4096) throw new CaptureFault("array-row-limit");
        total = checked(total + count);
      }
      // Both initial and recheck passes share the finite getter allowance.
      if (total > 8192 || checked((long)total * 44 * 2) > 450000 || st.Rows + total > 65536)
        throw new CaptureFault("frame-or-aggregate-row-or-get-limit");
      var snap = new Snapshot(properties, inputs) { RowCount = total };
      for (int i = 0; i < properties.Length; i++) Value(snap, properties[i], PropertyNames[i] == "MSBuildAllProjects");
      for (int i = 0; i < inputs.Length; i++) {
        var input = inputs[i];
        if (input == null) continue;
        // Holder size follows checked counts; input references are never cloned
        // or mutated and duplicate occurrences retain their own ordered rows.
        var rows = new Row[input.Length];
        snap.Rows[i] = rows;
        for (int j = 0; j < rows.Length; j++) {
          Tick();
          var item = input[j] ?? throw new CaptureFault("null-item");
          string? spec = item.ItemSpec;
          Value(snap, spec);
          var metadata = new string?[44];
          for (int k = 0; k < metadata.Length; k++) {
            if (++snap.Gets > 450000) throw new CaptureFault("metadata-get-limit");
            var value = item.GetMetadata(MetadataNames[k]);
            Value(snap, value);
            metadata[k] = value;
          }
          rows[j] = new Row(item, spec, metadata);
        }
      }
      return snap;
    }
    private void Recheck(Snapshot snap) {
      Tick();
      var properties = Properties();
      var inputs = Items();
      for (int i = 0; i < properties.Length; i++)
        if (!String.Equals(properties[i], snap.Properties[i], StringComparison.Ordinal)) throw new CaptureFault("property-changed");
      for (int i = 0; i < inputs.Length; i++) {
        var input = inputs[i];
        if (!Object.ReferenceEquals(input, snap.Inputs[i])) throw new CaptureFault("array-binding-changed");
        var rows = snap.Rows[i];
        if (input == null) { if (rows != null) throw new CaptureFault("null-array-changed"); continue; }
        if (rows == null || input.Length != rows.Length) throw new CaptureFault("array-length-changed");
        for (int j = 0; j < rows.Length; j++) {
          Tick();
          var row = rows[j];
          if (!Object.ReferenceEquals(row.Source, input[j]) ||
              !String.Equals(row.Source.ItemSpec, row.Spec, StringComparison.Ordinal)) throw new CaptureFault("item-changed");
          for (int k = 0; k < 44; k++) {
            if (++snap.Gets > 450000) throw new CaptureFault("metadata-get-limit");
            Tick();
            if (!String.Equals(row.Source.GetMetadata(MetadataNames[k]), row.Metadata[k], StringComparison.Ordinal))
              throw new CaptureFault("metadata-changed");
          }
        }
      }
    }
    private void Capture(State st) {
      int slot = Array.IndexOf(Slots, B3AssetsObserverSlot);
      if (slot < 0 || st.Seen[slot]) throw new CaptureFault("unknown-or-duplicate-slot");
      st.Seen[slot] = true; // An attempted faulted hook cannot be retried as fresh.
      if (slot % 2 == 1 && !st.Completed[slot - 1]) throw new CaptureFault("after-without-complete-before");
      st.CheckFirst(Tick);
      st.AnchorReadBytes = 0;
      var snap = TakeSnapshot(st);
      if (snap.Properties[8] != "net8.0" || snap.Properties[15] != "Release" || snap.Properties[16] != "AnyCPU" ||
          String.Equals(snap.Properties[17], "true", StringComparison.OrdinalIgnoreCase) ||
          snap.Properties[0] != st.OwnerPath || snap.Properties[61] != st.ImportPath)
        throw new CaptureFault("wrong-owner-or-import");
      // These are separate boundary-byte observations, not proof that SDK tasks
      // opened tables or consumed these item/property values.
      st.Anchors(Tick);
      if (slot == 0) {
        if (B3AssetsObserverRawAssetsPath != st.OwnerDirectory + "/obj/project.assets.json" ||
            Path.GetFullPath(Path.IsPathRooted(snap.Properties[19] ?? "") ? snap.Properties[19]! : st.OwnerDirectory + "/" + (snap.Properties[19] ?? "")) != B3AssetsObserverRawAssetsPath)
          throw new CaptureFault("raw-assets-path");
        st.FirstRaw(B3AssetsObserverRawAssetsPath, Tick);
      } else st.RecheckRaw(Tick);
      if (slot == 3) st.SelectTables(snap.Rows[5], B3AssetsObserverPacksRoot, Tick);
      else st.RecheckTables(Tick);
      int sequence = st.Sequence;
      Action<Json> emit = json => Emit(json, snap, st, sequence);
      long bytes = Json.Count(emit, Tick);
      if (bytes > 8388608 || st.FrameBytes + bytes > 29360128) throw new CaptureFault("frame-byte-limit");
      string partial = "frames/" + sequence.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + ".partial";
      using (var writer = Json.File(st, partial, bytes, Tick)) {
        emit(writer); writer.Finish();
      }
      Recheck(snap);
      st.CheckFirst(Tick);
      st.RecheckRaw(Tick);
      st.RecheckTables(Tick);
      st.Anchors(Tick);
      st.Publish(partial, "frames/" + sequence.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + ".json");
      st.FrameBytes += bytes; st.Rows += snap.RowCount; st.Completed[slot] = true; st.Sequence++;
    }
    private void Emit(Json json, Snapshot snap, State st, int sequence) {
      json.Raw("{\"schemaVersion\":1,\"slot\":"); json.String(B3AssetsObserverSlot);
      json.Raw(",\"sequence\":"); json.Number(sequence);
      json.Raw(",\"owner\":{\"projectRelativePath\":\"Source/Dafny/Dafny.csproj\",\"framework\":\"net8.0\",\"configuration\":\"Release\"},");
      json.Raw("\"targetBodyExecution\":\"not-observed\",\"taskParametersObserved\":false,\"status\":\"complete-boundary-observation\",\"properties\":[");
      for (int i = 0; i < 65; i++) {
        if (i != 0) json.Raw(","); json.Raw("{\"name\":"); json.String(PropertyNames[i]);
        json.Raw(",\"value\":"); json.String(snap.Properties[i]); json.Raw("}");
      }
      json.Raw("],\"itemSets\":[");
      for (int i = 0; i < 46; i++) {
        if (i != 0) json.Raw(","); json.Raw("{\"name\":"); json.String(ItemNames[i]);
        var rows = snap.Rows[i];
        json.Raw(rows == null ? ",\"arrayBinding\":\"null\",\"rows\":[]}" : ",\"arrayBinding\":\"array\",\"rows\":[");
        if (rows == null) continue;
        for (int j = 0; j < rows.Length; j++) {
          if (j != 0) json.Raw(","); json.Raw("{\"ordinal\":"); json.Number(j);
          json.Raw(",\"itemSpec\":"); json.String(rows[j].Spec);
          json.Raw(",\"metadataPresence\":\"not-observed\",\"metadata\":[");
          for (int k = 0; k < 44; k++) { if (k != 0) json.Raw(","); json.String(rows[j].Metadata[k]); }
          json.Raw("]}");
        }
        json.Raw("]}");
      }
      json.Raw("],\"sourceSeal\":"); json.String(st.Seal);
      json.Raw(",\"helperImageSha256\":"); json.String(st.ImageHash); json.Raw("}\n");
    }
  }
  internal sealed class CaptureFault : Exception { internal CaptureFault(string reason) : base(reason) { } }

  // Fixed x86-64 Linux/glibc descriptor API, pending independent ABI primary-
  // source review. No FileStream path constructor is used to imply NOFOLLOW.
  internal static class Native {
    internal const int Directory = 65536, NoFollow = 131072, CloseExec = 524288, Create = 64, Exclusive = 128;
    [StructLayout(LayoutKind.Sequential)] internal struct Stat {
      internal ulong Dev, Ino, Links;
      internal uint Mode, Uid, Gid, Pad;
      internal ulong Rdev;
      internal long Size, BlockSize, Blocks, ASeconds, ANanoseconds, MSeconds, MNanoseconds, CSeconds, CNanoseconds;
      internal long Reserved0, Reserved1, Reserved2;
      internal bool Same(Stat other) => Dev == other.Dev && Ino == other.Ino && Links == other.Links &&
        Mode == other.Mode && Size == other.Size && MSeconds == other.MSeconds && MNanoseconds == other.MNanoseconds &&
        CSeconds == other.CSeconds && CNanoseconds == other.CNanoseconds;
    }
    [DllImport("libc", EntryPoint="open", SetLastError=true)] internal static extern int Open(string path, int flags, uint mode);
    [DllImport("libc", EntryPoint="openat", SetLastError=true)] internal static extern int OpenAt(int directory, string path, int flags, uint mode);
    [DllImport("libc", EntryPoint="fstat", SetLastError=true)] internal static extern int FStat(int fd, out Stat stat);
    [DllImport("libc", EntryPoint="mkdirat", SetLastError=true)] internal static extern int MkdirAt(int fd, string name, uint mode);
    [DllImport("libc", EntryPoint="linkat", SetLastError=true)] internal static extern int LinkAt(int oldFd, string oldName, int newFd, string newName, int flags);
    [DllImport("libc", EntryPoint="unlinkat", SetLastError=true)] internal static extern int UnlinkAt(int fd, string name, int flags);
    internal static SafeFileHandle Handle(int fd) {
      if (fd < 0) throw new CaptureFault("descriptor-open-errno-" + Marshal.GetLastWin32Error());
      return new SafeFileHandle((IntPtr)fd, true);
    }
    internal static int Fd(SafeFileHandle handle) => checked((int)handle.DangerousGetHandle());
    internal static Stat Info(SafeFileHandle handle) {
      if (FStat(Fd(handle), out Stat stat) != 0) throw new CaptureFault("fstat");
      return stat;
    }
  }
  internal sealed class Route : IDisposable {
    private readonly List<SafeFileHandle> handles = new List<SafeFileHandle>();
    private readonly List<Native.Stat> initial = new List<Native.Stat>();
    internal SafeFileHandle Parent => handles[handles.Count - 1];
    internal readonly string Leaf;
    internal static void PathGrammar(string path) {
      if (path.Length < 2 || path.Length > 4096 || path[0] != '/' || path.EndsWith("/", StringComparison.Ordinal))
        throw new CaptureFault("absolute-path-grammar");
      int components = 0, start = 1;
      for (int i = 1; i <= path.Length; i++) {
        if (i == path.Length || path[i] == '/') {
          int length = i - start;
          if (++components > 128 || length == 0 || length > 255 ||
              (length == 1 && path[start] == '.') || (length == 2 && path[start] == '.' && path[start + 1] == '.'))
            throw new CaptureFault("path-component-grammar");
          start = i + 1;
        } else {
          char c = path[i];
          if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') &&
              c != '_' && c != '-' && c != '.') throw new CaptureFault("path-character-grammar");
        }
      }
    }
    internal Route(string path, bool createParents = false) {
      PathGrammar(path);
      int slash = path.LastIndexOf('/');
      Leaf = path.Substring(slash + 1);
      try {
        Add(Native.Handle(Native.Open("/", Native.Directory | Native.NoFollow | Native.CloseExec, 0)));
        int start = 1;
        for (int i = 1; i <= slash; i++) if (path[i] == '/') {
          string part = path.Substring(start, i - start); start = i + 1;
          if (part.Length == 0) continue;
          int fd = Native.OpenAt(Native.Fd(Parent), part, Native.Directory | Native.NoFollow | Native.CloseExec, 0);
          if (fd < 0 && createParents && Marshal.GetLastWin32Error() == 2) {
            if (Native.MkdirAt(Native.Fd(Parent), part, 448) != 0) throw new CaptureFault("mkdirat");
            // Creating own directories legitimately changes the parent's times.
            initial[initial.Count - 1] = Native.Info(Parent);
            fd = Native.OpenAt(Native.Fd(Parent), part, Native.Directory | Native.NoFollow | Native.CloseExec, 0);
          }
          Add(Native.Handle(fd));
        }
      } catch { Dispose(); throw; }
    }
    private void Add(SafeFileHandle handle) {
      var info = Native.Info(handle);
      if ((info.Mode & 61440) != 16384) { handle.Dispose(); throw new CaptureFault("non-directory-ancestor"); }
      handles.Add(handle); initial.Add(info);
    }
    internal void Stable() {
      // Holding all ancestors prevents symlink substitution; equality also
      // detects directory identity/time changes during this bounded operation.
      for (int i = 0; i < handles.Count; i++) if (!initial[i].Same(Native.Info(handles[i])))
        throw new CaptureFault("ancestor-changed");
    }
    internal bool Absent(Action tick) {
      tick(); Stable();
      int fd = Native.OpenAt(Native.Fd(Parent), Leaf, Native.NoFollow | Native.CloseExec | 2048, 0);
      int error = Marshal.GetLastWin32Error();
      if (fd >= 0) { Native.Handle(fd).Dispose(); return false; }
      Stable(); tick();
      if (error == 2 || error == 20) return true;
      throw new CaptureFault("unknown-absence-errno-" + error);
    }
    internal SafeFileHandle ReadHandle() => Native.Handle(Native.OpenAt(Native.Fd(Parent), Leaf, Native.NoFollow | Native.CloseExec | 2048, 0));
    internal SafeFileHandle CreateHandle() => Native.Handle(Native.OpenAt(Native.Fd(Parent), Leaf, 1 | Native.Create | Native.Exclusive | Native.NoFollow | Native.CloseExec, 384));
    public void Dispose() { for (int i = handles.Count - 1; i >= 0; i--) handles[i].Dispose(); handles.Clear(); }
  }
  internal sealed class FileIdentity {
    internal readonly long Bytes;
    internal readonly string Hash;
    internal FileIdentity(long bytes, string hash) { Bytes = bytes; Hash = hash; }
    internal bool Same(FileIdentity other) => Bytes == other.Bytes && Hash == other.Hash;
  }
  internal static class Files {
    internal static string Hex(byte[] bytes) {
      const string digits = "0123456789abcdef";
      char[] result = new char[checked(bytes.Length * 2)];
      for (int i = 0; i < bytes.Length; i++) { result[2 * i] = digits[bytes[i] >> 4]; result[2 * i + 1] = digits[bytes[i] & 15]; }
      return new string(result);
    }
    internal static FileIdentity Read(string path, long maximum, Action tick, Action<byte[], int>? consume = null, Action<int>? attempted = null) {
      using (var route = new Route(path)) using (var handle = route.ReadHandle()) {
        Native.Stat before = Native.Info(handle);
        if ((before.Mode & 61440) != 32768 || before.Size < 0 || before.Size > maximum) throw new CaptureFault("regular-file-length");
        using (var stream = new FileStream(handle, FileAccess.Read, 65536, false)) using (var hash = SHA256.Create()) {
          byte[] buffer = new byte[65536]; long length = 0;
          while (true) {
            tick(); int count = stream.Read(buffer, 0, buffer.Length); if (count == 0) break;
            attempted?.Invoke(count);
            length = checked(length + count);
            if (length > maximum || length > before.Size) throw new CaptureFault("file-growing");
            hash.TransformBlock(buffer, 0, count, null, 0); consume?.Invoke(buffer, count);
          }
          hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
          if (length != before.Size || !before.Same(Native.Info(handle))) throw new CaptureFault("file-changed");
          route.Stable(); tick(); return new FileIdentity(length, Hex(hash.Hash!));
        }
      }
    }
    internal static string[] Lines(string path, Action tick) {
      byte[] bytes = new byte[65536]; int length = 0;
      Read(path, bytes.Length, tick, (buffer, count) => { Buffer.BlockCopy(buffer, 0, bytes, length, count); length += count; });
      if (length == 0 || bytes[length - 1] != 10) throw new CaptureFault("control-terminal-lf");
      int rows = 0;
      for (int i = 0; i < length; i++) { if (bytes[i] == 10) rows++; else if (bytes[i] < 32 || bytes[i] > 126) throw new CaptureFault("control-ascii"); }
      if (rows > 64) throw new CaptureFault("control-row-limit");
      var result = new string[rows]; int start = 0, row = 0;
      for (int i = 0; i < length; i++) if (bytes[i] == 10) {
        char[] chars = new char[i - start]; for (int j = 0; j < chars.Length; j++) chars[j] = (char)bytes[start + j];
        result[row++] = new string(chars); start = i + 1;
      }
      return result;
    }
  }
  internal sealed class State {
    internal readonly string Root, Seal, OwnerPath, OwnerDirectory, ImagePath, ImageHash, ImportPath;
    internal readonly bool[] Seen = new bool[10], Completed = new bool[10];
    internal int Sequence, Rows, Faults;
    internal long Bytes, FrameBytes, TableBytes, AnchorReadBytes;
    private readonly string[] first, prepared;
    private readonly string[] anchors;
    private string? rawPath;
    private FileIdentity? raw;
    private readonly List<Tuple<string, FileIdentity?>> tables = new List<Tuple<string, FileIdentity?>>();
    private bool tablesSelected;
    internal State(string root, string seal, Action tick) {
      Route.PathGrammar(root);
      if (!HashGrammar(seal)) throw new CaptureFault("source-seal-grammar");
      Root = root; Seal = seal;
      first = Files.Lines(root + "/first/inventory.txt", tick);
      var association = Files.Lines(root + "/first/association.txt", tick);
      if (association.Length != 7 || association[0] != "B3AssetsObserverFirst/1" || association[1] != seal ||
          !HashGrammar(association[3]) || !HashGrammar(association[5])) throw new CaptureFault("first-association");
      ImagePath = association[2]; ImageHash = association[3]; ImportPath = association[6];
      OwnerPath = association[4]; Route.PathGrammar(OwnerPath);
      if (!OwnerPath.EndsWith("/Source/Dafny/Dafny.csproj", StringComparison.Ordinal)) throw new CaptureFault("owner-path");
      OwnerDirectory = OwnerPath.Substring(0, OwnerPath.Length - "/Dafny.csproj".Length);
      string repository = OwnerPath.Substring(0, OwnerPath.Length - "/Source/Dafny/Dafny.csproj".Length);
      string work = repository + "/out/b3-sdk-observer-work";
      if (Root != repository + "/out/b3-if-guard-corpus/sdk-observer" ||
          ImagePath != work + "/build/B3AssetsObserver.dll" || ImportPath != work + "/source/B3AssetsObserver.targets")
        throw new CaptureFault("separate-own-roots");
      var control = Files.Lines(Root + "/first/compiler-inputs.txt", tick);
      if (control.Length != 14 || control[0] != "B3AssetsObserverPreload/1" || control[3] != ImagePath ||
          control[1] != work + "/source/B3AssetsObserver.cs" || control[10] != Root || control[11] != Seal ||
          control[12] != "true" || control[13] != "0" || Files.Read(Root + "/first/compiler-inputs.txt", 65536, tick).Hash != association[5])
        throw new CaptureFault("compiler-control-association");
      string[] roles = { "helper.dll", "compiler-inputs.txt", "source.cs", "targets.xml", "preserve.py", "anchors.txt", "python-pins.txt",
        "declared-options.txt", "inputs.props", "netstandard.dll", "build-framework.dll", "bootstrap-pins.txt", "compiler-arguments.txt" };
      string[] lives = { ImagePath, work + "/control/compiler-inputs.txt", control[1], ImportPath, work + "/source/preserve-observer.py",
        work + "/source/anchors.txt", work + "/source/python-pins.txt", work + "/source/declared-options.txt", work + "/source/B3AssetsObserver.inputs.props",
        control[4], control[6], work + "/source/bootstrap-pins.txt", work + "/control/compiler-arguments.txt" };
      if (first.Length != roles.Length) throw new CaptureFault("first-fixed-role-count");
      for (int i = 0; i < first.Length; i++) {
        var fields = Parse(first[i], 4);
        long maximum = i == 0 ? 524288 : i == 2 || i == 3 || i == 4 ? 262144 : i == 9 || i == 10 ? 8388608 : 65536;
        if (fields[0] != lives[i] || fields[1] != Root + "/first/" + roles[i] || Decimal(fields[2]) > maximum || !HashGrammar(fields[3]))
          throw new CaptureFault("first-fixed-role-identity");
      }
      prepared = Files.Lines(root + "/prepared/inventory.txt", tick);
      string[] preparedNames = { "B3AssetsObserver.cs", "B3AssetsObserver.targets", "preserve-observer.py", "declared-options.txt",
        "anchors.txt", "python-pins.txt", "B3AssetsObserver.inputs.props", "bootstrap-pins.txt" };
      if (prepared.Length != preparedNames.Length) throw new CaptureFault("prepared-fixed-source-count");
      for (int i = 0; i < prepared.Length; i++) {
        var fields = Parse(prepared[i], 4);
        if (fields[0] != work + "/source/" + preparedNames[i] || fields[1] != Root + "/prepared/" + preparedNames[i] ||
            Decimal(fields[2]) > (i < 3 ? 262144 : 65536) || !HashGrammar(fields[3]))
          throw new CaptureFault("prepared-fixed-source-identity");
        Bytes = checked(Bytes + Decimal(fields[2]));
      }
      Bytes = checked(Bytes + Files.Read(root + "/prepared/inventory.txt", 65536, tick).Bytes);
      var ready = Files.Lines(root + "/first/ready", tick);
      if (ready.Length != 1 || ready[0] != ImageHash) throw new CaptureFault("not-preserved-or-compiler-unsuccessful");
      anchors = Files.Lines(root + "/first/anchors.txt", tick);
      if (anchors.Length != 5 || anchors[0] != "B3AssetsObserverAnchors/1") throw new CaptureFault("anchor-pin-count");
      // Bootstrap outputs are fixed and complete before helper load. Their byte
      // aggregate is independently bounded/rechecked by the coordinator.
      foreach (var line in first) {
        var fields = Parse(line, 4);
        if (fields[1].StartsWith(Root + "/first/", StringComparison.Ordinal)) {
          long size = Decimal(fields[2]); Bytes = checked(Bytes + size);
        }
      }
      Bytes = checked(Bytes + Files.Read(root + "/first/inventory.txt", 65536, tick).Bytes +
        Files.Read(root + "/first/association.txt", 65536, tick).Bytes +
        Files.Read(root + "/first/ready", 65536, tick).Bytes);
      if (Bytes > 8388608) throw new CaptureFault("bootstrap-first-aggregate");
    }
    internal static bool HashGrammar(string value) {
      if (value.Length != 64) return false;
      foreach (char c in value) if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
      return true;
    }
    private static long Decimal(string text) {
      if (text.Length == 0 || text.Length > 10 || (text.Length > 1 && text[0] == '0')) throw new CaptureFault("decimal-grammar");
      long n = 0; foreach (char c in text) { if (c < '0' || c > '9') throw new CaptureFault("decimal-grammar"); n = checked(n * 10 + c - '0'); }
      return n;
    }
    private static string[] Parse(string line, int count) {
      var fields = line.Split('|');
      if (fields.Length != count) throw new CaptureFault("identity-field-count");
      return fields;
    }
    internal void CheckFirst(Action tick) {
      for (int set = 0; set < 2; set++) foreach (var line in set == 0 ? first : prepared) {
        tick(); var fields = Parse(line, 4);
        Route.PathGrammar(fields[0]); Route.PathGrammar(fields[1]);
        long size = Decimal(fields[2]); if (size > 8388608 || !HashGrammar(fields[3])) throw new CaptureFault("first-pin-grammar");
        var live = Files.Read(fields[0], size, tick); var copy = Files.Read(fields[1], size, tick);
        if (live.Bytes != size || live.Hash != fields[3] || !live.Same(copy)) throw new CaptureFault("first-input-or-copy-changed");
      }
      var image = Files.Read(ImagePath, 524288, tick);
      if (image.Hash != ImageHash) throw new CaptureFault("helper-image-changed");
    }
    internal void Anchors(Action tick) {
      var assemblies = new System.Reflection.Assembly[] { typeof(CaptureTask).Assembly, typeof(ITask).Assembly,
        typeof(Object).Assembly, typeof(FileStream).Assembly, typeof(SHA256).Assembly };
      string[] names = { "CaptureTask", "ITask", "Object", "FileStream", "SHA256" };
      Action<int> charge = count => { AnchorReadBytes = checked(AnchorReadBytes + count); if (AnchorReadBytes > 67108864) throw new CaptureFault("anchor-read-aggregate"); };
      // Hash once, retain only five bounded identity records, then serialize
      // without rereading all anchors inside a counting/writing double pass.
      var records = new List<Tuple<string, string, string, FileIdentity>>(5);
      for (int i = 0; i < 5; i++) {
        tick(); string location = assemblies[i].Location;
        string path = ImagePath, hash = ImageHash; long size = 524288;
        if (i != 0) { var fields = Parse(anchors[i], 4); if (fields[0] != names[i]) throw new CaptureFault("anchor-role");
          path = fields[1]; size = Decimal(fields[2]); hash = fields[3]; }
        if (location != path || size > 33554432 || !HashGrammar(hash)) throw new CaptureFault("anchor-location-or-pin");
        var info = Files.Read(location, size, tick, null, charge);
        if ((i != 0 && info.Bytes != size) || info.Hash != hash) throw new CaptureFault("anchor-bytes-changed");
        string fullName = assemblies[i].FullName ?? throw new CaptureFault("missing-anchor-name");
        if (fullName.Length > 4096) throw new CaptureFault("anchor-name-limit");
        records.Add(Tuple.Create(names[i], fullName, location, info));
      }
      string target = "anchors/" + Sequence.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + ".json";
      // The second same-frame call validates hashes; it never overwrites the
      // immutable first anchor record published at that sequence.
      using (var route = new Route(Root + "/" + target, true)) if (!route.Absent(tick)) return;
      WriteJson(target, json => {
        json.Raw("{\"schemaVersion\":1,\"scope\":\"named-type-assembly-anchors-only\",\"atomicImageAttestation\":false,\"roles\":[");
        for (int i = 0; i < records.Count; i++) { var row = records[i]; if (i != 0) json.Raw(",");
          json.Raw("{\"role\":"); json.String(row.Item1); json.Raw(",\"assemblyName\":"); json.String(row.Item2);
          json.Raw(",\"location\":"); json.String(row.Item3); json.Raw(",\"bytes\":"); json.Number(row.Item4.Bytes);
          json.Raw(",\"sha256\":"); json.String(row.Item4.Hash); json.Raw("}"); }
        json.Raw("]}\n");
      }, tick, 32768);
    }
    internal void FirstRaw(string path, Action tick) {
      if (raw != null) throw new CaptureFault("raw-snapshot-repeated");
      rawPath = path; raw = Copy(path, "raw-assets/project.assets.json", 16777216, tick);
      WriteJson("raw-assets/identity.json", json => {
        json.Raw("{\"schemaVersion\":1,\"slot\":\"ResolvePackageAssets.before\",\"path\":"); json.String(path);
        json.Raw(",\"bytes\":"); json.Number(raw.Bytes); json.Raw(",\"sha256\":"); json.String(raw.Hash);
        json.Raw(",\"targetBodyExecution\":\"not-observed\",\"parsed\":false}\n");
      }, tick, 32768);
    }
    internal void RecheckRaw(Action tick) {
      if (raw == null || rawPath == null) return;
      if (!Files.Read(rawPath, 16777216, tick).Same(raw) ||
          !Files.Read(Root + "/raw-assets/project.assets.json", 16777216, tick).Same(raw)) throw new CaptureFault("raw-assets-changed");
    }
    internal void SelectTables(CaptureTask.Row[]? rows, string packsRoot, Action tick) {
      if (tablesSelected) throw new CaptureFault("table-selection-repeated");
      tablesSelected = true; Route.PathGrammar(packsRoot);
      if (rows == null || rows.Length > 4) throw new CaptureFault("selected-pack-row-limit-or-null");
      string[] tails = { "data/PlatformManifest.txt", "data/PackageOverrides.txt", "data/FrameworkList.xml" };
      var records = new List<Tuple<int, string, string, string, FileIdentity?>>(12);
      for (int i = 0; i < rows.Length; i++) {
        string path = rows[i].Metadata[16] ?? "";
        Route.PathGrammar(path);
        if (!path.StartsWith(packsRoot + "/", StringComparison.Ordinal)) throw new CaptureFault("pack-root-containment");
        foreach (string tail in tails) {
          tick(); string candidate = path + "/" + tail;
          FileIdentity? info = null; string status = "absent";
          try {
            using (var route = new Route(candidate)) {
              if (!route.Absent(tick)) {
                string temporary = "tables/candidate-" + (i * 3 + Array.IndexOf(tails, tail)).ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + ".partial";
                info = Copy(candidate, temporary, 1048576, tick, count => {
                  TableBytes = checked(TableBytes + count);
                  if (TableBytes > 8388608) throw new CaptureFault("table-read-aggregate");
                });
                string destination = "tables/" + info.Hash + ".bin";
                using (var copyRoute = new Route(Root + "/" + destination, true)) {
                  if (copyRoute.Absent(tick)) {
                    Publish(temporary, destination);
                  } else {
                    if (!Files.Read(Root + "/" + destination, 1048576, tick).Same(info)) throw new CaptureFault("table-copy-changed");
                    using (var duplicate = new Route(Root + "/" + temporary))
                      if (Native.UnlinkAt(Native.Fd(duplicate.Parent), duplicate.Leaf, 0) != 0) throw new CaptureFault("duplicate-prefix-unlink");
                  }
                }
                status = "source-derived-candidate-bytes";
              }
            }
          } catch (CaptureFault) { status = "unqualified-path-or-byte-fault"; info = null; }
          tables.Add(Tuple.Create(candidate, info)); records.Add(Tuple.Create(i, path, candidate, status, info));
        }
      }
      WriteJson("tables/selection.json", json => {
        json.Raw("{\"schemaVersion\":1,\"slot\":\"ResolveTargetingPackAssets.after\",\"actualSdkOpenObserved\":false,\"candidates\":[");
        for (int i = 0; i < records.Count; i++) { var row = records[i]; if (i != 0) json.Raw(",");
          json.Raw("{\"ownerOrdinal\":"); json.Number(row.Item1); json.Raw(",\"packPath\":"); json.String(row.Item2);
          json.Raw(",\"path\":"); json.String(row.Item3); json.Raw(",\"status\":"); json.String(row.Item4);
          json.Raw(",\"bytes\":"); if (row.Item5 == null) json.Raw("null"); else json.Number(row.Item5.Bytes);
          json.Raw(",\"sha256\":"); json.String(row.Item5?.Hash); json.Raw("}"); }
        json.Raw("]}\n");
      }, tick, 32768);
    }
    internal void RecheckTables(Action tick) {
      foreach (var row in tables) {
        tick(); using (var route = new Route(row.Item1)) {
          if (row.Item2 == null) { if (!route.Absent(tick)) throw new CaptureFault("table-absence-changed-or-unqualified"); }
          else if (!Files.Read(row.Item1, 1048576, tick).Same(row.Item2)) throw new CaptureFault("table-bytes-changed");
        }
      }
    }
    internal void Charge(long count) {
      if (count < 0 || checked(Bytes + count) > 67108864) throw new CaptureFault("observer-output-aggregate");
      Bytes += count;
    }
    internal FileIdentity Copy(string source, string destination, long maximum, Action tick, Action<int>? attempted = null) {
      using (var route = new Route(Root + "/" + destination, true)) using (var handle = route.CreateHandle())
      using (var stream = new FileStream(handle, FileAccess.Write, 65536, false)) {
        return Files.Read(source, maximum, tick, (buffer, count) => { Charge(count); stream.Write(buffer, 0, count); }, attempted);
      }
    }
    internal void Publish(string partial, string complete) {
      using (var from = new Route(Root + "/" + partial)) using (var to = new Route(Root + "/" + complete)) {
        if (Native.LinkAt(Native.Fd(from.Parent), from.Leaf, Native.Fd(to.Parent), to.Leaf, 0) != 0)
          throw new CaptureFault("exclusive-complete-publish");
        if (Native.UnlinkAt(Native.Fd(from.Parent), from.Leaf, 0) != 0) throw new CaptureFault("partial-unlink");
      }
    }
    internal void WriteJson(string path, Action<Json> emit, Action tick, long maximum) {
      long bytes = Json.Count(emit, tick); if (bytes > maximum) throw new CaptureFault("auxiliary-json-limit");
      using (var writer = Json.File(this, path + ".partial", bytes, tick)) { emit(writer); writer.Finish(); }
      Publish(path + ".partial", path);
    }
    internal void Fault(string slot, string type) {
      if (Faults >= 10) return;
      if (slot.Length > 128) slot = "invalid-slot"; if (type.Length > 128) type = "bounded-type";
      string path = "faults/" + (Faults++).ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + ".json";
      WriteJson(path, json => {
        json.Raw("{\"schemaVersion\":1,\"accepted\":false,\"slot\":"); json.String(slot);
        json.Raw(",\"faultType\":"); json.String(type); json.Raw(",\"sourceSeal\":"); json.String(Seal); json.Raw("}\n");
      }, () => { }, 32768);
    }
  }
  internal sealed class Json : IDisposable {
    private readonly Action tick;
    private readonly State? state;
    private readonly Route? route;
    private readonly FileStream? stream;
    private readonly byte[]? buffer;
    private readonly long expected;
    private long count;
    private int buffered;
    private Json(Action tick) { this.tick = tick; expected = Int64.MaxValue; }
    private Json(State state, string path, long expected, Action tick) {
      if (expected < 0 || checked(state.Bytes + expected) > 67108864) throw new CaptureFault("observer-output-reservation");
      this.state = state; this.expected = expected; this.tick = tick;
      route = new Route(state.Root + "/" + path, true);
      try { stream = new FileStream(route.CreateHandle(), FileAccess.Write, 65536, false); buffer = new byte[65536]; }
      catch { route.Dispose(); throw; }
    }
    internal static long Count(Action<Json> emit, Action tick) { using (var writer = new Json(tick)) { emit(writer); return writer.count; } }
    internal static Json File(State state, string path, long expected, Action tick) => new Json(state, path, expected, tick);
    private void Byte(byte value) {
      count = checked(count + 1); if (count > expected) throw new CaptureFault("encoded-size-changed");
      if ((count & 255) == 0) tick();
      if (buffer == null) return;
      buffer[buffered++] = value; if (buffered == buffer.Length) Flush();
    }
    private void Flush() { if (buffered == 0) return; tick(); state!.Charge(buffered); stream!.Write(buffer!, 0, buffered); buffered = 0; }
    internal void Raw(string ascii) { foreach (char c in ascii) { if (c > 127) throw new CaptureFault("literal-nonascii"); Byte((byte)c); } }
    internal void Number(long n) { if (n < 0) throw new CaptureFault("negative-wire-number"); Raw(n.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
    internal static long StringBytes(string? value, Action tick) {
      if (value == null) return 4;
      long count = 2;
      for (int i = 0; i < value.Length; i++) {
        if ((i & 255) == 0) tick(); char c = value[i];
        if (Char.IsHighSurrogate(c)) { if (++i >= value.Length || !Char.IsLowSurrogate(value[i])) throw new CaptureFault("unmatched-surrogate"); count += 12; }
        else if (Char.IsLowSurrogate(c)) throw new CaptureFault("unmatched-surrogate");
        else count += c == '"' || c == '\\' || c == '\b' || c == '\f' || c == '\n' || c == '\r' || c == '\t' ? 2 : c < 32 || c > 126 ? 6 : 1;
      }
      return count;
    }
    internal void String(string? value) {
      StringBytes(value, tick);
      if (value == null) { Raw("null"); return; }
      Byte(34); const string hex = "0123456789abcdef";
      foreach (char c in value) {
        if (c == '"' || c == '\\') { Byte(92); Byte((byte)c); }
        else if (c == '\b' || c == '\f' || c == '\n' || c == '\r' || c == '\t') {
          Byte(92); Byte((byte)(c == '\b' ? 'b' : c == '\f' ? 'f' : c == '\n' ? 'n' : c == '\r' ? 'r' : 't'));
        } else if (c < 32 || c > 126) { Raw("\\u"); Byte((byte)hex[(c >> 12) & 15]); Byte((byte)hex[(c >> 8) & 15]); Byte((byte)hex[(c >> 4) & 15]); Byte((byte)hex[c & 15]); }
        else Byte((byte)c);
      }
      Byte(34);
    }
    internal void Finish() { if (count != expected) throw new CaptureFault("encoded-size-mismatch"); Flush(); stream!.Flush(); }
    public void Dispose() { stream?.Dispose(); route?.Dispose(); }
  }
}
