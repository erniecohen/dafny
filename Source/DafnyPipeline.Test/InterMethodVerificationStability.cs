using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Xunit;
using Xunit.Abstractions;

namespace DafnyPipeline.Test {
  // Main.Resolve has static shared state (TypeConstraint.ErrorsToBeReported for example)
  // so we can't execute tests that use it in parallel.
  [Collection("Singleton Test Collection - Resolution")]
  public class InterMethodVerificationStability {
    private readonly ITestOutputHelper output;

    [Fact]
    public void CreatingBoogieVariableNameCollisionsHasExpectedDiff() {
      var beforeChange = @"
method M(heap: object) 
modifies heap
{
  var x := 0;
  if y :| y < x {
    var x := 0; 
  } else {
    var y := 0;
  }

  while(x < 5) {
    var x := 2;
    var y := 0;
  }

  for i: int := 0 to 3 {
    var x := 2;
    var y := 0;
  }

  forall i | 0 <= i < x {
    var x := 2;
    var y := 0;
  }

  var s := {2,3};
  modify heap {
    var z := 3; // modify does not push a Dafny scope, so we can't assign to a fresh x here.
  }

  assert x == 0 by {
    var x := x + 2;
    var y := 0;
  }
  var y := 200;
}";

      var afterChange = @"
method M(heap: object) 
  modifies heap
{
  var x := 0;
  if k :| k < x {
    var a := 0; 
  } else {
    var b := 0;
  }
  
  while(x < 5) {
    var c := 2;
    var d := 0;
  }
  
  for i: int := 0 to 3 {
    var e := 2;
    var f := 0;
  }
  
  forall i | 0 <= i < x {
    var g := 2;
    var h := 0;
  }
  
  var s := {2,3};
  modify heap {
    var z := 3; // modify does not push a Dafny scope, so we can't assign to a fresh x here.
  }
  
  assert x == 0 by {
    var i := x + 2;
    var j := 0;
  }
  var y := 200;
}";
      var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".dfy");
      var beforeBoogie = GetBoogie(beforeChange, tempPath);
      var afterBoogie = GetBoogie(afterChange, tempPath);
      var diff = GetDiff(beforeBoogie, afterBoogie);

      var expectedDiff = @"-   var eg$y#0: int;
-   var y#0_0: int;
-   var x#1_0: int;
-   var y#2_0: int;
+   var eg$k#0: int;
+   var k#0_0: int;
+   var a#1_0: int;
+   var b#2_0: int;
-   var x#3_0: int;
-   var y#3_0: int;
+   var c#3_0: int;
+   var d#3_0: int;
-   var x#4_0: int;
-   var y#4_0: int;
+   var e#4_0: int;
+   var f#4_0: int;
-   var x#5_0: int;
-   var y#5_0: int;
+   var g#5_0: int;
+   var h#5_0: int;
-   var x#7_0: int;
-   var y#7_0: int;
+   var i#7_0: int;
+   var j#7_0: int;
-     havoc eg$y#0;
+     havoc eg$k#0;
-     havoc y#0_0;
+     havoc k#0_0;
-         assume y#0_0 < x#0;
+         assume k#0_0 < x#0;
-         x#1_0 := {:id ""id1""} LitInt(0);
+         a#1_0 := {:id ""id1""} LitInt(0);
-         assume {:id ""id2""} !(exists eg$y#1: int :: eg$y#1 < x#0);
+         assume {:id ""id2""} !(exists eg$k#1: int :: eg$k#1 < x#0);
-         y#2_0 := {:id ""id3""} LitInt(0);
+         b#2_0 := {:id ""id3""} LitInt(0);
-         x#3_0 := {:id ""id4""} LitInt(2);
+         c#3_0 := {:id ""id4""} LitInt(2);
-         y#3_0 := {:id ""id5""} LitInt(0);
+         d#3_0 := {:id ""id5""} LitInt(0);
-         x#4_0 := {:id ""id9""} LitInt(2);
+         e#4_0 := {:id ""id9""} LitInt(2);
-         y#4_0 := {:id ""id10""} LitInt(0);
+         f#4_0 := {:id ""id10""} LitInt(0);
-         x#5_0 := {:id ""id12""} LitInt(2);
+         g#5_0 := {:id ""id12""} LitInt(2);
-         y#5_0 := {:id ""id13""} LitInt(0);
+         h#5_0 := {:id ""id13""} LitInt(0);
-         x#7_0 := {:id ""id17""} x#0 + 2;
+         i#7_0 := {:id ""id17""} x#0 + 2;
-         y#7_0 := {:id ""id18""} LitInt(0);
+         j#7_0 := {:id ""id18""} LitInt(0);
";
      Assert.Equal(expectedDiff.Replace("\r\n", "\n"), diff.Replace("\r\n", "\n"));
    }

    string GetDiff(string before, string after) {
      var diff = InlineDiffBuilder.Diff(before, after);
      var result = new StringBuilder();
      foreach (var line in diff.Lines) {
        switch (line.Type) {
          case ChangeType.Inserted:
            result.Append("+ ");
            result.AppendLine(line.Text);
            break;
          case ChangeType.Deleted:
            result.Append("- ");
            result.AppendLine(line.Text);
            break;
        }
      }

      return result.ToString();
    }

    public InterMethodVerificationStability(ITestOutputHelper output) {
      this.output = output;
    }

    string GetBoogie(string dafnyProgram, string fileName) {
      File.WriteAllText(fileName, dafnyProgram);
      try {
        var processStartInfo = new ProcessStartInfo {
          FileName = "dotnet",
          Arguments = $"\"{typeof(Microsoft.Dafny.DafnyNewCli).Assembly.Location}\" -useBaseNameForFileName -compileVerbose:0 /errorTrace:0 /compile:0 /env:0 /print:- \"{fileName}\"",
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          UseShellExecute = false
        };
        using var dafnyProcess = Process.Start(processStartInfo);
        var result = dafnyProcess.StandardOutput.ReadToEnd();
        dafnyProcess.WaitForExit();

        if (dafnyProcess.ExitCode != 4) {
          output.WriteLine("Arguments: {0}", processStartInfo.Arguments);
          output.WriteLine("Result:");
          output.WriteLine(result);
          output.WriteLine(dafnyProcess.StandardError.ReadToEnd());
        }
        Assert.Equal(4, dafnyProcess.ExitCode);
        return result;
      } finally {
        File.Delete(fileName);
      }
    }
  }
}
