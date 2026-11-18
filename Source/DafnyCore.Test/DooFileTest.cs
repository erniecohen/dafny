using Microsoft.Dafny;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tomlyn;

namespace DafnyCore.Test;

public class DooFileTest {
  [Fact]
  public async Task RoundTripCurrentVersion() {
    var options = DafnyOptions.Default;
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

  /// <summary>
  /// A library built without --member-invariants (issue 105) records nothing about the option, so its
  /// manifest is the same as before the option existed; one built with it records it.
  /// </summary>
  [Fact]
  public void MemberInvariantsRecordedOnlyWhenOn() {
    var options = DafnyOptions.CreateUsingOldParser(TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    var name = CommonOptionBag.MemberInvariants.Name;

    var without = new DooFile.ManifestData(options);
    Assert.False(without.Options.ContainsKey(name));
    var writer = new StringWriter();
    without.Write(writer);
    Assert.DoesNotContain(name, writer.ToString());

    options.Set(CommonOptionBag.MemberInvariants, true);
    var with = new DooFile.ManifestData(options);
    Assert.Equal(true, with.Options[name]);
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
