using System.Reactive.Linq;
using System.Text.Json;
using DafnyDriver.Commands;
using Microsoft.Dafny;
using static DafnyDriver.Test.ModernB3TestSupport;

namespace DafnyDriver.Test;

[Collection("Modern B3 CLI")]
public class ModernB3AdmissionTest {
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task SourceLibraryAdmissionWarningIsFrozenOrRejectedByWarningPolicy(bool allowed) {
    using var source = new SourceFile();
    var library = Path.Combine(source.Directory, "library.dfy");
    File.WriteAllText(library, "module Library { }");
    var options = Options();
    options.CliRootSourceUris.Add(new Uri(source.Path));
    options.LibraryFiles.Add(library);
    options.Set(CommonOptionBag.AllowWarnings, allowed);
    options.ApplyBinding(CommonOptionBag.AllowWarnings);
    var (exit, inputs) = await PreparedCliInputs.PrepareAsync(options, CancellationToken.None);
    Assert.Equal(allowed, inputs != null);
    if (!allowed) { Assert.NotEqual(ExitValue.SUCCESS, exit); return; }
    Assert.Equal(ExitValue.SUCCESS, exit);
    Assert.Equal("UnverifiedLibrary", Assert.Single(inputs!.AdmissionDiagnostics).ErrorId);
    Assert.Single(inputs.RootFiles.Where(file => file.ShouldNotVerify && file.ShouldNotCompile));
    var backend = new Backend();
    using var compilation = CliCompilation.CreatePreparedB3(options, inputs, () => backend);
    compilation.Compilation.ShouldProcessSolverOptions = false;
    var diagnostics = new List<DafnyDiagnostic>();
    using var subscription = compilation.Compilation.Updates.OfType<NewDiagnostic>()
      .Subscribe(item => diagnostics.Add(item.Diagnostic));
    compilation.Start();
    await compilation.Resolution;
    Assert.Single(diagnostics.Where(diagnostic => diagnostic.ErrorId == "UnverifiedLibrary"));
    Assert.Equal(ExitValue.SUCCESS, await compilation.GetAndReportExitValue());
  }

  [Theory]
  [InlineData(DafnyOptions.DiagnosticsFormats.PlainText)]
  [InlineData(DafnyOptions.DiagnosticsFormats.JSON)]
  public async Task FailedStandardLibraryAdmissionIsRejectedBeforeParentConstruction(
    DafnyOptions.DiagnosticsFormats format) {
    using var source = new SourceFile();
    var output = new StringWriter();
    var options = Options(output);
    options.DiagnosticsFormat = format;
    options.CliRootSourceUris.Add(new Uri(source.Path));
    options.Set(CommonOptionBag.UseStandardLibraries, true);
    var original = DafnyMain.StandardLibrariesDooUriTarget["py"];
    DafnyMain.StandardLibrariesDooUriTarget["py"] = new Uri(Path.Combine(source.Directory, "missing-stdlib.doo"));
    try {
      var createCalls = 0;
      var compilerCalls = 0;
      var exit = await ModernCliCompilation.RunB3Async(options, CancellationToken.None,
        new ModernCliServices((_, _) => { createCalls++; throw new Exception("parent must not be constructed"); },
          (_, _, _, _) => { compilerCalls++; return Task.FromResult(true); }));
      Assert.Equal((int)ExitValue.PREPROCESSING_ERROR, exit);
      Assert.Equal(0, createCalls);
      Assert.Equal(0, compilerCalls);
      Assert.Contains("missing-stdlib.doo", output.ToString());
      if (format == DafnyOptions.DiagnosticsFormats.JSON) {
        var line = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
          .First(text => text.TrimStart().StartsWith("{", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(line);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
      }
    } finally { DafnyMain.StandardLibrariesDooUriTarget["py"] = original; }
  }

  [Fact]
  public async Task SourceLibraryErrorAndCancelledAdmissionNeverConstructParent() {
    using var source = new SourceFile();
    foreach (var cancelled in new[] { false, true }) {
      var options = Options();
      options.CliRootSourceUris.Add(new Uri(source.Path));
      options.LibraryFiles.Add(Path.Combine(source.Directory, "missing-library.dfy"));
      using var cancellation = new CancellationTokenSource();
      if (cancelled) { cancellation.Cancel(); }
      var calls = 0;
      var exit = await ModernCliCompilation.RunB3Async(options, cancellation.Token,
        new ModernCliServices((_, _) => { calls++; throw new Exception("parent must not be constructed"); },
          (_, _, _, _) => throw new Exception("target must not be invoked")));
      Assert.NotEqual(0, exit);
      Assert.Equal(0, calls);
    }
  }

  [Fact]
  public async Task PreparedInputListsAreOwnedAndAuxiliaryTargetFilesSurvive() {
    using var source = new SourceFile();
    var foreign = Path.Combine(source.Directory, "auxiliary.py");
    File.WriteAllText(foreign, "# auxiliary target source");
    var options = Options();
    options.CliRootSourceUris.Add(new Uri(source.Path));
    options.CliRootSourceUris.Add(new Uri(foreign));
    var (exit, inputs) = await PreparedCliInputs.PrepareAsync(options, CancellationToken.None);
    Assert.Equal(ExitValue.SUCCESS, exit);
    options.CliRootSourceUris.Clear();
    Assert.Single(inputs!.RootFiles);
    Assert.Equal(foreign, Assert.Single(inputs.ForeignFiles));
    Assert.Throws<NotSupportedException>(() => ((IList<DafnyFile>)inputs.RootFiles).Clear());
    Assert.Throws<NotSupportedException>(() => ((IList<string>)inputs.ForeignFiles).Clear());
    Skip(options);
    var backend = new Backend();
    var calls = 0;
    using var compilation = CliCompilation.CreatePreparedB3(options, inputs, () => backend);
    compilation.Compilation.ShouldProcessSolverOptions = false;
    compilation.Start();
    var resolution = await compilation.Resolution;
    Assert.NotNull(resolution);
    var receipt = CliVerificationLedger.Disabled(resolution!.ResolvedProgram);
    var continuation = await ModernCliCompilation.ContinueAsync(receipt, resolution.ResolvedProgram,
      CancellationToken.None, () => { calls++; Assert.Single(inputs.ForeignFiles); return Task.FromResult(true); });
    Assert.True(continuation.Authorized);
    Assert.Equal(1, calls);
  }

  [Fact]
  public async Task StandardInputIsPreparedOnceWithoutReopeningItAsAFile() {
    var options = Options(input: new StringReader("method Main() { }"));
    options.UseStdin = true;
    var (exit, inputs) = await PreparedCliInputs.PrepareAsync(options, CancellationToken.None);
    Assert.Equal(ExitValue.SUCCESS, exit);
    var root = Assert.Single(inputs!.RootFiles);
    Assert.Equal(DafnyFile.StdInUri, root.Uri);
    Assert.Single(options.CliRootSourceUris);
  }

  [Theory]
  [InlineData("hidden")]
  [InlineData("unrecorded")]
  [InlineData("dump")]
  [InlineData("filter")]
  public async Task UnsupportedInvocationIsRejectedBeforeInputOrBackendWork(string mode) {
    var options = Options();
    switch (mode) {
      case "hidden": options.Set(BoogieOptionBag.HiddenNoVerify, true); break;
      case "unrecorded": options.Verify = false; break;
      case "dump": Skip(options); options.PrintFile = "requested.bpl"; break;
      case "filter": options.Set(VerifyCommand.FilterSymbol, "Main"); break;
    }
    var calls = 0;
    var exit = await ModernCliCompilation.RunB3Async(options, CancellationToken.None,
      new ModernCliServices((_, _) => { calls++; throw new Exception("must reject before preparation"); },
        (_, _, _, _) => throw new Exception("must not compile")));
    Assert.Equal((int)ExitValue.PREPROCESSING_ERROR, exit);
    Assert.Equal(0, calls);
  }
}
