using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using XUnitExtensions.Lit;

namespace IntegrationTests;

/// <summary>
/// Runs resolution in a fresh process: resolver nontermination is not bounded by a solver timeout.
/// A rejected program must exit normally with code 2 and the requested diagnostic.
/// </summary>
class BoundedResolveCommand : ILitCommand {
  private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
  private static readonly Regex ExceptionOutput = new(
    @"\b[\w.]*Exception\b|Stack overflow|Dafny encountered an internal|Internal (?:\w+\s+)?(?:error|exception)|^\s+at [\w.]+[.(]",
    RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
  private static readonly Regex RedirectingCycle = new(
    @"Error: cycle among redirecting types \(newtypes, subset types, type synonyms\): [^\r\n]+ -> ",
    RegexOptions.CultureInvariant);
  private static readonly Regex RefreshedCycle = new(
    @"Error: Cyclic dependency among declarations: [^\r\n]+ -> ",
    RegexOptions.CultureInvariant);
  private static readonly Regex Underconstrained = new(
    @"Error: base type of (?:newtype|subset type) '[^']+' is not fully determined",
    RegexOptions.CultureInvariant);

  private readonly string expectation;
  private readonly bool refreshed;
  private readonly string file;
  private readonly string[] extraArguments;
  private readonly string[] environmentVariables;
  private readonly string driverAssembly;

  public BoundedResolveCommand(IEnumerable<string> arguments,
    IEnumerable<string> environmentVariables, string driverAssembly) {
    var args = arguments.ToArray();
    if (args.Length < 3 || args[0] is not ("cycle" or "explicit-cycle" or "cycle-and-error" or "valid" or "underconstrained")) {
      throw new ArgumentException("%bounded-resolve expects OUTCOME REFRESHED FILE [OPTIONS]");
    }
    expectation = args[0];
    refreshed = bool.Parse(args[1]);
    file = args[2];
    extraArguments = args[3..];
    this.environmentVariables = environmentVariables.ToArray();
    this.driverAssembly = driverAssembly;
  }

  public async Task<int> Execute(TextReader inputReader, TextWriter outputWriter, TextWriter errorWriter) {
    var release = Environment.GetEnvironmentVariable("DAFNY_RELEASE");
    using var process = new Process {
      StartInfo = new ProcessStartInfo {
        FileName = release == null ? "dotnet" : Path.Join(release, "dafny"),
        UseShellExecute = false,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true
      }
    };
    if (release == null) {
      process.StartInfo.ArgumentList.Add(driverAssembly);
    }
    foreach (var arg in new[] {
               "resolve", $"--type-system-refresh:{refreshed.ToString().ToLowerInvariant()}",
               "--use-basename-for-filename", "--show-snippets:false", "--standard-libraries:false"
             }.Concat(extraArguments.Any(argument => argument.StartsWith("--general-newtypes"))
               ? Array.Empty<string>() : new[] { "--general-newtypes:false" }).Concat(extraArguments).Append(file)) {
      process.StartInfo.ArgumentList.Add(arg);
    }
    process.StartInfo.Environment.Clear();
    foreach (var variable in environmentVariables) {
      var value = Environment.GetEnvironmentVariable(variable);
      if (value != null) {
        process.StartInfo.Environment[variable] = value;
      }
    }

    process.Start();
    process.StandardInput.Close();
    // Drain both streams concurrently; an exception trace may otherwise fill stderr and deadlock.
    var stdoutTask = process.StandardOutput.ReadToEndAsync();
    var stderrTask = process.StandardError.ReadToEndAsync();
    using var timeout = new CancellationTokenSource(Timeout);
    var timedOut = false;
    try {
      await process.WaitForExitAsync(timeout.Token);
    } catch (OperationCanceledException) {
      timedOut = true;
      // The resolver and any descendants must not survive a failed test.
      if (!process.HasExited) {
        process.Kill(entireProcessTree: true);
      }
      await process.WaitForExitAsync();
    }
    var stdout = await stdoutTask;
    var stderr = await stderrTask;
    var combined = stdout + stderr;
    var expectedCode = expectation == "valid" ? 0 : 2;
    var hasCycle = RedirectingCycle.IsMatch(combined) || refreshed && RefreshedCycle.IsMatch(combined);
    var correctDiagnostic = expectation switch {
      "cycle" => hasCycle,
      "explicit-cycle" => RedirectingCycle.IsMatch(combined),
      "cycle-and-error" => hasCycle && combined.Contains("unresolved identifier: missing"),
      "underconstrained" => Underconstrained.IsMatch(combined) && !hasCycle,
      "valid" => !combined.Contains("Error:") && !combined.Contains("Warning:"),
      _ => false
    };
    if (timedOut || process.ExitCode != expectedCode || ExceptionOutput.IsMatch(combined) || !correctDiagnostic) {
      await errorWriter.WriteLineAsync($"{this}: expected normal exit {expectedCode} with {expectation}; " +
                                       $"actual exit {process.ExitCode}, timed out: {timedOut}");
      await outputWriter.WriteAsync(stdout);
      await errorWriter.WriteAsync(stderr);
      return 1;
    }

    // The precise source position and chosen witness may differ between resolver modes.
    // Check the semantic outcome above and keep the transcript stable.
    await outputWriter.WriteLineAsync($"{(refreshed ? "refreshed" : "legacy")} {Path.GetFileName(file)}: {expectation} (exit {process.ExitCode})");
    return 0;
  }

  public override string ToString() => $"%bounded-resolve {expectation} {refreshed} {file} {string.Join(" ", extraArguments)}";
}
