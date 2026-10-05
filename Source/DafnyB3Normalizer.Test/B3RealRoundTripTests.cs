// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dafny;
using Xunit;
using Bpl = Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace DafnyB3Normalizer.Test;

[Collection("B3 translation")]
public class B3RealRoundTripTests {
  [Theory]
  [InlineData("variable")]
  [InlineData("negative")]
  [InlineData("zero")]
  public void DirectTypedFloorEmbeddingRewrites(string shape) {
    Ir.Expression value = shape == "variable" ? V("si", "int") : N(shape == "negative" ? "-17" : "0");
    var original = Program(Check("sC", Eq(F(R(value)), value)));
    var prepared = Prepare(original);
    Assert.True(Assert.Single(prepared.Evidence).Applied);
    var equality = Assert.IsType<Ir.Operation>(Assert.Single(Checks(prepared.Requests[0].Program.Unit.Body)).Condition);
    Same(value, equality.Arguments[0]); Same(value, equality.Arguments[1]);
    Assert.Equal(0, NativeFloors(prepared.Requests[0].Program.Unit.Body));
  }

  [Theory]
  [InlineData("variable")]
  [InlineData("nonintegral")]
  public void RealInverseWithoutEmbeddingIsUnchanged(string shape) {
    Ir.Expression value = shape == "variable" ? V("sr", "real") : new Ir.RationalLiteral("-13", "10");
    var original = Program(Check("sC", Eq(R(F(value)), value)));
    var prepared = Prepare(original);
    Assert.False(Assert.Single(prepared.Evidence).Applied);
    Assert.Null(Assert.Single(prepared.Evidence).DeclineReason);
    Same(original, prepared.Requests[0].Program);
    Assert.Equal(1, NativeFloors(prepared.Requests[0].Program.Unit.Body));
  }

  [Fact]
  public async Task ConversionAssignmentsReachBothOriginalChecks() {
    // This is fresh typed Dafny emission, not a historical packet or solver verdict.
    Microsoft.Dafny.Type.ResetScopes();
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.DafnyPrelude = Path.Combine(AppContext.BaseDirectory, "DafnyPrelude.bpl");
    var reporter = new BatchErrorReporter(options);
    var parsed = await ProgramParser.Parse("lemma IntegerRoundTrip(i: int) {\n  assert ((i as real) as int) == i;\n}\n",
      new Uri("file:///B3RealRoundTripTests.dfy"), reporter);
    await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
    Assert.False(reporter.HasErrors, string.Join("\n", reporter.AllMessages.Select(message => message.Message)));
    var checkedUnits = 0;
    foreach (var (_, source) in BoogieGenerator.Translate(parsed.Program, reporter)) {
      Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options));
      foreach (var implementation in source.Implementations) {
        var normalized = B3Normalizer.Normalize(source, implementation, options);
        Assert.True(normalized.Success, string.Join("; ", normalized.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var contexts = normalized.Contexts ?? new[] { new B3VerificationContext("sLegacy", normalized.Program!,
          normalized.Obligations, Array.Empty<B3DefinitionOrigin>()) };
        B3DefinitionContexts.ValidatePartition(normalized.Program!, normalized.Obligations, contexts, implementation.tok);
        var prepared = B3RealContextPreparation.Prepare(contexts, contexts.Select(Request).ToArray(), implementation.tok);
        for (var i = 0; i < contexts.Count; i++) {
          var before = Checks(contexts[i].Program.Unit.Body).ToArray();
          var after = Checks(prepared.Requests[i].Program.Unit.Body).ToArray();
          Assert.Equal(before.Select(check => (check.ObligationId, check.Learn)), after.Select(check => (check.ObligationId, check.Learn)));
          Assert.Equal(contexts[i].Obligations, prepared.Requests[i].Obligations);
          if (NativeFloors(contexts[i].Program.Unit.Body) > 0) {
            Assert.True(before.Length >= 2); // Cast-integrality and the original assertion both remain.
            Assert.True(NativeFloors(prepared.Requests[i].Program.Unit.Body) < NativeFloors(contexts[i].Program.Unit.Body));
            Assert.True(prepared.Evidence[i].Applied); checkedUnits++;
          }
        }
      }
    }
    Assert.False(reporter.HasErrors); Assert.True(checkedUnits > 0);
  }

  [Theory]
  [InlineData("source")]
  [InlineData("temporary")]
  [InlineData("target")]
  public void AssignmentInvalidatesKeyAndDependentExpressions(string overwritten) {
    Ir.Assign write = overwritten switch {
      "source" => new Ir.Assign("si", N("4")),
      "temporary" => new Ir.Assign("st", V("sq", "real")),
      _ => new Ir.Assign("sr", V("sq", "real"))
    };
    var original = Program(Block(new Ir.Assign("st", R(V("si", "int"))),
      new Ir.Assign("sr", V("st", "real")), write, Check("sC", Eq(F(V("sr", "real")), V("si", "int")))));
    var prepared = Prepare(original);
    var equality = Assert.IsType<Ir.Operation>(Assert.Single(Checks(prepared.Requests[0].Program.Unit.Body)).Condition);
    if (overwritten == "temporary") {
      // sr's stored expression was expanded to ToReal(si); st is no longer its dependency.
      Same(V("si", "int"), equality.Arguments[0]);
    } else {
      Assert.IsType<Ir.Operation>(equality.Arguments[0]);
      Assert.Equal(Ir.Operator.ToInt, ((Ir.Operation)equality.Arguments[0]).Operator);
    }
    Assert.Equal(new[] { "st", "sr", write.Variable }, Statements(prepared.Requests[0].Program.Unit.Body).OfType<Ir.Assign>().Select(assign => assign.Variable));
  }

  [Fact]
  public void SelfDependentAssignmentDoesNotCreateRememberedRow() {
    var original = Program(Block(new Ir.Assign("sr", R(F(V("sr", "real")))),
      Check("sC", Eq(F(V("sr", "real")), N("0")))));
    var prepared = Prepare(original);
    Same(original, prepared.Requests[0].Program);
    Assert.False(Assert.Single(prepared.Evidence).Applied);
  }

  [Fact]
  public void HavocInvalidatesAllDependentRows() {
    var original = Program(Block(new Ir.Assign("st", R(V("si", "int"))),
      new Ir.Assign("sr", V("st", "real")), new Ir.Havoc(new[] { "si" }),
      Check("sC", Eq(F(V("st", "real")), F(V("sr", "real"))))));
    var prepared = Prepare(original);
    var check = Assert.Single(Checks(prepared.Requests[0].Program.Unit.Body));
    Assert.Equal(2, NativeFloors(check.Condition));
    Assert.Equal(new[] { "si" }, Assert.Single(Statements(prepared.Requests[0].Program.Unit.Body).OfType<Ir.Havoc>()).Variables);
  }

  [Theory]
  [InlineData("conditional")]
  [InlineData("choice")]
  public void BranchDictionariesStaySeparateAndJoinIsCleared(string kind) {
    var left = Block(new Ir.Assign("sr", R(V("si", "int"))), Check("sLeft", Eq(F(V("sr", "real")), V("si", "int"))));
    var right = Block(new Ir.Assign("sr", R(V("sj", "int"))), Check("sRight", Eq(F(V("sr", "real")), V("sj", "int"))));
    Ir.Statement branch = kind == "conditional" ? new Ir.Conditional(new Ir.BooleanLiteral(true), left, right) : new Ir.Choice(new[] { left, right });
    var original = Program(Block(new Ir.Assign("sr", R(V("si", "int"))), branch,
      Check("sJoin", Eq(F(V("sr", "real")), V("si", "int")))));
    var checks = Checks(Prepare(original).Requests[0].Program.Unit.Body).ToDictionary(check => check.ObligationId);
    Assert.Equal(0, NativeFloors(checks["sLeft"].Condition)); Assert.Equal(0, NativeFloors(checks["sRight"].Condition));
    Assert.Equal(1, NativeFloors(checks["sJoin"].Condition)); Assert.Equal(3, checks.Count);
  }

  [Fact]
  public void LoopHeaderBodyAndExitStartWithoutIncomingRows() {
    var loop = new Ir.Loop(Array.Empty<Ir.Expression>(), Block(Check("sBody", Eq(F(V("sr", "real")), V("si", "int"))),
      new Ir.Assign("sr", R(V("sj", "int"))), new Ir.Exit("sStop")));
    var original = Program(Block(new Ir.Assign("sr", R(V("si", "int"))),
      Check("sEntry", Eq(F(V("sr", "real")), V("si", "int"))), new Ir.Labeled("sStop", loop),
      Check("sAfter", Eq(F(V("sr", "real")), V("si", "int")))));
    var checks = Checks(Prepare(original).Requests[0].Program.Unit.Body).ToDictionary(check => check.ObligationId);
    Assert.Equal(0, NativeFloors(checks["sEntry"].Condition));
    Assert.Equal(1, NativeFloors(checks["sBody"].Condition)); Assert.Equal(1, NativeFloors(checks["sAfter"].Condition));
    var direct = Program(Block(new Ir.Assign("sr", R(V("si", "int"))),
      new Ir.Loop(Array.Empty<Ir.Expression>(), Check("sDirect", Eq(F(V("sr", "real")), V("si", "int")))),
      Check("sDirectAfter", Eq(F(V("sr", "real")), V("si", "int")))));
    Assert.All(Checks(Prepare(direct).Requests[0].Program.Unit.Body), check => Assert.Equal(1, NativeFloors(check.Condition)));
    var invalid = original with { Unit = original.Unit with { Body = new Ir.Loop(new Ir.Expression[] { new Ir.BooleanLiteral(true) }, Block()) } };
    Assert.Throws<B3RealPreparationRejection>(() => Prepare(invalid));
  }

  [Theory]
  [InlineData("label")]
  [InlineData("exit")]
  [InlineData("return")]
  public void LabeledAndAbruptControlClearContinuationRows(string kind) {
    var goal = Check("sC", Eq(F(V("sr", "real")), V("si", "int")));
    Ir.Statement body = kind switch {
      "label" => Block(new Ir.Assign("sr", R(V("si", "int"))), new Ir.Labeled("sStop", goal),
        Check("sAfter", Eq(F(V("sr", "real")), V("si", "int")))),
      "exit" => new Ir.Labeled("sStop", Block(new Ir.Assign("sr", R(V("si", "int"))), new Ir.Exit("sStop"), goal)),
      _ => Block(new Ir.Assign("sr", R(V("si", "int"))), new Ir.Return(), goal)
    };
    var original = Program(body); var prepared = Prepare(original);
    Assert.Equal(Checks(original.Unit.Body).Count(), Checks(prepared.Requests[0].Program.Unit.Body).Count());
    Assert.All(Checks(prepared.Requests[0].Program.Unit.Body), check => Assert.Equal(1, NativeFloors(check.Condition)));
  }

  [Theory]
  [InlineData("assume")]
  [InlineData("learn")]
  [InlineData("nonlearn")]
  public void CheckAndAssumeNeverCreateOptimizerRows(string kind) {
    var equality = Eq(V("sr", "real"), R(V("si", "int")));
    Ir.Statement premise = kind == "assume" ? new Ir.Assume(equality) : Check("sPremise", equality, kind == "learn");
    var prepared = Prepare(Program(Block(premise, Check("sC", Eq(F(V("sr", "real")), V("si", "int"))))));
    var goal = Checks(prepared.Requests[0].Program.Unit.Body).Single(check => check.ObligationId == "sC");
    Assert.Equal(1, NativeFloors(goal.Condition)); Assert.False(Assert.Single(prepared.Evidence).Applied);
  }

  [Theory]
  [InlineData("quantifier")]
  [InlineData("let")]
  [InlineData("application")]
  [InlineData("word")]
  public void BindersApplicationsAndWordsRemainSubstitutionBarriers(string kind) {
    var floor = F(V("sr", "real"));
    Ir.Expression barrier = kind switch {
      "quantifier" => new Ir.Quantifier(true, new[] { new Ir.Binding("sr", "real") },
        new[] { (IReadOnlyList<Ir.Expression>)new[] { floor } }, Eq(floor, N("0"))),
      "let" => new Ir.Let(new Ir.Binding("sr", "real"), V("sq", "real"), floor),
      "application" => new Ir.Application("sf", "int", new[] { floor }),
      _ => new Ir.BitvectorOperation(Ir.BitvectorOperator.IntToBitvector, 8, 0, 0, Ir.Protocol.BitvectorTypeName(8), new[] { floor })
    };
    Ir.Expression condition = barrier.Type == "bool" ? barrier : Eq(barrier, barrier);
    var original = Program(Block(new Ir.Assign("sr", R(V("si", "int"))), Check("sC", condition)),
      functions: kind == "application" ? new[] { new Ir.Function("sf", new[] { new Ir.Binding("sp", "int") }, "int") } : null);
    var prepared = Prepare(original);
    Same(condition, Assert.Single(Checks(prepared.Requests[0].Program.Unit.Body)).Condition);
    Assert.False(Assert.Single(prepared.Evidence).Applied);
    if (kind == "word") {
      var nativeInteger = new Ir.BitvectorOperation(Ir.BitvectorOperator.BitvectorToUnsignedInt, 8, 0, 0, "int", new Ir.Expression[] { new Ir.BitvectorLiteral("7", 8) });
      var outer = Prepare(Program(Check("sOuter", Eq(F(R(nativeInteger)), nativeInteger))));
      Same(nativeInteger, Assert.IsType<Ir.Operation>(Assert.Single(Checks(outer.Requests[0].Program.Unit.Body)).Condition).Arguments[0]);
    }
  }

  [Fact]
  public void RewriteRetainsAllChecksLabelsOriginsAndLearnFlags() {
    var original = Program(Block(new Ir.Labeled("sStop", Block(
      Check("sFalse", new Ir.Label("sFalseLabel", new Ir.BooleanLiteral(false)), false),
      Check("sFold", new Ir.Label("sNumericLabel", Eq(F(R(V("si", "int"))), V("si", "int"))), true))),
      Check("sTail", new Ir.BooleanLiteral(false), false)));
    var context = Context(original); var request = Request(context);
    var prepared = B3RealContextPreparation.Prepare(new[] { context }, new[] { request }, Bpl.Token.NoToken);
    var before = Checks(original.Unit.Body).ToArray(); var after = Checks(prepared.Requests[0].Program.Unit.Body).ToArray();
    Assert.Equal(before.Select(check => (check.ObligationId, check.Learn)), after.Select(check => (check.ObligationId, check.Learn)));
    Assert.All(context.Obligations.Zip(prepared.Requests[0].Obligations), pair => Assert.Same(pair.First, pair.Second));
    Assert.False(Assert.IsType<Ir.BooleanLiteral>(Assert.IsType<Ir.Label>(after[0].Condition).Body).Value);
    Assert.Equal("sNumericLabel", Assert.IsType<Ir.Label>(after[1].Condition).Name);
    Assert.False(Assert.IsType<Ir.BooleanLiteral>(after[2].Condition).Value);
    var submitted = Assert.Single(prepared.Requests);
    var configurationHash = B3RealContextPreparation.ConfigurationHash(submitted.Configuration);
    var originalArguments = Assert.IsType<string[]>(request.Configuration.SolverArguments);
    Assert.NotSame(originalArguments, submitted.Configuration.SolverArguments);
    originalArguments[0] = "-untrusted-mutation";
    Assert.Equal(new[] { "-in", "-smt2" }, submitted.Configuration.SolverArguments);
    Assert.Equal(configurationHash, B3RealContextPreparation.ConfigurationHash(submitted.Configuration));
    Assert.Matches("^[0-9a-f]{64}$", configurationHash);
  }

  [Fact]
  public void G3MasksKeepOriginalPartitionAndStableFunctions() {
    var function = new Ir.Function("sf", Array.Empty<Ir.Binding>(), "int");
    var guard = new Ir.Function("sg", Array.Empty<Ir.Binding>(), "bool");
    var call = new Ir.Application("sf", "int", Array.Empty<Ir.Expression>());
    var original = Program(Block(new Ir.Assign("sr", R(V("si", "int"))),
      Check("sVisible", Eq(F(V("sr", "real")), call)), Check("sHidden", Eq(call, call), false)), new[] { function, guard });
    var identities = Identities(original);
    var formula = new B3DefinitionContexts.Formula(new B3DefinitionOrigin("sDef", "sOwner", 0,
      new string('e', 64), "ground", 1, 1), Op(Ir.Operator.Implies, "bool",
      new Ir.Application("sg", "bool", Array.Empty<Ir.Expression>()), Eq(call, N("7"))));
    var selection = new Dictionary<string, IReadOnlyList<B3DefinitionContexts.Formula>> {
      ["sVisible"] = new[] { formula }, ["sHidden"] = Array.Empty<B3DefinitionContexts.Formula>()
    };
    var contexts = B3DefinitionContexts.Create(original, identities, selection, Bpl.Token.NoToken);
    B3DefinitionContexts.ValidatePartition(original, identities, contexts, Bpl.Token.NoToken);
    var prepared = B3RealContextPreparation.Prepare(contexts, contexts.Select(Request).ToArray(), Bpl.Token.NoToken);
    Assert.Equal(2, prepared.Requests.Count);
    for (var i = 0; i < contexts.Count; i++) {
      Assert.Equal(contexts[i].MaskId, prepared.Evidence[i].MaskId);
      Same(contexts[i].Program.Functions, prepared.Requests[i].Program.Functions);
      Same(contexts[i].Program.Axioms, prepared.Requests[i].Program.Axioms);
      Assert.Equal(contexts[i].Obligations, prepared.Requests[i].Obligations);
      Assert.Equal(Checks(contexts[i].Program.Unit.Body).Select(check => (check.ObligationId, check.Learn)),
        Checks(prepared.Requests[i].Program.Unit.Body).Select(check => (check.ObligationId, check.Learn)));
    }
    var hidden = contexts.Select((context, index) => (context, index)).Single(pair => pair.context.Definitions.Count == 0);
    Assert.Empty(prepared.Requests[hidden.index].Program.Axioms);
    Assert.Single(Statements(prepared.Requests[hidden.index].Program.Unit.Body).OfType<Ir.Assume>());
    Assert.Equal(identities.Select(identity => identity.Id).OrderBy(id => id),
      prepared.Requests.SelectMany(request => request.Obligations).Select(identity => identity.Id).OrderBy(id => id));
  }

  [Fact]
  public async Task ConcurrentPreparationsUseFreshState() {
    var tasks = Enumerable.Range(0, 20).Select(index => Task.Run(() => {
      var integer = N(index.ToString(System.Globalization.CultureInfo.InvariantCulture));
      var original = Program(Block(new Ir.Assign("sr", R(integer)), Check("sC", Eq(F(V("sr", "real")), integer))));
      var prepared = Prepare(original);
      var equality = Assert.IsType<Ir.Operation>(Assert.Single(Checks(prepared.Requests[0].Program.Unit.Body)).Condition);
      Same(integer, equality.Arguments[0]); Assert.True(Assert.Single(prepared.Evidence).Applied);
      return prepared.Requests[0].ProgramHash;
    }));
    var hashes = await Task.WhenAll(tasks); Assert.Equal(20, hashes.Distinct().Count());
  }

  [Theory]
  [InlineData("arity")]
  [InlineData("inner")]
  [InlineData("result")]
  [InlineData("unknown")]
  public void MalformedTypedConversionsAreRejectedBeforeRewrite(string kind) {
    Ir.Expression malformed = kind switch {
      "arity" => Op(Ir.Operator.ToInt, "int", R(V("si", "int")), R(V("sj", "int"))),
      "inner" => F(Op(Ir.Operator.ToReal, "real", V("sr", "real"))),
      "result" => Op(Ir.Operator.ToInt, "real", R(V("si", "int"))),
      _ => F(R(V("sMissing", "int")))
    };
    Assert.Throws<B3RealPreparationRejection>(() => Prepare(Program(Check("sC", Eq(malformed, malformed)))));
    if (kind == "inner") {
      Ir.Expression[] aliases = {
        R(V("si", "int")) with { Type = "int" }, V("si", "int") with { ResultType = "real" },
        new Ir.Application("sf", "int", Array.Empty<Ir.Expression>()) with { ResultType = "real" },
        new Ir.BitvectorOperation(Ir.BitvectorOperator.IntToBitvector, 8, 0, 0, Ir.Protocol.BitvectorTypeName(8), new[] { V("si", "int") }) with { Type = "int" },
        new Ir.Let(new Ir.Binding("sp", "int"), N("0"), N("1")) with { Type = "real" },
        new Ir.Label("sLabel", N("0")) with { Type = "real" }, N("0") with { Type = "real" },
        new Ir.Quantifier(true, Array.Empty<Ir.Binding>(), Array.Empty<IReadOnlyList<Ir.Expression>>(), new Ir.BooleanLiteral(true)) with { Type = "real" },
        new Ir.BooleanLiteral(true) with { Type = "real" }, new Ir.RationalLiteral("1", "1") with { Type = "int" },
        new Ir.BitvectorLiteral("0", 8) with { Type = "real" }
      };
      foreach (var alias in aliases) {
        var rejection = Assert.Throws<B3RealPreparationRejection>(() => Prepare(Program(Check("sAlias", Eq(alias, alias)))));
        Assert.Contains("type aliases", rejection.Message);
      }
    }
  }

  [Theory]
  [InlineData(15, false)]
  [InlineData(16, true)]
  [InlineData(17, true)]
  public void OutputGrowthCountsCopiedOccurrencesBeforeAllocation(int nodeLimit, bool applies) {
    // Independent census: Block(1), Assign+ToReal+ToInt+sr(4),
    // Check+Eq+two copies of ToReal(ToInt(sr))(8), two variables+identity(3).
    // Output has 16 occurrences versus 12 input occurrences, far below the byte cap.
    var original = Program(Block(new Ir.Assign("st", R(F(V("sr", "real")))), Check("sC", Eq(V("st", "real"), V("st", "real")))),
      variables: new[] { new Ir.Binding("sr", "real"), new Ir.Binding("st", "real") });
    var prepared = Prepare(original, new B3RealPreparationLimits(MaximumOutputNodes: nodeLimit));
    Assert.Equal(applies, Assert.Single(prepared.Evidence).Applied);
    Assert.True(B3RealContextPreparation.JsonSize(prepared.Requests[0]) < 8192);
    if (!applies) {
      Assert.NotNull(Assert.Single(prepared.Evidence).DeclineReason); Same(original, prepared.Requests[0].Program);
      Assert.Equal(B3RealContextPreparation.Hash(original), prepared.Requests[0].ProgramHash);
      var rows = Program(Block(new Ir.Assign("sr", R(V("si", "int"))), new Ir.Assign("st", R(V("sj", "int"))),
        Check("sRows", Eq(F(V("sr", "real")), F(V("st", "real"))))));
      foreach (var optional in new[] { new B3RealPreparationLimits(MaximumRows: 1),
                 new B3RealPreparationLimits(MaximumFreeMemberships: 1), new B3RealPreparationLimits(MaximumWork: 1) }) {
        var declined = Prepare(rows, optional); Assert.NotNull(Assert.Single(declined.Evidence).DeclineReason);
        Assert.False(Assert.Single(declined.Evidence).Applied); Same(rows, declined.Requests[0].Program);
      }
      var mutable = new Ir.Statement[] { new Ir.Assign("sr", R(V("si", "int"))), Check("sOwned", Eq(F(V("sr", "real")), V("si", "int"))) };
      var fallback = Prepare(Program(new Ir.Block(mutable)), new B3RealPreparationLimits(MaximumWork: 1));
      var frozenHash = fallback.Requests[0].ProgramHash; mutable[1] = Check("sOwned", new Ir.BooleanLiteral(false));
      Assert.Equal(frozenHash, B3RealContextPreparation.Hash(fallback.Requests[0].Program));
      Assert.IsType<Ir.Operation>(Assert.Single(Checks(fallback.Requests[0].Program.Unit.Body)).Condition);
    } else { Assert.Equal(3, NativeFloors(prepared.Requests[0].Program.Unit.Body)); }
    Assert.Single(Checks(prepared.Requests[0].Program.Unit.Body));
  }

  [Theory]
  [InlineData(6, true)]
  [InlineData(5, true)]
  [InlineData(4, false)]
  public void BoundedTraversalRejectsDepthAndMetadataExcess(int depthLimit, bool applies) {
    // Block -> Check -> Eq -> ToInt -> ToReal -> si has source depth five.
    var original = Program(Block(Check("sC", Eq(F(R(V("si", "int"))), V("si", "int")))));
    var prepared = Prepare(original, new B3RealPreparationLimits(MaximumDepth: depthLimit));
    Assert.Equal(applies, Assert.Single(prepared.Evidence).Applied);
    if (!applies) { Same(original, prepared.Requests[0].Program); Assert.NotNull(Assert.Single(prepared.Evidence).DeclineReason); }
    if (depthLimit == 5) {
      var context = Context(original); var request = Request(context);
      var oversized = new string('\u2028', Ir.Protocol.MaximumMessageBytes / 6 + 1);
      var oversizedContext = context with { Obligations = new[] { context.Obligations[0] with { Description = oversized } } };
      var rejected = Assert.Throws<B3RealPreparationRejection>(() => B3RealContextPreparation.Prepare(new[] { oversizedContext },
        new[] { Request(oversizedContext) }, Bpl.Token.NoToken));
      Assert.Contains("encoded text budget", rejected.Message);
      var slots = new OversizedList<string>(B3RealContextPreparation.MaximumCaptureSlots + 1);
      rejected = Assert.Throws<B3RealPreparationRejection>(() => Prepare(Program(new Ir.Havoc(slots))));
      Assert.Contains("allocation bound", rejected.Message); Assert.Equal(0, slots.ItemReads);
      var detached = new B3VerificationContext(context.MaskId, original with { }, context.Obligations, context.Definitions);
      Assert.Throws<B3RealPreparationRejection>(() => B3RealContextPreparation.Prepare(new[] { detached }, new[] { request }, Bpl.Token.NoToken));
      Ir.Expression deep = N("0");
      for (var i = 0; i <= Ir.Protocol.MaximumDepth; i++) { deep = Op(Ir.Operator.Negate, "int", deep); }
      Assert.Throws<B3RealPreparationRejection>(() => Prepare(Program(Check("sDeep", Eq(deep, N("0"))))));
    }
  }

  [Theory]
  [InlineData("hash")]
  [InlineData("check")]
  [InlineData("learn")]
  [InlineData("control")]
  public void ForgedOrMutatedRewriteCertificateIsRejected(string mutation) {
    var original = Program(Check("sC", Eq(F(R(V("si", "int"))), V("si", "int"))));
    var context = Context(original); var before = Request(context) with { ProgramHash = B3RealContextPreparation.Hash(original) };
    var prepared = B3RealContextPreparation.Prepare(new[] { context }, new[] { before }, Bpl.Token.NoToken);
    var submitted = prepared.Requests[0]; var evidence = prepared.Evidence[0];
    if (mutation == "hash") {
      evidence = evidence with { OriginalProgramHash = new string('f', 64) };
      var list = new ChangingStatements(Check("sC", new Ir.BooleanLiteral(false)), Check("sC", new Ir.BooleanLiteral(true)));
      Assert.Throws<B3RealPreparationRejection>(() => Prepare(Program(new Ir.Block(list))));
      Assert.True(list.ItemReads >= 3);
    } else {
      var check = Assert.Single(Checks(submitted.Program.Unit.Body));
      Ir.Statement body = mutation switch {
        "check" => check with { ObligationId = "sOther" },
        "learn" => check with { Learn = !check.Learn },
        _ => new Ir.Conditional(new Ir.BooleanLiteral(true), check, Block())
      };
      submitted = submitted with { Program = submitted.Program with { Unit = submitted.Program.Unit with { Body = body } } };
      if (mutation == "check") { submitted = submitted with { Obligations = new[] { before.Obligations[0] with { Id = "sOther" } } }; }
      submitted = submitted with { ProgramHash = B3RealContextPreparation.Hash(submitted.Program) };
      evidence = evidence with { FinalProgramHash = submitted.ProgramHash };
    }
    Assert.Throws<B3RealPreparationRejection>(() => B3RealRoundTripRelation.Validate(before, submitted, evidence, Bpl.Token.NoToken));
  }

  private static Ir.Variable V(string name, string type) => new(name, type);
  private static Ir.IntegerLiteral N(string value) => new(value);
  private static Ir.Operation Op(Ir.Operator operation, string type, params Ir.Expression[] args) => new(operation, type, args);
  private static Ir.Operation R(Ir.Expression value) => Op(Ir.Operator.ToReal, "real", value);
  private static Ir.Operation F(Ir.Expression value) => Op(Ir.Operator.ToInt, "int", value);
  private static Ir.Operation Eq(Ir.Expression left, Ir.Expression right) => Op(Ir.Operator.Equal, "bool", left, right);
  private static Ir.Block Block(params Ir.Statement[] statements) => new(statements);
  private static Ir.Check Check(string id, Ir.Expression condition, bool learn = true) => new(id, condition, learn);
  private static Ir.Program Program(Ir.Statement body, IReadOnlyList<Ir.Function>? functions = null, IReadOnlyList<Ir.Binding>? variables = null) =>
    new(Array.Empty<string>(), functions ?? Array.Empty<Ir.Function>(), Array.Empty<Ir.Axiom>(), new Ir.Unit("sUnit", variables ?? new[] {
      new Ir.Binding("si", "int"), new Ir.Binding("sj", "int"), new Ir.Binding("sr", "real"), new Ir.Binding("st", "real"), new Ir.Binding("sq", "real")
    }, body));
  private static IReadOnlyList<Ir.SourceIdentity> Identities(Ir.Program program) => Checks(program.Unit.Body).Select((check, index) =>
    new Ir.SourceIdentity(check.ObligationId, "test:real-roundtrip", index + 1, 1, "assertion")).ToArray();
  private static B3VerificationContext Context(Ir.Program program) => new("sMask", program, Identities(program), Array.Empty<B3DefinitionOrigin>());
  private static Ir.Request Request(B3VerificationContext context) => new(Ir.Protocol.Version, "sRequest", Ir.Protocol.NormalizerVersion,
    new string('a', 40), string.Empty, context.Program.Unit.Name, context.Program,
    new Ir.Configuration("z3", new[] { "-in", "-smt2" }, 20000, 200000, 1024 * 1024, 2, "5.1.0", new string('c', 64)),
    context.Obligations, new string('d', 64));
  private static B3RealPreparedRequests Prepare(Ir.Program program, B3RealPreparationLimits? limits = null) {
    var context = Context(program);
    return B3RealContextPreparation.Prepare(new[] { context }, new[] { Request(context) }, Bpl.Token.NoToken, limits);
  }
  private static void Same<T>(T left, T right) => Assert.True(B3RealContextPreparation.JsonEqual(left, right));
  private static IEnumerable<Ir.Statement> Statements(Ir.Statement root) {
    yield return root;
    IEnumerable<Ir.Statement> children = root switch {
      Ir.Block block => block.Statements, Ir.Choice choice => choice.Branches,
      Ir.Conditional conditional => new[] { conditional.Then, conditional.Else },
      Ir.Loop loop => new[] { loop.Body }, Ir.Labeled labeled => new[] { labeled.Body }, _ => Array.Empty<Ir.Statement>()
    };
    foreach (var child in children) { foreach (var nested in Statements(child)) { yield return nested; } }
  }
  private static IEnumerable<Ir.Check> Checks(Ir.Statement root) => Statements(root).OfType<Ir.Check>();
  private static int NativeFloors(Ir.Statement root) => Statements(root).Sum(statement => statement switch {
    Ir.Assign assign => NativeFloors(assign.Value), Ir.Check check => NativeFloors(check.Condition),
    Ir.Assume assume => NativeFloors(assume.Condition), Ir.Conditional conditional => NativeFloors(conditional.Condition), _ => 0
  });
  private static int NativeFloors(Ir.Expression root) {
    IEnumerable<Ir.Expression> children = root switch {
      Ir.Operation operation => operation.Arguments, Ir.Application application => application.Arguments,
      Ir.BitvectorOperation word => word.Arguments,
      Ir.Quantifier quantifier => new[] { quantifier.Body }.Concat(quantifier.Patterns.SelectMany(pattern => pattern)),
      Ir.Let let => new[] { let.Value, let.Body }, Ir.Label label => new[] { label.Body }, _ => Array.Empty<Ir.Expression>()
    };
    return (root is Ir.Operation { Operator: Ir.Operator.ToInt } ? 1 : 0) + children.Sum(NativeFloors);
  }
  private sealed class OversizedList<T>(int count) : IReadOnlyList<T> {
    public int ItemReads { get; private set; }
    public int Count => count;
    public T this[int index] { get { ItemReads++; throw new InvalidOperationException("Oversized slots must be rejected before indexing"); } }
    public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("No oversized enumeration");
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
  }
  private sealed class ChangingStatements(Ir.Statement before, Ir.Statement after) : IReadOnlyList<Ir.Statement> {
    public int ItemReads { get; private set; }
    public int Count => 1;
    public Ir.Statement this[int index] { get { if (index != 0) { throw new IndexOutOfRangeException(); } return ItemReads++ < 2 ? before : after; } }
    public IEnumerator<Ir.Statement> GetEnumerator() { yield return this[0]; }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
  }
}
