// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Dafny;
using Xunit;
using XUnitExtensions.Lit;

namespace IntegrationTests;

public class CardinalityEmissionTests {
  [Theory]
  [InlineData("lib")]
  [InlineData("cs")]
  public async Task RejectedNoVerifyBuildProducesNoArtifact(string target) {
    var directory = Path.Combine(Path.GetTempPath(), "cardinality-emission-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    try {
      var source = Path.Combine(directory, "input.dfy");
      await File.WriteAllTextAsync(source,
        "trait V {} datatype D extends V = D(p: V -> bool) method Main() {}\n");
      var outputs = Path.Combine(directory, "output");
      var output = Path.Combine(outputs, target == "lib" ? "invalid.doo" : "invalid");
      var command = new ShellLitCommand("dotnet", new[] {
        typeof(Dafny.Dafny).Assembly.Location, "build", "--no-verify", "--target=" + target,
        "--type-system-refresh", "--general-traits=datatype", "--show-snippets=false",
        "--standard-libraries=false", "--output", output, source
      }, DafnyCliTests.ReferencedEnvironmentVariables);
      using var stdout = new StringWriter();
      using var stderr = new StringWriter();
      var exit = await command.Execute(TextReader.Null, stdout, stderr);
      Assert.Equal(2, exit);
      Assert.Contains("potentially cardinality-expanding cycle", stdout.ToString());
      Assert.False(File.Exists(output));
      if (Directory.Exists(outputs)) {
        Assert.Empty(Directory.EnumerateFiles(outputs, "*", SearchOption.AllDirectories));
      }
    } finally {
      Directory.Delete(directory, true);
    }
  }
}
