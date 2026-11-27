using System.Text.Json;
using Microsoft.Boogie;
using Microsoft.Dafny;
using Microsoft.Dafny.Caching;
using Program = Microsoft.Dafny.Program;

namespace DafnyCore.Test;

[Collection("Cardinality resolution")]
public class PersistentModelsTests {
  private static async Task<(Program Program, Method Method)> Parse(string prefix = "", string uri = "file:///before/model.dfy") {
    Microsoft.Dafny.Type.ResetScopes();
    var options = DafnyOptions.CreateUsingOldParser(TextWriter.Null);
    options.Set(CommonOptionBag.TypeSystemRefresh, true);
    var parsed = await ProgramParser.Parse(prefix + "module M { lemma Bad(x: int) { assert x > 0; } }", new Uri(uri), new BatchErrorReporter(options));
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    return (parsed.Program, SymbolExtensions.GetSymbolDescendants(parsed.Program.DefaultModule).OfType<Method>().Single(m => m.Name == "Bad"));
  }

  private static Model Native() {
    var model = new Model { ModelHasStatesAlready = true };
    var orphan = model.ConstructElement("-1");
    var zero = model.MkElement("0");
    var value = model.MkElement("T!val!0");
    var bitvector = model.MkElement("12bv8");
    var real = model.MkElement("1.5");
    var map = model.MkFunc("map", 1);
    map.AddApp(value, zero);
    map.AddApp(bitvector, value);
    map.Else = real;
    model.MkFunc("alias", 0).SetConstant(value);
    model.MkFunc("empty", null);
    model.MkElement("as-array[map]");
    model.MkElement("Unit");
    model.InitialState.AddBinding("shared", value);
    model.InitialState.AddBinding("unassigned", null!);
    var state = model.MkState("module:M:state:0");
    state.AddBinding("z", zero);
    state.AddBinding("a", value);
    state.AddBinding("literal", orphan);
    state.AddBinding("literalAlias", orphan);
    return model;
  }

  private static StoredModel Capture(Program program, Method method, Model? native = null) => Assert.IsType<StoredModel>(
    PersistentModels.Capture(SourceLocations.For(program), native ?? Native(),
      new Dictionary<string, (TokenRange, string)> { ["module:M:state:0"] = (method.Body!.Body[0].ReportingRange, ": initial state") }));

  [Fact]
  public async Task NativeGraphPreservesOrderedFunctionsAliasesStatesAndValues() {
    var (program, method) = await Parse();
    var native = Native();
    var stored = Capture(program, method, native);
    var restored = Assert.IsType<Model>(PersistentModels.Restore(SourceLocations.For(program), stored, out _));
    Assert.Equal(native.Elements.Select(e => (e.Id, e.Kind, e.ToString())), restored.Elements.Select(e => (e.Id, e.Kind, e.ToString())));
    Assert.Equal(native.Functions.Select(f => (f.Name, f.Arity)), restored.Functions.Select(f => (f.Name, f.Arity)));
    Assert.Equal(native.Functions.SelectMany(f => f.Apps).Select(a => a.ToString()), restored.Functions.SelectMany(f => f.Apps).Select(a => a.ToString()));
    Assert.Equal("1.5", restored.GetFunc("map").Else.ToString());
    Assert.Equal(new[] { "z", "a", "literal", "literalAlias" }, restored.States.Last().Variables);
    Assert.Same(restored.GetFunc("alias").GetConstant(), restored.States.Last().TryGet("a"));
    Assert.Same(restored.States.Last().TryGet("a"), restored.States.Last().TryGet("shared"));
    Assert.True(restored.ModelHasStatesAlready);
    Assert.Contains("unassigned", restored.InitialState.Variables);
    Assert.Null(restored.States.Last().TryGet("unassigned"));
    var literal = restored.States.Last().TryGet("literal");
    Assert.IsType<Model.Integer>(literal);
    Assert.Equal("-1", literal.ToString());
    Assert.Equal(restored.Elements.ElementAt(2).Id, literal.Id);
    Assert.NotSame(restored.Elements.ElementAt(2), literal);
    Assert.DoesNotContain(literal, restored.Elements);
    Assert.Same(literal, restored.States.Last().TryGet("literalAlias"));
  }

  [Fact]
  public async Task NativeStateDescriptionsUseCurrentLocationsWithoutMutatingOptions() {
    var (program, method) = await Parse();
    var stored = Capture(program, method);
    var (current, currentMethod) = await Parse("\n\n", "file:///after/renamed.dfy");
    var restored = Assert.IsType<DafnyModel>(PersistentModels.Restore(SourceLocations.For(current), stored, current.Options));
    Assert.Equal(currentMethod.Body!.Body[0].ReportingRange.ToFileRangeString(current.Options) + ": initial state", restored.States.Last().FullStateName);
    Assert.Equal("module:M:state:0", current.Options.CapturedStateDescription("module:M:state:0"));
    Assert.Same(restored.Model.GetFunc("alias").GetConstant(), restored.Model.States.Last().TryGet("a"));
  }

  [Fact]
  public async Task NativeParsedDatatypeArgumentsAndAliasesRoundTrip() {
    var (program, method) = await Parse();
    var native = Assert.Single(Model.ParseModels(new StringReader("*** MODEL\nf -> (C 0)\n*** END_MODEL\n")));
    var value = Assert.IsType<Model.DatatypeValue>(native.GetFunc("f").GetConstant());
    Assert.Equal("C", value.ConstructorName);
    Assert.Equal("0", Assert.Single(value.Arguments).ToString());
    var state = native.MkState("module:M:state:0");
    state.AddBinding("x", value);
    state.AddBinding("alias", value.Arguments[0]);
    var stored = Capture(program, method, native);
    var restored = Assert.IsType<Model>(PersistentModels.Restore(SourceLocations.For(program), stored, out _));
    var current = Assert.IsType<Model.DatatypeValue>(restored.GetFunc("f").GetConstant());
    Assert.Equal(value.ToString(), current.ToString());
    Assert.Equal(value.Id, current.Id);
    Assert.Same(current, restored.States.Last().TryGet("x"));
    Assert.Same(current.Arguments[0], restored.States.Last().TryGet("alias"));
    var argument = stored.Elements.FindIndex(e => e.Kind == Model.ElementKind.DataValue && e.Arguments.Count > 0);
    var invalid = stored.Elements.ToList();
    invalid[argument] = invalid[argument] with { Arguments = [argument] };
    Assert.Null(PersistentModels.Restore(SourceLocations.For(program), stored with { Elements = invalid }, out _));
  }

  [Fact]
  public async Task FailurePacketsRequireAndExposeNativeModelsWhenRequested() {
    var (program, method) = await Parse();
    var locations = SourceLocations.For(program);
    var diagnostic = new DafnyDiagnostic(MessageSource.Verifier, null!, method.Body!.Body[0].ReportingRange,
      ["an assertion might not hold"], ErrorLevel.Error, []);
    var packet = Assert.IsType<StoredFailureReport>(PersistentFailureReports.Capture(locations, 1, [diagnostic]));
    var stored = Capture(program, method);
    packet = packet with { Models = [new StoredFailureModel(stored, false)] };
    Assert.Null(PersistentFailureReports.Restore(locations, packet));
    Assert.Null(PersistentFailureReports.Restore(locations, packet, program.Options));
    program.Options.ExtractCounterexample = true;
    Assert.Null(PersistentFailureReports.Restore(locations, packet with { Models = null }, program.Options));
    Assert.Null(PersistentFailureReports.Restore(locations, packet with { Models = [] }, program.Options));
    var restored = Assert.Single(PersistentFailureReports.Restore(locations, packet, program.Options, out var models)!);
    Assert.Equal(diagnostic.MessageParts, restored.MessageParts);
    Assert.Single(models!);
    Assert.Equal("1.5", models![0].Model.GetFunc("map").Else.ToString());
    var entry = new PersistentModuleCache.TaskEntry(locations.Symbol(method), false, "M.Bad", 0, 7, true,
      1, 1, 42, 1, 0, new string('a', 64), Outcome: SolverOutcome.Invalid, ResourceLimit: 1000, Failure: packet);
    var task = new PersistentModuleCache.PersistedTask(method, new CanVerifyOrigin(method), entry, [], failureDiagnostics: [restored], models: models);
    Assert.Same(models, task.Models);
    Assert.Throws<InvalidOperationException>(() => task.Split);
  }

  [Fact]
  public async Task InvalidModelGraphsAndSourceBindingsMiss() {
    var (program, method) = await Parse();
    var stored = Capture(program, method);
    var state = stored.States[1];
    var function = stored.Functions[0];
    var invalid = new[] {
      stored with { Elements = [] },
      stored with { Elements = [stored.Elements[1], .. stored.Elements.Skip(1)] },
      stored with { Elements = [stored.Elements[0] with { Kind = (Model.ElementKind)99 }, .. stored.Elements.Skip(1)] },
      stored with { Elements = [.. stored.Elements.Take(2), stored.Elements[2] with { References = [new(999, 0)] }, .. stored.Elements.Skip(3)] },
      stored with { Elements = [.. stored.Elements.Take(2), stored.Elements[2] with { References = [new(0, 999)] }, .. stored.Elements.Skip(3)] },
      stored with { Functions = [function with { Arity = 2 }, .. stored.Functions.Skip(1)] },
      stored with { Functions = [function with { Otherwise = 999 }, .. stored.Functions.Skip(1)] },
      stored with { Functions = [function with { Applications = [function.Applications[0] with { Result = -1 }] }, .. stored.Functions.Skip(1)] },
      stored with { Functions = [.. stored.Functions, function] },
      stored with { States = [] },
      stored with { States = [stored.States[0] with { Location = state.Location }, state] },
      stored with { States = [stored.States[0], state with { Name = "<initial>" }] },
      stored with { States = [stored.States[0], state, state] },
      stored with { States = [stored.States[0], state with { Location = state.Location! with { SourceOriginal = "changed" } }] },
      stored with { States = [stored.States[0], state with { Suffix = "model.dfy" }] },
      stored with { States = [stored.States[0], state with { Variables = [state.Variables[0] with { Element = 999 }] }] },
    };
    foreach (var candidate in invalid) { Assert.Null(PersistentModels.Restore(SourceLocations.For(program), candidate, out _)); }
    var wrongHistory = Native();
    wrongHistory.GetFunc("map").AddApp(wrongHistory.GetFunc("alias").GetConstant(), wrongHistory.False);
    var unusual = Capture(program, method, wrongHistory);
    var restored = Assert.IsType<Model>(PersistentModels.Restore(SourceLocations.For(program), unusual, out _));
    Assert.Equal(wrongHistory.GetFunc("alias").GetConstant().References.Select(r => r.ToString()),
      restored.GetFunc("alias").GetConstant().References.Select(r => r.ToString()));
  }

  [Theory]
  [InlineData("model", "Elements")]
  [InlineData("model", "Functions")]
  [InlineData("model", "States")]
  [InlineData("model", "HasStates")]
  [InlineData("element", "Kind")]
  [InlineData("element", "Value")]
  [InlineData("element", "Id")]
  [InlineData("element", "Registered")]
  [InlineData("element", "Arguments")]
  [InlineData("element", "References")]
  [InlineData("reference", "Function")]
  [InlineData("reference", "Application")]
  [InlineData("function", "Name")]
  [InlineData("function", "Arity")]
  [InlineData("function", "Applications")]
  [InlineData("function", "Otherwise")]
  [InlineData("application", "Result")]
  [InlineData("application", "Arguments")]
  [InlineData("state", "Name")]
  [InlineData("state", "Variables")]
  [InlineData("state", "Location")]
  [InlineData("state", "Suffix")]
  [InlineData("variable", "Name")]
  [InlineData("variable", "Element")]
  [InlineData("failure", "Model")]
  [InlineData("failure", "Render")]
  public async Task EveryNativePayloadFieldIsRequired(string table, string field) {
    var (program, method) = await Parse();
    var stored = Capture(program, method);
    object value = table switch {
      "model" => stored, "element" => stored.Elements[0], "reference" => stored.Elements[2].References[0], "function" => stored.Functions[0],
      "application" => stored.Functions[0].Applications[0], "state" => stored.States[1], "variable" => stored.States[1].Variables[0],
      _ => new StoredFailureModel(stored, true)
    };
    var json = JsonSerializer.SerializeToNode(value, value.GetType())!;
    Assert.NotNull(JsonSerializer.Deserialize(json, value.GetType()));
    json.AsObject().Remove(field);
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, value.GetType()));
  }
}
