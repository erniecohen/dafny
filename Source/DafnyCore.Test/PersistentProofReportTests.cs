using System.Text.Json;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Microsoft.Dafny.Caching;
using DafnyCore.Verifier;
using Program = Microsoft.Dafny.Program;

namespace DafnyCore.Test;

[Collection("Cardinality resolution")]
public class PersistentProofReportTests {
  private const string Source = "module M { lemma Good(x: int) requires x >= 0 { assert x >= 0; } }";

  private static async Task<(Program Program, Method Method)> Parse(string text = Source, string uri = "file:///before/source.dfy") {
    Microsoft.Dafny.Type.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.Set(CommonOptionBag.TypeSystemRefresh, true);
    options.TrackVerificationCoverage = true;
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse(text, new Uri(uri), reporter);
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    Assert.Empty(reporter.AllMessagesByLevel[ErrorLevel.Error]);
    return (parsed.Program, SymbolExtensions.GetSymbolDescendants(parsed.Program.DefaultModule).OfType<Method>().Single(m => m.Name == "Good"));
  }

  private static (PersistentProofReports.Recorder Recorder, StoredTaskProof Task) Capture(Program program, Method method) {
    var manager = new ProofDependencyManager();
    manager.SetCurrentDefinition("scope", method);
    var first = new RequiresDependency(method.Req[0].E.Origin, method.Req[0].E);
    var second = new RequiresDependency(method.Req[0].E.Origin, method.Req[0].E);
    var id = manager.GetProofDependencyId(first);
    manager.GetProofDependencyId(second);
    var recorder = new PersistentProofReports.Recorder(program, manager);
    return (recorder, Assert.IsType<StoredTaskProof>(recorder.CaptureTask(new[] { new LabeledNodeComponent(id), new LabeledNodeComponent(id) }, "scope")));
  }

  private static PersistentProofReports.Restorer? Restore(Program program, StoredProofInventory? inventory) {
    var locations = SourceLocations.For(program);
    return PersistentProofReports.Restorer.Create(locations, inventory);
  }

  [Fact]
  public async Task EqualLookingDependenciesRetainIdentityAndCurrentSourceRanges() {
    var (before, method) = await Parse();
    var (recorder, task) = Capture(before, method);
    var inventory = Assert.IsType<StoredProofInventory>(recorder.Inventory);
    var (after, current) = await Parse("\n\n" + Source, "file:///after/renamed.dfy");
    var restored = Assert.IsType<PersistentProofReports.Restorer>(Restore(after, inventory));
    var covered = Assert.IsAssignableFrom<IReadOnlyList<TrackedNodeComponent>>(restored.Task(task, 0, "scope"));
    var potential = restored.Manager.GetPotentialDependenciesForDefinition("scope").ToList();
    Assert.Equal(2, potential.Count);
    Assert.NotSame(potential[0], potential[1]);
    Assert.Same(potential[0], restored.Manager.GetFullIdDependency(covered[0]));
    Assert.Same(potential[0], restored.Manager.GetFullIdDependency(covered[1]));
    Assert.Equal(current.Req[0].E.StartToken.Uri, potential[0].Range.Uri);
    Assert.Equal(current.Req[0].E.StartToken.line, potential[0].Range.StartToken.line);
    Assert.Equal("x >= 0", potential[0].OriginalString());
  }

  [Theory]
  [InlineData("Kind")]
  [InlineData("Description")]
  [InlineData("Start")]
  [InlineData("End")]
  [InlineData("InclusiveEnd")]
  [InlineData("Original")]
  [InlineData("SourceOriginal")]
  [InlineData("Warnings")]
  [InlineData("Related")]
  public async Task RequiredDependencyFieldsCannotBeOmitted(string field) {
    var (program, method) = await Parse();
    var (recorder, _) = Capture(program, method);
    var json = JsonSerializer.SerializeToNode(recorder.Inventory!.Dependencies[0])!;
    json.AsObject().Remove(field);
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StoredProofDependency>(json));
  }

  [Theory]
  [InlineData(-1)]
  [InlineData(int.MaxValue)]
  public async Task InvalidMembershipMisses(int index) {
    var (program, method) = await Parse();
    var (recorder, task) = Capture(program, method);
    var restored = Assert.IsType<PersistentProofReports.Restorer>(Restore(program, recorder.Inventory));
    Assert.Null(restored.Task(task with { Covered = [index] }, 0, "scope"));
    var inventory = recorder.Inventory!;
    Assert.Null(Restore(program, inventory with { Scopes = [inventory.Scopes[0] with { Dependencies = [index] }] }));
  }

  [Fact]
  public async Task UnsupportedKindsOrChangedExcerptsMiss() {
    var (program, method) = await Parse();
    var (recorder, _) = Capture(program, method);
    var inventory = recorder.Inventory!;
    foreach (var dependency in new[] {
      inventory.Dependencies[0] with { Kind = "unknown" },
      inventory.Dependencies[0] with { SourceOriginal = "different source" },
      inventory.Dependencies[0] with { End = inventory.Dependencies[0].End with { Column = 999 } }
    }) {
      Assert.Null(Restore(program, inventory with { Dependencies = [dependency, inventory.Dependencies[1]] }));
    }
  }

  [Fact]
  public async Task ReportedTriviaIsSeparateFromCurrentSourceEvidence() {
    var (program, method) = await Parse();
    var (recorder, _) = Capture(program, method);
    var inventory = recorder.Inventory!;
    var stored = inventory.Dependencies[0] with { Original = "x>= 0" };
    var restored = Assert.IsType<PersistentProofReports.Restorer>(Restore(program,
      inventory with { Dependencies = [stored, inventory.Dependencies[1]] }));
    var dependency = Assert.IsType<ReplayedProofDependency>(restored.Manager.GetPotentialDependenciesForDefinition("scope").First());
    Assert.Equal("x>= 0", dependency.OriginalText);
    Assert.Equal("x >= 0", dependency.Range.PrintOriginal());
  }

  [Theory]
  [InlineData("null")]
  [InlineData("datatype")]
  [InlineData("translated")]
  [InlineData("export-copy")]
  public async Task ReportScopesDoNotRequireExecutableDeclarations(string shape) {
    var (program, method) = await Parse(Source.Replace("module M {", "module M { datatype D = V"));
    Microsoft.Dafny.Declaration? declaration = shape switch {
      "null" => null,
      "datatype" => SymbolExtensions.GetSymbolDescendants(program.DefaultModule).OfType<IndDatatypeDecl>().Single(d => d.Name == "D"),
      _ => new Cloner().CloneMember(method, false)
    };
    if (declaration is MemberDecl copy) {
      copy.EnclosingClass = method.EnclosingClass;
      copy.SetOrigin(method.Req[0].E.Origin);
      if (shape == "export-copy") { copy.NameNode = new Name("exported-name"); }
    }
    var manager = new ProofDependencyManager();
    manager.SetCurrentDefinition("report-scope", declaration!);
    manager.GetProofDependencyId(new RequiresDependency(method.Req[0].E.Origin, method.Req[0].E));
    var recorder = new PersistentProofReports.Recorder(program, manager);
    var restored = Assert.IsType<PersistentProofReports.Restorer>(Restore(program, recorder.Inventory));
    Assert.Null(restored.Manager.idsByMemberName["report-scope"].Decl);
    Assert.Single(restored.Manager.GetPotentialDependenciesForDefinition("report-scope"));
  }

  [Fact]
  public async Task GeneratedEndExtentsThatDifferFromCurrentSourceDoNotRecord() {
    var (program, method) = await Parse();
    var expression = method.Req[0].E;
    var end = expression.EndToken;
    var changed = new Microsoft.Dafny.Token(end.line, end.col) { Uri = end.Uri, pos = end.pos, val = end.val + "0" };
    var manager = new ProofDependencyManager();
    manager.SetCurrentDefinition("extent", method);
    manager.GetProofDependencyId(new RequiresDependency(new SourceOrigin(expression.StartToken, changed), expression));
    Assert.Null(new PersistentProofReports.Recorder(program, manager).Inventory);
  }

  [Theory]
  [InlineData("inventory", "Dependencies")]
  [InlineData("inventory", "Scopes")]
  [InlineData("scope", "Name")]
  [InlineData("scope", "Dependencies")]
  [InlineData("task", "External")]
  [InlineData("task", "Covered")]
  [InlineData("task", "HadScope")]
  public async Task RequiredMembershipFieldsCannotAcquireDefaults(string table, string field) {
    var (program, method) = await Parse();
    var (recorder, task) = Capture(program, method);
    var inventory = recorder.Inventory!;
    object value = table switch { "inventory" => inventory, "scope" => inventory.Scopes[0], _ => task };
    var json = JsonSerializer.SerializeToNode(value, value.GetType())!;
    json.AsObject().Remove(field);
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, value.GetType()));
  }

}
