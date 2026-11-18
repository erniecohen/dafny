using Microsoft.Dafny;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tomlyn;

namespace DafnyCore.Test;

public class DooFileTest {
  [Fact]
  public async Task RoundTripCurrentVersion() {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    var program = await ParseProgram("module MyModule { function TheAnswer(): int { 42 } }", options);
    options.ProcessSolverOptions(program.Reporter, Token.Cli);
    var dooFile = new DooFile(program);

    var path = Path.GetTempFileName();
    dooFile.Write(path);
    var loadedDooFile = await DooFile.Read(path);

    Assert.Equal(loadedDooFile.Manifest.DooFileVersion, DooFile.ManifestData.CurrentDooFileVersion);
    Assert.Equal(loadedDooFile.Manifest.DafnyVersion, options.VersionNumber);
    Assert.Equal(loadedDooFile.Manifest.SolverIdentifier, options.SolverIdentifier);
    Assert.Equal(loadedDooFile.Manifest.SolverVersion, options.SolverVersion.ToString());
  }

  [Fact]
  public async Task RecordedWildcardSerializationSurvivesMergedInputs() {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.PrintMode = PrintModes.Serialization;
    var first = await ParseProgram("module A { function F(): (int -> int) { _ => 0 } }", options);
    var second = await ParseProgram("module B { function G(): int { match 0 case _ => 1 } }", options);
    first.DefaultModuleDef.SourceDecls.AddRange(second.DefaultModuleDef.SourceDecls);
    var legacy = new DooFile(first).ProgramText;
    Assert.Contains("/* _v0 */", legacy);
    var recorded = new DooFile(first, canonicalWildcards: true).ProgramText;
    Assert.DoesNotContain("/* _v", recorded);
    var reparsed = await ParseProgram(recorded, options);
    Assert.Equal(recorded, new DooFile(reparsed, canonicalWildcards: true).ProgramText);
    Assert.Equal(legacy, new DooFile(first).ProgramText);
  }

  [Fact]
  public void OrdinaryManifestDoesNotInventVerificationEvidence() {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    Assert.DoesNotContain(CommonOptionBag.LegacyNoModularTermination.Name,
      new DooFile.ManifestData(options).Options.Keys);
    options.Set(CommonOptionBag.LegacyNoModularTermination, true);
    Assert.Equal(true, new DooFile.ManifestData(options).Options[
      CommonOptionBag.LegacyNoModularTermination.Name]);
  }

  [Fact]
  public void UnknownManifestEntries() {
    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles/DooFileTest/badManifest.toml");
    var source = File.ReadAllText(filePath);
    Assert.Throws<TomlException>(() => DooFile.ManifestData.Read(new StringReader(source)));
  }

  private static async Task<Program> ParseProgram(string dafnyProgramText, DafnyOptions options) {
    const string fullFilePath = "untitled:foo";
    var rootUri = new Uri(fullFilePath);
    Microsoft.Dafny.Type.ResetScopes();
    var errorReporter = new ConsoleErrorReporter(options);
    var parseResult = await ProgramParser.Parse(dafnyProgramText, rootUri, errorReporter);
    Assert.Equal(0, errorReporter.ErrorCount);
    return parseResult.Program;
  }
}
