#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Boogie;
using Microsoft.Boogie.SMTLib;
using BplType = Microsoft.Boogie.Type;

namespace Microsoft.Dafny.Caching;

/// <summary>
/// Frozen adaptive-program parameters, ordered UsedTypes inputs and toolchain identity.
/// The initial SMT stream is captured separately by the transport after lowering.
/// </summary>
internal sealed class TaskExecutionEnvironment(DafnyOptions options) {
  private static readonly HashSet<string> ReportBindings = new(StringComparer.Ordinal) {
    "OutputWriter", "XmlSink"
  };
  private static readonly HashSet<string> VerifiedTransport = new(StringComparer.Ordinal) {
    "CreateSolver", "TheProverFactory"
  };

  public byte[]? Freeze(SMTLibOptions library, SMTLibSolverOptions solver, out string? reason) {
    try {
      if (library.TheProverFactory.GetType() != typeof(Factory) || solver.GetType() != typeof(SMTLibSolverOptions) ||
          solver.Solver != SolverKind.Z3 || solver.SolverArguments.Count != 1 || solver.SolverArguments[0] != "-smt2 -in" || solver.BatchMode || solver.LogFilename != null || solver.Inspector != null ||
          library.ProverPreamble != null || library.RunDiagnosticsOnTimeout || library.TraceDiagnosticsOnTimeout ||
          library.Trace || library.TraceVerify || library.TraceTimes || options.AdditionalPlugins.Count != 0 ||
          options.AdditionalPluginArguments.Count != 0 || options.ProcessTimeLimit != 0 || library.ModelViewFile != null || library.SoundnessSmokeTest ||
          library.VcsMaxKeepGoingSplits > 1 || library.ImmediatelyAcceptCommands || !library.RestartProverPerVC) {
        reason = "unsupported factory, adaptive diagnostics or external side effects";
        return null;
      }
      var identity = ToolchainIdentity(solver);
      var manifest = new SortedDictionary<string, object?>(StringComparer.Ordinal) {
        ["abi"] = "dafny-task-transport-v1",
        ["toolchain"] = JsonSerializer.Deserialize<SortedDictionary<string, string>>(identity)
      };
      // Enumerate the complete interfaces used by the pinned Boogie prover, including inherited
      // members. No newly added or unsupported runtime value is silently omitted.
      var interfaces = new[] { typeof(SMTLibOptions) }.Concat(typeof(SMTLibOptions).GetInterfaces());
      foreach (var property in interfaces.SelectMany(t => t.GetProperties()).OrderBy(p => p.DeclaringType!.FullName).ThenBy(p => p.Name)) {
        var name = property.DeclaringType!.FullName + "." + property.Name;
        if (ReportBindings.Contains(property.Name)) { manifest[name] = "current-report-binding"; continue; }
        if (VerifiedTransport.Contains(property.Name)) { manifest[name] = "verified-transport"; continue; }
        manifest[name] = Encode(property.GetValue(library));
      }
      foreach (var field in solver.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public).OrderBy(f => f.Name)) {
        if (field.Name == "UsedTypes") { continue; }
        manifest["solver." + field.Name] = Encode(field.GetValue(solver));
      }
      foreach (var property in solver.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).OrderBy(p => p.Name)) {
        if (property.Name is "LibOptions" or "Help") { continue; }
        manifest["solver." + property.Name] = Encode(property.GetValue(solver));
      }
      // UsedTypes inspects all nonimplementation declarations plus this implementation. Preserve
      // the resulting order explicitly, even when a type does not occur in the pruned VC text.
      manifest["solver.OrderedUsedTypes"] = EncodeUsedTypes(solver.UsedTypes, options);
      reason = null;
      return JsonSerializer.SerializeToUtf8Bytes(manifest);
    } catch (Exception e) when (e is NotSupportedException or IOException or UnauthorizedAccessException or
                               ArgumentException or TargetInvocationException or InvalidOperationException) {
      reason = e.Message;
      return null;
    }
  }

  internal static string?[][] EncodeUsedTypes(IEnumerable<BplType> usedTypes, PrintOptions options) =>
    usedTypes.Select(type => {
      if (type.GetType().Assembly != typeof(BplType).Assembly) { throw new NotSupportedException("external Boogie type"); }
      using var text = new StringWriter(CultureInfo.InvariantCulture);
      using (var writer = new TokenTextWriter("cache-type", text, false, false, options)) { type.Emit(writer); }
      return new[] { type.GetType().FullName, text.ToString() };
    }).ToArray();

  private static object? Encode(object? value) {
    if (value == null) { return null; }
    var type = value.GetType();
    if (type.IsEnum) { return new object[] { type.FullName!, Convert.ToInt64(value, CultureInfo.InvariantCulture) }; }
    if (value is string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or char) { return value; }
    if (value is double d) { return new[] { "double-bits", BitConverter.DoubleToInt64Bits(d).ToString("x16", CultureInfo.InvariantCulture) }; }
    if (value is float f) { return new[] { "float-bits", BitConverter.SingleToInt32Bits(f).ToString("x8", CultureInfo.InvariantCulture) }; }
    if (value is OptionValue option) { return new[] { option.Option, option.Value }; }
    if (value is CoreOptions.AiFlags flags) {
      return new object[] { flags.J_Trivial, flags.J_Intervals, flags.StepsBeforeWidening, flags.DebugStatistics };
    }
    if (value is IEnumerable sequence) { return sequence.Cast<object?>().Select(Encode).ToArray(); }
    throw new NotSupportedException("unclassified execution option type " + type.FullName);
  }

  private byte[] ToolchainIdentity(SMTLibSolverOptions solver) {
    using (var executable = File.OpenRead(solver.ExecutablePath())) {
      Span<byte> magic = stackalloc byte[4];
      if (executable.Read(magic) != 4 ||
          !(magic.SequenceEqual(new byte[] { 0xCF, 0xFA, 0xED, 0xFE }) ||
            magic.SequenceEqual(new byte[] { 0xCA, 0xFE, 0xBA, 0xBE }) ||
            magic.SequenceEqual(new byte[] { 0x7f, 0x45, 0x4c, 0x46 }) || magic[..2].SequenceEqual(new byte[] { 0x4d, 0x5a }))) {
        throw new NotSupportedException("solver launch scripts require a dependency manifest");
      }
    }
    var manifest = ExecutionImageIdentity.Read(solver.ExecutablePath());
    manifest["solver-version"] = options.SolverVersion?.ToString() ?? throw new NotSupportedException("unvalidated solver version");
    manifest["os"] = RuntimeInformation.OSDescription;
    manifest["architecture"] = RuntimeInformation.ProcessArchitecture.ToString();
    foreach (DictionaryEntry entry in System.Environment.GetEnvironmentVariables()) {
      // Solver environment is part of identity; retain only digests, never secret values.
      manifest["env:" + (string)entry.Key] = CacheDigest.Blob(Encoding.UTF8.GetBytes((string)entry.Value!));
    }
    return JsonSerializer.SerializeToUtf8Bytes(manifest);
  }
}
