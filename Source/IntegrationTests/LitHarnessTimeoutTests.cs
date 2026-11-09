using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Xunit;
using Xunit.Abstractions;
using XUnitExtensions.Lit;

namespace IntegrationTests;

/// <summary>
/// Issue 176: a lit test case whose commands do not finish within the harness's timeout
/// must fail, and its commands, with the processes they started, must be stopped.
/// Before the fix, <see cref="LitTestCase.Run(string, LitTestConfiguration, ITestOutputHelper)"/>
/// ignored the result of its wait, so such a test case was reported as passed.
/// </summary>
public class LitHarnessTimeoutTests {
  private static readonly string Inputs =
    Path.Join("TestFiles", "LitTests", "LitTest", "git-issues", "Inputs");

  private static readonly LitTestConfiguration Config = new(
    new Dictionary<string, object>(),
    new Dictionary<string, Func<IEnumerable<string>, LitTestConfiguration, ILitCommand>>(),
    [],
    ["PATH"]);

  private readonly ITestOutputHelper output;

  public LitHarnessTimeoutTests(ITestOutputHelper output) {
    this.output = output;
  }

  [SkippableFact]
  public void UnfinishedCommandsFailTheTestAndAreStopped() {
    Skip.If(OperatingSystem.IsWindows(), "The helper test case uses a POSIX shell.");
    var path = Path.Join(Inputs, "git-issue-176-hangs.lit");
    var temp = Path.Join(Path.GetFullPath(Inputs), "Output", "git-issue-176-hangs.lit.tmp");
    foreach (var stale in new[] { temp + ".shell", temp + ".child" }) {
      if (File.Exists(stale)) {
        File.Delete(stale);
      }
    }

    var watch = Stopwatch.StartNew();
    var exception = Assert.Throws<TimeoutException>(() =>
      LitTestCase.Run(path, Config, output, TimeSpan.FromSeconds(5)));
    watch.Stop();
    output.WriteLine(exception.Message);
    output.WriteLine($"Run returned after {watch.Elapsed.TotalSeconds:F1} s");

    // The timeout, not the second command, failed the test; and the commands stopped,
    // well before the 600 s that the first one waits.
    Assert.Contains("did not finish within", exception.Message);
    Assert.Contains("have stopped", exception.Message);
    Assert.True(watch.Elapsed < TimeSpan.FromSeconds(60), $"Run took {watch.Elapsed}");

    // The shell and the child it started are both gone.
    foreach (var pidFile in new[] { temp + ".shell", temp + ".child" }) {
      var pid = int.Parse(File.ReadAllText(pidFile).Trim());
      Assert.True(Stops(pid), $"process {pid}, from {Path.GetFileName(pidFile)}, is still running");
    }
  }

  [SkippableFact]
  public void FinishedCommandsAreJudgedByTheirExitCodes() {
    Skip.If(OperatingSystem.IsWindows(), "The helper test cases use a POSIX shell.");
    LitTestCase.Run(Path.Join(Inputs, "git-issue-176-finishes.lit"), Config, output, TimeSpan.FromSeconds(60));

    var exception = Assert.ThrowsAny<Exception>(() =>
      LitTestCase.Run(Path.Join(Inputs, "git-issue-176-fails.lit"), Config, output, TimeSpan.FromSeconds(60)));
    Assert.IsNotType<TimeoutException>(exception);
    Assert.Contains("non-zero exit code (3)", exception.ToString());
  }

  /// <summary>
  /// Whether process <paramref name="pid"/> has gone, or is a zombie, within ten seconds.
  /// </summary>
  private static bool Stops(int pid) {
    for (var i = 0; i < 100; i++) {
      if (!Running(pid)) {
        return true;
      }
      Thread.Sleep(100);
    }
    return false;
  }

  private static bool Running(int pid) {
    if (Directory.Exists("/proc/self")) {
      // Linux: a reaped process has no entry, and a zombie's state, after its parenthesized name, is Z.
      try {
        var text = File.ReadAllText($"/proc/{pid}/stat");
        return text[(text.LastIndexOf(')') + 2)..].Split(' ')[0] is not ("Z" or "X");
      } catch (IOException) {
        return false;
      }
    }
    try {
      using var process = Process.GetProcessById(pid);
      return !process.HasExited;
    } catch (Exception e) when (e is ArgumentException or InvalidOperationException) {
      return false;
    }
  }
}
