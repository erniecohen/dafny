// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using Microsoft.BaseTypes;
using Microsoft.Dafny;
using Xunit;
using Bpl = Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace DafnyB3Normalizer.Test;

[Collection("B3 translation")]
public class B3IfGuardCertificateTests {
  private static string Header => "function {:bvbuiltin \"bv2int\"} N(x:bv3):int; function F(x:bv3):int; " +
    "axiom (forall b:bv3 :: { F(b) } ((0 <= F(b) && F(b) < 8) && F(b) == N(b))); ";
  private static (Bpl.Program Source, DafnyOptions Options) Parse(string body, string extra = "") {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.DafnyPrelude = Path.Combine(AppContext.BaseDirectory, "DafnyPrelude.bpl");
    Assert.Equal(0, Bpl.Parser.Parse(Header + extra + "procedure P(x:bv3); implementation P(x:bv3) { " + body + " }",
      "B3IfGuardCertificateTests.bpl", out var source));
    Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options));
    return (source, options);
  }
  private static IEnumerable<Bpl.IfCmd> Ifs(Bpl.StmtList list) {
    foreach (var block in list.BigBlocks) {
      if (block.ec is Bpl.IfCmd conditional) {
        for (var current = conditional; current != null; current = current.ElseIf) {
          yield return current;
          foreach (var child in Ifs(current.Thn)) { yield return child; }
          if (current.ElseBlock != null) { foreach (var child in Ifs(current.ElseBlock)) { yield return child; } }
        }
      }
      if (block.ec is Bpl.WhileCmd loop) { foreach (var child in Ifs(loop.Body)) { yield return child; } }
    }
  }

  // G01-G10 cover complete producer topology, not a flat expression match.
  [Theory]
  [InlineData("if (F(x) >= 0) { assert true; } else { assert false; }", 1, "inlined-then", "inlined-else")]
  [InlineData("if (F(x) >= 0) { assert true; } assert true;", 1, "inlined-then", "negative-runoff")]
  [InlineData("if (F(x) >= 0) { assert F(x) < 8; } else { assert F(x) < 8; }", 1, "inlined-then", "inlined-else")]
  [InlineData("if (F(x) >= 0) { T: assert true; } else { assert true; }", 1, "dedicated-then", "inlined-else")]
  [InlineData("if (F(x) >= 0) { assert true; } else { E: assert true; }", 1, "inlined-then", "dedicated-else")]
  [InlineData("if (F(x) >= 0) { T: assert true; } else { E: assert true; }", 1, "dedicated-then", "dedicated-else")]
  [InlineData("if (F(x) >= 0) { assert true; } else if (F(x) < 8) { assert true; } else { assert false; }", 2, "inlined-then", "else-if-predecessor")]
  [InlineData("if (F(x) >= 0) { T: assert true; } else if (F(x) < 8) { U: assert true; }", 2, "dedicated-then", "else-if-predecessor")]
  [InlineData("if (F(x) >= 0) { if (F(x) < 8) { assert true; } } assert true;", 2, "inlined-then", "negative-runoff")]
  [InlineData("if (0 <= F(x)) { } assert F(x) < 8;", 1, "inlined-then", "negative-runoff")]
  public void ProducerOwnedPairsKeepTheirExactOwnerAndSlots(string body, int pairs, string positiveRole, string negativeRole) {
    var (source, options) = Parse(body); var unit = source.Implementations.Single();
    var catalogue = B3StructuredCfgCorrespondence.DescribeIfGuards(unit);
    Assert.Equal(pairs, catalogue.Count);
    var owners = Ifs(unit.StructuredStmts).ToArray();
    Assert.True(catalogue.TryGet(unit, owners[0], out var first));
    Assert.Equal(positiveRole, first.Positive.Role); Assert.Equal(negativeRole, first.Negative.Role);
    var commands = new HashSet<Bpl.Cmd>(ReferenceEqualityComparer.Instance);
    var paths = new HashSet<string>(StringComparer.Ordinal);
    foreach (var owner in owners) {
      Assert.True(catalogue.TryGet(unit, owner, out var certificate));
      Assert.Same(owner.Guard, certificate.Guard); Assert.True(paths.Add(certificate.SourcePath));
      Assert.Same(owner.Guard, certificate.Positive.Expression);
      foreach (var slot in new[] { certificate.Positive, certificate.Negative }) {
        Assert.Same(unit.Blocks[slot.BlockIndex], slot.Block);
        Assert.Same(slot.Block.Cmds[slot.CommandIndex], slot.Command);
        Assert.Same(slot.Command.Attributes, slot.Attributes);
        Assert.True(commands.Add(slot.Command));
      }
    }
    catalogue.Recheck();
    var result = B3Normalizer.Normalize(source, unit, options); Valid(result);
    Assert.Equal(unit.Blocks.SelectMany(block => block.Cmds).OfType<Bpl.AssertCmd>().Count(), result.Obligations.Count);
  }


  private static void Valid(B3NormalizationResult result) {
    Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.Code + ": " + d.Message)));
    Assert.Empty(result.Program!.Axioms);
    B3DefinitionContexts.ValidatePartition(result.Program, result.Obligations, result.Contexts!, Bpl.Token.NoToken);
  }
  private static void Unsupported(B3NormalizationResult result, string code = "b3_unsigned_wrapper") {
    Assert.False(result.Success); Assert.Null(result.Program); Assert.Empty(result.Obligations);
    Assert.Contains(result.Diagnostics, d => d.Code == code);
  }
  private static Ir.Application Application() => new("sF", "int", new[] { new Ir.Variable("sX", "#bv3") });
  private static Ir.Operation AtLeastZero(Ir.Expression expression) => new(Ir.Operator.LessEqual,
    "bool", new Ir.Expression[] { new Ir.IntegerLiteral("0"), expression });
  private sealed record Fixture(Bpl.Program Source, Bpl.Implementation Unit, Bpl.IfCmd Owner,
    B3StructuredCfgCorrespondence.IfGuardCertificate Certificate, B3UnsignedWrappers Helper,
    Ir.Program Original, IReadOnlyList<Ir.SourceIdentity> Identities,
    IReadOnlyDictionary<string, IReadOnlyList<B3DefinitionContexts.Formula>> Definitions,
    IReadOnlyList<B3VerificationContext> Contexts, Ir.Conditional Conditional,
    Ir.Application PrefixApplication, Ir.Application GuardApplication, Ir.Application CheckApplication);
  private static Fixture Captured(bool reuseApplication = false, bool learn = false, bool masks = false, bool extract = false, bool duplicateGuard = false) {
    var (source, _) = Parse("if (F(" + (extract ? "x[3:0]" : "x") + ") >= 0) { assert F(x) >= 0; assume true; } else { assert false; }");
    var unit = source.Implementations.Single(); var owner = Ifs(unit.StructuredStmts).Single();
    var catalogue = B3StructuredCfgCorrespondence.DescribeIfGuards(unit);
    Assert.True(catalogue.TryGet(unit, owner, out var certificate));
    var helper = new B3UnsignedWrappers(source, unit, catalogue);
    var call = Assert.IsType<Bpl.NAryExpr>(((Bpl.NAryExpr)owner.Guard).Args[0]);
    var prefixApplication = Application(); var guardApplication = reuseApplication ? prefixApplication : Application();
    var prefix = helper.InCommand(certificate.Positive.Command, () => {
      helper.Capture(call, prefixApplication); return new Ir.Assume(AtLeastZero(prefixApplication));
    });
    var checkApplication = Application();
    var assertion = unit.Blocks.SelectMany(block => block.Cmds).OfType<Bpl.AssertCmd>().First();
    var assertedCall = Assert.IsType<Bpl.NAryExpr>(((Bpl.NAryExpr)assertion.Expr).Args[0]);
    var check = helper.InCommand(assertion, () => {
      helper.Capture(assertedCall, checkApplication); return new Ir.Check("sO0", AtLeastZero(checkApplication), learn);
    });
    var negativeApplication = Application();
    var negative = helper.InCommand(certificate.Negative.Command, () => {
      helper.Capture(call, negativeApplication);
      return new Ir.Assume(new Ir.Operation(Ir.Operator.Less, "bool",
        new Ir.Expression[] { negativeApplication, new Ir.IntegerLiteral("0") }));
    });
    var conditional = helper.InIfGuard(owner, () => {
      helper.Capture(call, guardApplication);
      var condition = AtLeastZero(guardApplication);
      // A deterministic identity can copy one certified application inside its
      // owned condition. Both occurrence paths must be bound in every mask.
      return duplicateGuard ? new Ir.Operation(Ir.Operator.And, "bool", new[] { condition, condition }) : condition;
    }, new Ir.Block(new Ir.Statement[] { prefix, check, new Ir.Assume(new Ir.BooleanLiteral(true)) }),
      new Ir.Block(new Ir.Statement[] { negative, new Ir.Check("sO1", new Ir.BooleanLiteral(false), false) }));
    var original = new Ir.Program(Array.Empty<string>(),
      new[] { new Ir.Function("sF", new[] { new Ir.Binding("sP", "#bv3") }, "int") }, Array.Empty<Ir.Axiom>(),
      new Ir.Unit("sUnit", new[] { new Ir.Binding("sX", "#bv3") },
        new Ir.Block(new Ir.Statement[] { conditional, new Ir.Assume(new Ir.BooleanLiteral(true)) })));
    helper.BindOriginalGuardOccurrences(original);
    var identities = new[] { new Ir.SourceIdentity("sO0", "source.dfy", 1, 1, "assertion"),
      new Ir.SourceIdentity("sO1", "source.dfy", 1, 2, "assertion") };
    var definitions = new Dictionary<string, IReadOnlyList<B3DefinitionContexts.Formula>> {
      ["sO0"] = masks ? new[] { new B3DefinitionContexts.Formula(
        new B3DefinitionOrigin("sD", "sG", 0, "test-formula", "closed"), new Ir.BooleanLiteral(true)) } : Array.Empty<B3DefinitionContexts.Formula>(),
      ["sO1"] = Array.Empty<B3DefinitionContexts.Formula>()
    };
    var contexts = B3DefinitionContexts.Create(original, identities, definitions, unit.tok);
    return new Fixture(source, unit, owner, certificate, helper, original, identities, definitions, contexts,
      conditional, prefixApplication, guardApplication, checkApplication);
  }
  private static Ir.Program Lower(Fixture fixture) => fixture.Helper.Lower(fixture.Original,
    fixture.Identities, fixture.Definitions, fixture.Contexts);
  private static IEnumerable<Ir.Statement> Statements(Ir.Statement root) {
    yield return root;
    var children = root switch {
      Ir.Block block => block.Statements, Ir.Choice choice => choice.Branches,
      Ir.Conditional conditional => new[] { conditional.Then, conditional.Else }, Ir.Labeled labeled => new[] { labeled.Body },
      Ir.Loop loop => new[] { loop.Body }, _ => Array.Empty<Ir.Statement>()
    };
    foreach (var child in children) { foreach (var statement in Statements(child)) { yield return statement; } }
  }
  private static IEnumerable<Ir.Expression> Expressions(Ir.Expression root) {
    yield return root;
    var children = root switch {
      Ir.Application application => application.Arguments, Ir.Operation operation => operation.Arguments,
      Ir.BitvectorOperation word => word.Arguments, Ir.Let let => new[] { let.Value, let.Body },
      Ir.Label label => new[] { label.Body },
      Ir.Quantifier quantifier => quantifier.Patterns.SelectMany(p => p).Append(quantifier.Body),
      _ => Array.Empty<Ir.Expression>()
    };
    foreach (var child in children) { foreach (var expression in Expressions(child)) { yield return expression; } }
  }
  private static int Unsigned(Ir.Program program) => Statements(program.Unit.Body).SelectMany(statement => statement switch {
    Ir.Check check => new[] { check.Condition }, Ir.Assume assume => new[] { assume.Condition },
    Ir.Conditional conditional => new[] { conditional.Condition }, Ir.Assign assign => new[] { assign.Value },
    _ => Array.Empty<Ir.Expression>()
  }).SelectMany(Expressions).OfType<Ir.BitvectorOperation>().Count(word => word.Operator == Ir.BitvectorOperator.BitvectorToUnsignedInt);

  // G11: one mutable source guard/call reused under different If owners.
  [Fact]
  public void SharedSourceGuardHasDistinctOwnerAndTargetOccurrences() {
    var (source, options) = Parse("if (F(x) >= 0) { assert true; } if (F(x) >= 0) { assert true; }");
    var unit = source.Implementations.Single(); var owners = Ifs(unit.StructuredStmts).ToArray();
    var catalogue = B3StructuredCfgCorrespondence.DescribeIfGuards(unit);
    Assert.True(catalogue.TryGet(unit, owners[1], out var second));
    owners[1].Guard = owners[0].Guard; second.Positive.Command.Expr = owners[0].Guard;
    second.Negative.Command.Expr = Bpl.Expr.Not(owners[0].Guard);
    Assert.Equal(0, source.Typecheck(options));
    var result = B3Normalizer.Normalize(source, unit, options); Valid(result);
    var conditions = Statements(result.Program!.Unit.Body).OfType<Ir.Conditional>().Select(c => c.Condition).ToArray();
    Assert.Equal(2, conditions.Length); Assert.NotSame(conditions[0], conditions[1]);
    Assert.All(conditions, condition => Assert.Contains(Expressions(condition), e => e is Ir.BitvectorOperation));
    Assert.Equal(2, result.Obligations.Count);
  }
  // G12-G14: sharing a raw prefix/call cannot share invocation authority.
  [Fact]
  public void OrdinaryPositivePrefixAndGuardKeepDifferentApplications() {
    var fixture = Captured(); Assert.NotSame(fixture.PrefixApplication, fixture.GuardApplication);
    Assert.Same(fixture.Owner.Guard, fixture.Certificate.Positive.Command.Expr);
    var lower = Lower(fixture); Assert.Equal(4, Unsigned(lower));
    var final = B3DefinitionContexts.Create(lower, fixture.Identities, fixture.Definitions, fixture.Unit.tok);
    fixture.Helper.ValidateContexts(fixture.Contexts, final);
  }
  [Fact]
  public void PrefixApplicationCannotBeReusedByGuardCapture() =>
    Assert.Throws<B3UnsignedWrappers.Rejection>(() => Captured(reuseApplication: true));
  [Fact]
  public void UnownedThirdInvocationCannotBorrowTheSharedGuardCall() {
    var fixture = Captured(); var call = (Bpl.NAryExpr)((Bpl.NAryExpr)fixture.Owner.Guard).Args[0];
    var unowned = new Bpl.AssumeCmd(fixture.Owner.tok, fixture.Owner.Guard);
    Assert.Throws<B3UnsignedWrappers.Rejection>(() => fixture.Helper.InCommand(unowned, () => {
      var application = Application(); fixture.Helper.Capture(call, application); return new Ir.Assume(AtLeastZero(application));
    }));
  }
  // G15-G17: each replay has its own guard path and ordinary learning rules.
  [Theory]
  [InlineData("two-masks")]
  [InlineData("learning")]
  [InlineData("nonlearning")]
  public void EveryRetainedReplayOccurrenceHasItsOwnCertificate(string mode) {
    var fixture = Captured(learn: mode == "learning", masks: true, duplicateGuard: mode == "two-masks");
    Assert.Equal(2, fixture.Contexts.Count);
    var lower = Lower(fixture);
    var final = B3DefinitionContexts.Create(lower, fixture.Identities, fixture.Definitions, fixture.Unit.tok);
    fixture.Helper.ValidateContexts(fixture.Contexts, final);
    Assert.All(final, context => {
      var conditional = Assert.Single(Statements(context.Program.Unit.Body).OfType<Ir.Conditional>());
      Assert.Contains(Expressions(conditional.Condition), e => e is Ir.BitvectorOperation);
      Assert.Equal(mode == "two-masks" ? 2 : 1, Expressions(conditional.Condition).OfType<Ir.BitvectorOperation>().Count());
      var expected = context.Obligations.Any(o => o.Id == "sO0") || mode == "learning" ? 4 : 3;
      Assert.Equal(expected + (mode == "two-masks" ? 1 : 0), Unsigned(context.Program));
    });
    B3DefinitionContexts.ValidatePartition(lower, fixture.Identities, final, fixture.Unit.tok);
  }
  // G18-G25: even coherent producer mutations cannot renew an old witness.
  [Theory]
  [InlineData("positive-expression")]
  [InlineData("negative-expression")]
  [InlineData("positive-attribute")]
  [InlineData("negative-attribute")]
  [InlineData("block-order")]
  [InlineData("coherent-relocation")]
  [InlineData("coherent-operator")]
  [InlineData("argument-child")]
  public void CapturedProducerOrSemanticFieldsCannotChange(string change) {
    var fixture = Captured(extract: change == "argument-child"); var certificate = fixture.Certificate;
    switch (change) {
      case "positive-expression": certificate.Positive.Command.Expr = Bpl.Expr.False; break;
      case "negative-expression": certificate.Negative.Command.Expr = Bpl.Expr.False; break;
      case "positive-attribute": certificate.Positive.Command.Attributes = Partition(); break;
      case "negative-attribute": certificate.Negative.Command.Attributes = Partition(); break;
      case "block-order": (fixture.Unit.Blocks[1], fixture.Unit.Blocks[2]) = (fixture.Unit.Blocks[2], fixture.Unit.Blocks[1]); break;
      case "coherent-relocation":
        fixture.Owner.Thn.BigBlocks[0].simpleCmds.Reverse();
        certificate.Positive.Block.Cmds = fixture.Owner.Thn.PrefixCommands.Concat(fixture.Owner.Thn.BigBlocks[0].simpleCmds).ToList();
        B3StructuredCfgCorrespondence.Validate(fixture.Unit); break;
      case "coherent-operator":
        ((Bpl.NAryExpr)fixture.Owner.Guard).Fun = new Bpl.BinaryOperator(fixture.Owner.tok, Bpl.BinaryOperator.Opcode.Lt);
        ((Bpl.NAryExpr)certificate.Negative.Expression).Fun = new Bpl.BinaryOperator(fixture.Owner.tok, Bpl.BinaryOperator.Opcode.Le);
        B3StructuredCfgCorrespondence.Validate(fixture.Unit); break;
      case "argument-child":
        var call = (Bpl.NAryExpr)((Bpl.NAryExpr)fixture.Owner.Guard).Args[0];
        ((Bpl.BvExtractExpr)call.Args[0]).Bitvector = new Bpl.LiteralExpr(call.tok, BigNum.FromInt(1), 3);
        B3StructuredCfgCorrespondence.Validate(fixture.Unit); break;
    }
    var failure = Record.Exception(() => Lower(fixture));
    Assert.True(failure is B3UnsignedWrappers.Rejection or B3StructuredCfgCorrespondence.Rejection,
      failure?.ToString() ?? "Mutation unexpectedly retained its old certificate");
  }
  private static Bpl.QKeyValue Partition() => new(Bpl.Token.NoToken, "partition", new List<object>(), null);
  // G26-G28: source-owner and original/replay occurrence paths are independent.
  [Fact]
  public void ForeignUnitCannotBorrowAnOwnersCatalogue() {
    var fixture = Captured(); var (other, _) = Parse("if (F(x) >= 0) { assert true; }");
    var foreign = other.Implementations.Single();
    var catalogue = B3StructuredCfgCorrespondence.DescribeIfGuards(fixture.Unit);
    Assert.False(catalogue.TryGet(foreign, fixture.Owner, out _));
    var helper = new B3UnsignedWrappers(fixture.Source, foreign, catalogue);
    Assert.Throws<B3UnsignedWrappers.Rejection>(() => helper.InIfGuard(fixture.Owner, () => {
      var application = Application(); helper.Capture((Bpl.NAryExpr)((Bpl.NAryExpr)fixture.Owner.Guard).Args[0], application);
      return AtLeastZero(application);
    }, new Ir.Block(Array.Empty<Ir.Statement>()), new Ir.Block(Array.Empty<Ir.Statement>())));
  }
  [Fact]
  public void ASecondOriginalConditionalPathCannotBorrowTheCapturedApplication() {
    var fixture = Captured(); var children = (Ir.Statement[])((Ir.Block)fixture.Original.Unit.Body).Statements;
    children[1] = fixture.Conditional with { Then = new Ir.Block(Array.Empty<Ir.Statement>()), Else = new Ir.Block(Array.Empty<Ir.Statement>()) };
    var contexts = B3DefinitionContexts.Create(fixture.Original, fixture.Identities, fixture.Definitions, fixture.Unit.tok);
    Assert.Throws<B3UnsignedWrappers.Rejection>(() => fixture.Helper.Lower(fixture.Original, fixture.Identities, fixture.Definitions, contexts));
  }
  [Fact]
  public void AnEquivalentReplayConditionCannotBorrowTheConditionReference() {
    var fixture = Captured(); var before = Assert.Single(fixture.Contexts);
    var children = ((Ir.Block)before.Program.Unit.Body).Statements.ToArray();
    var conditional = (Ir.Conditional)children[0]; var equivalent = ((Ir.Operation)conditional.Condition) with { };
    Assert.Equal(conditional.Condition, equivalent); Assert.NotSame(conditional.Condition, equivalent);
    children[0] = conditional with { Condition = equivalent };
    var changed = before with { Program = before.Program with { Unit = before.Program.Unit with { Body = new Ir.Block(children) } } };
    B3DefinitionContexts.ValidatePartition(fixture.Original, fixture.Identities, new[] { changed }, fixture.Unit.tok);
    Assert.Throws<B3UnsignedWrappers.Rejection>(() => fixture.Helper.Lower(fixture.Original, fixture.Identities, fixture.Definitions, new[] { changed }));
  }
  // G29-G33: the new route supplies no Body/loop/spec/synthetic or oversized origin.
  [Fact]
  public void ABodyChainInsideTheGuardRemainsUnsupported() {
    var (source, options) = Parse("if (G(x) >= 0) { assert true; }", "function {:inline} G(x:bv3):int { F(x) } ");
    Unsupported(B3Normalizer.Normalize(source, source.Implementations.Single(), options), "b3_bitvector_body");
  }
  [Fact]
  public void AnIfInsideWhileDoesNotAcquireANewLoopOrigin() {
    var (source, options) = Parse("while (*) invariant true; { if (F(x) >= 0) { assert true; } break; }");
    var unit = source.Implementations.Single(); Assert.Equal(0, B3StructuredCfgCorrespondence.DescribeIfGuards(unit).Count);
    Unsupported(B3Normalizer.Normalize(source, unit, options));
  }
  [Fact]
  public void AWhileGuardDoesNotBecomeAnIfCertificate() {
    var (source, options) = Parse("while (F(x) >= 0) invariant true; { break; } assert true;");
    var unit = source.Implementations.Single(); Assert.Equal(0, B3StructuredCfgCorrespondence.DescribeIfGuards(unit).Count);
    Unsupported(B3Normalizer.Normalize(source, unit, options));
  }
  [Fact]
  public void SharingTheCertifiedGuardWithASpecDoesNotLicenseTheSpec() {
    var (source, options) = Parse("if (F(x) >= 0) { assert true; }"); var unit = source.Implementations.Single();
    unit.Proc.Requires.Add(new Bpl.Requires(false, Ifs(unit.StructuredStmts).Single().Guard));
    Unsupported(B3Normalizer.Normalize(source, unit, options));
  }
  [Fact]
  public void ADeepSemanticFieldViewRejectsInsteadOfDroppingItsWrapperLeaf() {
    var (source, _) = Parse("if (F(x) >= 0) { assert true; }");
    Bpl.Expr expression = Ifs(source.Implementations.Single().StructuredStmts).Single().Guard;
    for (var i = 0; i < Ir.Protocol.MaximumDepth; i++) {
      expression = new Bpl.NAryExpr(expression.tok, new Bpl.UnaryOperator(expression.tok, Bpl.UnaryOperator.Opcode.Not),
        new List<Bpl.Expr> { expression }) { Type = Bpl.Type.Bool };
    }
    var nodes = 0; long bytes = 0;
    var exception = Assert.Throws<B3UnsignedWrappers.Rejection>(() => B3IfGuardSnapshot.Capture(expression, ref nodes, ref bytes));
    Assert.Contains("bound", exception.Message);
    // The occurrence tree is finite, but a declaration's where expression can
    // refer back to that declaration. The field view rejects this cycle safely;
    // it does not create a new source-call path inside the where expression.
    var owner = Ifs(source.Implementations.Single().StructuredStmts).Single();
    var call = (Bpl.NAryExpr)((Bpl.NAryExpr)owner.Guard).Args[0];
    var variable = ((Bpl.IdentifierExpr)call.Args[0]).Decl;
    variable.TypedIdent.WhereExpr = new Bpl.NAryExpr(variable.tok,
      new Bpl.BinaryOperator(variable.tok, Bpl.BinaryOperator.Opcode.Eq),
      new List<Bpl.Expr> { new Bpl.IdentifierExpr(variable.tok, variable), new Bpl.IdentifierExpr(variable.tok, variable) }) { Type = Bpl.Type.Bool };
    nodes = 0; bytes = 0;
    var cycle = Assert.Throws<B3UnsignedWrappers.Rejection>(() => B3IfGuardSnapshot.Capture(owner.Guard, ref nodes, ref bytes));
    Assert.Contains("bound", cycle.Message);
  }
  // G34: source truth/vacuity controls remain actual goals.
  [Fact]
  public void AReachableFalseAfterTheCertifiedGuardIsPreserved() {
    var (source, options) = Parse("if (F(x) >= 0) { assert true; } assert false;");
    var result = B3Normalizer.Normalize(source, source.Implementations.Single(), options); Valid(result);
    Assert.Equal(2, result.Obligations.Count);
    Assert.Contains(Statements(result.Program!.Unit.Body), s => s is Ir.Check { Condition: Ir.BooleanLiteral { Value: false } });
    Assert.All(result.Contexts!, c => Assert.Contains(Statements(c.Program.Unit.Body), s => s is Ir.Check { Condition: Ir.BooleanLiteral { Value: false } }));
  }
}
