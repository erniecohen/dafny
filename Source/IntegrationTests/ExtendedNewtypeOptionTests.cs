// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Dafny;
using Xunit;
using XUnitExtensions.Lit;

namespace IntegrationTests;

public class ExtendedNewtypeOptionTests {
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task EnabledLibraryRejectsDefaultOrExplicitDisabledClient(bool explicitDisabled) {
    var directory = Path.Combine(Path.GetTempPath(), "issue113-option-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    try {
      var librarySource = Path.Combine(directory, "library.dfy");
      var clientSource = Path.Combine(directory, "client.dfy");
      var library = Path.Combine(directory, "library.doo");
      await File.WriteAllTextAsync(librarySource, "module Lib { newtype Ord = ORDINAL }\n");
      await File.WriteAllTextAsync(clientSource,
        "import L = Lib lemma Use(o: L.Ord) { var raw := o as ORDINAL; }\n");
      var build = await Run("build", "--no-verify", "--target=lib", "--standard-libraries=false",
        "--type-system-refresh=true", "--general-newtypes=true", "--extended-newtype-bases=true",
        "--output", library, librarySource);
      Assert.True(build.Exit == 0, build.Output);
      Assert.True(File.Exists(library), build.Output);

      var arguments = new List<string> {
        "resolve", "--standard-libraries=false", "--type-system-refresh=true", "--general-newtypes=true",
        "--library", library, clientSource
      };
      if (explicitDisabled) {
        arguments.Add("--extended-newtype-bases=false");
      }
      var rejected = await Run(arguments.ToArray());
      Assert.NotEqual(0, rejected.Exit);
      Assert.Contains("--extended-newtype-bases is set locally to False, but the library was built with True", rejected.Output);

      arguments.Add("--extended-newtype-bases=true");
      if (explicitDisabled) {
        arguments.Remove("--extended-newtype-bases=false");
      }
      var accepted = await Run(arguments.ToArray());
      Assert.True(accepted.Exit == 0, accepted.Output);
    } finally {
      Directory.Delete(directory, true);
    }
  }

  private static async Task<(int Exit, string Output)> Run(params string[] arguments) {
    var commandArguments = new List<string> { typeof(Dafny.Dafny).Assembly.Location };
    commandArguments.AddRange(arguments);
    var command = new ShellLitCommand("dotnet", commandArguments,
      DafnyCliTests.ReferencedEnvironmentVariables);
    using var stdout = new StringWriter();
    using var stderr = new StringWriter();
    var exit = await command.Execute(TextReader.Null, stdout, stderr);
    return (exit, stdout.ToString() + stderr.ToString());
  }
}
