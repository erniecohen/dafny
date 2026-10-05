using System.Text.Json;
using DafnyDriver.Commands;
using Microsoft.Dafny;
using static DafnyDriver.Test.ModernB3TestSupport;

namespace DafnyDriver.Test;

[Collection("Modern B3 CLI")]
public class ModernB3CommandBoundaryTest {
  [Fact]
  public async Task ActualBuildSkipProducesOrdinaryLibraryAndInformationalDisabledSidecar() {
    using var source = new SourceFile("module Example { function Value(): int { 7 } }");
    var output = new StringWriter();
    var errors = new StringWriter();
    var exit = await DafnyNewCli.Execute(new WritersConsole(TextReader.Null, output, errors), new[] {
      "build", source.Path, "--target", "lib", "--verification-backend", "b3", "--no-verify",
      "--b3-worker", Path.Combine(source.Directory, "missing-worker.dll"),
      "--solver-path", Path.Combine(source.Directory, "missing-solver")
    });
    Assert.Equal(0, exit);
    var libraryPath = Path.ChangeExtension(source.Path, ".doo");
    var library = await DooFile.Read(libraryPath);
    Assert.Equal(DooFile.ManifestData.CurrentDooFileVersion, library.Manifest.DooFileVersion);
    Assert.DoesNotContain("verification-backend", library.Manifest.Options.Keys);
    Assert.DoesNotContain("b3-worker", library.Manifest.Options.Keys);
    Assert.Null(library.Manifest.SolverVersion);
    using var information = JsonDocument.Parse(File.ReadAllText(libraryPath + ".b3-verification.json"));
    Assert.Equal("Disabled", information.RootElement.GetProperty("Compilation").GetProperty("Disposition").GetString());
    Assert.Contains("no proof was attempted", output.ToString());
    // This is an actual skipped CLI/library writer control, not a B3 worker/solver proof.
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task SourceProjectDependencyKeepsSelectedB3OrItsExplicitSkipPolicy(bool skip) {
    using var source = new SourceFile();
    var dependencyDirectory = Path.Combine(source.Directory, "dependency");
    Directory.CreateDirectory(dependencyDirectory);
    var dependency = Path.Combine(dependencyDirectory, "dependency.dfy");
    File.WriteAllText(dependency, "module Dependency { method Value() { assert true; } }");
    var project = Path.Combine(dependencyDirectory, "dfyconfig.toml");
    File.WriteAllText(project, "includes = [\"dependency.dfy\"]\n");
    _ = DafnyNewCli.RootCommand; // Register the real project extension handler.
    var output = new StringWriter();
    var options = Options(output);
    options.CliRootSourceUris.Add(new Uri(source.Path));
    options.Set(CommonOptionBag.Libraries, (IList<FileInfo>)new List<FileInfo> { new(project) });
    options.ApplyBinding(CommonOptionBag.Libraries);
    if (skip) { Skip(options); }
    else {
      options.Set(BoogieOptionBag.ArithmeticSolver, 1);
      var nonExecutable = Path.Combine(source.Directory, "identity-only-not-a-solver");
      File.WriteAllText(nonExecutable, "identity-only input; no process may execute this file");
      options.ProverOptions.Add("PROVER_PATH=" + nonExecutable);
    }
    var (exit, inputs) = await PreparedCliInputs.PrepareAsync(options, CancellationToken.None);
    if (skip) {
      Assert.Equal(ExitValue.SUCCESS, exit);
      Assert.NotNull(inputs);
      var loaded = Assert.Single(inputs!.RootFiles.Where(file => file.Uri.LocalPath == dependency));
      Assert.True(loaded.ShouldNotVerify);
      Assert.True(loaded.ShouldNotCompile);
    } else {
      Assert.NotEqual(ExitValue.SUCCESS, exit);
      Assert.Null(inputs);
      Assert.Contains("B3 currently supports --arithmetic-solver 2 only", output.ToString());
      Assert.Contains("Failed to build dependency", output.ToString());
      Assert.DoesNotContain("build, run, test, and translate are unsupported", output.ToString());
    }
    Assert.False(File.Exists(Path.ChangeExtension(dependency, ".doo")));
  }

  [Theory]
  [InlineData("b3", null, true)]
  [InlineData("boogie", "b3", true)]
  [InlineData("b3", "boogie", false)]
  public async Task ActualProjectAndCliPrecedenceChooseOneBackend(string projectBackend,
    string? cliBackend, bool expectB3Rejection) {
    using var source = new SourceFile();
    var project = Path.Combine(source.Directory, "dfyconfig.toml");
    File.WriteAllText(project, "includes = [\"program.dfy\"]\n[options]\nverification-backend = \"" + projectBackend + "\"\n");
    var output = new StringWriter();
    var errors = new StringWriter();
    var args = new List<string> { "build", project, "--target", "lib", "--hidden-no-verify" };
    if (cliBackend != null) { args.AddRange(new[] { "--verification-backend", cliBackend }); }
    var exit = await DafnyNewCli.Execute(new WritersConsole(TextReader.Null, output, errors), args);
    if (expectB3Rejection) {
      Assert.Equal((int)ExitValue.PREPROCESSING_ERROR, exit);
      Assert.Contains("B3 does not support --hidden-no-verify", output.ToString());
      Assert.False(File.Exists(Path.ChangeExtension(source.Path, ".doo")));
    } else {
      Assert.Equal(0, exit);
      Assert.True(File.Exists(Path.ChangeExtension(source.Path, ".doo")));
      Assert.False(File.Exists(Path.ChangeExtension(source.Path, ".doo") + ".b3-verification.json"));
    }
  }
}
