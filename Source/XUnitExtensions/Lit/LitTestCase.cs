using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace XUnitExtensions.Lit {
  public class LitTestCase {
    private static readonly TimeSpan IndividualTestTimeout = TimeSpan.FromMinutes(15);
    public string FilePath { get; }
    public IEnumerable<ILitCommand> Commands { get; }
    public bool ExpectFailure { get; }

    private static LitTestCase Parse(string filePath, LitTestConfiguration config) {
      ILitCommand[] commands = File.ReadAllLines(filePath)
        .Select(line => ILitCommand.Parse(line, config))
        .Where(c => c != null)
        .Select(e => e!)
        .ToArray();
      if (commands.Length == 0) {
        throw new ArgumentException($"No lit commands found in test file: {filePath}");
      }

      var xfail = commands.Any(c => c is XFailCommand);
      foreach (var unsupported in commands.OfType<UnsupportedCommand>()) {
        foreach (var feature in config.Features) {
          if (unsupported.Features.Contains(feature)) {
            throw new SkipException($"Test case not supported: {feature}");
          }
        }
      }

      return new LitTestCase(filePath, commands, xfail);
    }

    public static LitTestCase Read(string filePath, LitTestConfiguration config) {
      string fileName = Path.GetFileName(filePath);
      string? directory = Path.GetDirectoryName(filePath);
      if (directory == null) {
        throw new ArgumentException($"Couldn't get directory name for path: {filePath}");
      }

      string fullDirectoryPath = Path.GetFullPath(directory).Replace(@"\", "/");
      config = config.WithSubstitutions(new Dictionary<string, object> {
        {"%s", filePath.Replace(@"\", "/")},
        // For class path separators
        {"%{pathsep}", Path.PathSeparator.ToString()},
        {"%S", fullDirectoryPath},
        {"%t", Path.Join(fullDirectoryPath, "Output", $"{fileName}.tmp")}
      });

      return Parse(filePath, config);
    }

    public static readonly TimeSpan MaxTestCaseRuntime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How long a timed-out test case's commands get to stop, after they are cancelled,
    /// before <see cref="Run(string, LitTestConfiguration, ITestOutputHelper, TimeSpan)"/> reports the timeout anyway.
    /// </summary>
    private static readonly TimeSpan CancellationGracePeriod = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The cancellation of the test case whose commands are running in this asynchronous flow.
    /// <see cref="ShellLitCommand"/> kills its process tree when it is cancelled.
    /// </summary>
    internal static readonly AsyncLocal<CancellationToken> CurrentCancellation = new();

    public static void Run(string filePath, LitTestConfiguration config, ITestOutputHelper outputHelper) {
      Run(filePath, config, outputHelper, IndividualTestTimeout);
    }

    /// <summary>
    /// Runs the test case's commands, and fails if they fail or do not finish within <paramref name="timeout"/>.
    /// A test case that times out is never reported as passed: its commands are cancelled, which kills
    /// the process trees of its shell commands, and a <see cref="TimeoutException"/> is thrown.
    /// Commands that run in this process (DAFNY_INTEGRATION_TESTS_IN_PROCESS) cannot be stopped;
    /// the test case still fails, and the exception says that they are still running.
    /// </summary>
    public static void Run(string filePath, LitTestConfiguration config, ITestOutputHelper outputHelper, TimeSpan timeout) {
      var litTestCase = Read(filePath, config);
      using var cancellation = new CancellationTokenSource();
      var task = Task.Run(() => {
        CurrentCancellation.Value = cancellation.Token;
        return litTestCase.Execute(outputHelper);
      });
      if (task.Wait(timeout)) {
        return;
      }

      cancellation.Cancel();
      var stopped = Task.WhenAny(task, Task.Delay(CancellationGracePeriod)).Result == task;
      _ = task.Exception; // Observed: the timeout is the failure reported.
      throw new TimeoutException(
        $"Test case did not finish within {timeout}: {filePath}\n" +
        (stopped
          ? "Its commands were cancelled and have stopped."
          : $"Its commands were cancelled but had not stopped {CancellationGracePeriod} later."));
    }

    public LitTestCase(string filePath, IEnumerable<ILitCommand> commands, bool expectFailure) {
      this.FilePath = filePath;
      this.Commands = commands;
      this.ExpectFailure = expectFailure;
    }

    public async Task Execute(ITestOutputHelper outputHelper) {
      Directory.CreateDirectory(Path.Join(Path.GetDirectoryName(FilePath), "Output"));
      // For debugging. Only printed on failure in case the true cause is buried in an earlier command.
      List<(string, string)> results = [];


      foreach (var command in Commands) {
        CurrentCancellation.Value.ThrowIfCancellationRequested();
        int exitCode;
        var outputWriter = new StringWriter();
        var errorWriter = new StringWriter();
        try {
          outputHelper.WriteLine($"Executing command: {command}");
          exitCode = await command.Execute(TextReader.Null, outputWriter, errorWriter);
        } catch (Exception e) {
          throw new Exception($"Exception thrown while executing command: {command}", e);
        }

        if (ExpectFailure) {
          if (exitCode != 0) {
            results.Add((outputWriter.ToString(), errorWriter.ToString()));
            return;
          }
        }

        if (exitCode != 0) {
          outputHelper?.WriteLine("Previous command results:");
          foreach (var (prevOutput, prevError) in results) {
            outputHelper?.WriteLine($"Output:\n{prevOutput}");
            outputHelper?.WriteLine($"Error:\n{prevError}");
          }

          throw new Exception(
            $"Command returned non-zero exit code ({exitCode}): {command}\nOutput:\n{outputWriter}\nError:\n{errorWriter}");
        }

        results.Add((outputWriter.ToString(), errorWriter.ToString()));
      }

      if (ExpectFailure) {
        throw new Exception($"Test case passed but expected to fail: {FilePath}");
      }
    }
  }
}