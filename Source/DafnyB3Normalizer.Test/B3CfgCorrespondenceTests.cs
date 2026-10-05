// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using Microsoft.Dafny;
using Xunit;
using Bpl = Microsoft.Boogie;

namespace DafnyB3Normalizer.Test;

[Collection("B3 translation")]
public class B3CfgCorrespondenceTests {
  [Theory]
  [InlineData("var x: int; x := 0; assume x == 0; assert x == 0;")]
  [InlineData("var x: int; if (x < 0) { assert x < 0; } else { assert 0 <= x; }")]
  [InlineData("var x: int; if (x == 0) { } else if (x != 1) { } else { } assert true;")]
  [InlineData("var x: int; if (!true) { A: assert true; } else { B: assert true; }")]
  [InlineData("var x: int; while (x < 3) invariant x <= 3; { x := x + 1; } assert true;")]
  [InlineData("var x: int; while (x < 3) invariant true; { B: if (x == 2) { break; } x := x + 1; } assert true;")]
  [InlineData("while (*) invariant true; { while (*) invariant true; { break; } }")]
  [InlineData("L: if (*) { if (*) { break L; } else { return; } } assert true;")]
  [InlineData("while (*) invariant true; { goto Next; assert false; Next: assert true; }")]
  public void SupportedProducerShapesKeepExactCommandsAndTopology(string body) {
    var (source, options) = Source(body); var unit = source.Implementations.Single();
    var before = Emit(source, options);
    B3StructuredCfgCorrespondence.Validate(unit);
    Assert.Equal(before, Emit(source, options));
    var result = B3Normalizer.Normalize(source, unit, options);
    Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));
  }

  [Theory]
  [InlineData("==")]
  [InlineData("!=")]
  public void TypedBooleanComparisonBranchesPreserveComplementaryGuardSemantics(string operation) {
    var opposite = operation == "==" ? "!=" : "==";
    var (source, options) = B3VisibilityTests.Parse("procedure P(a: bool, b: bool); implementation P(a: bool, b: bool) { " +
      "if (a " + operation + " b) { assert a " + operation + " b; } else { assert a " + opposite + " b; } }");
    var unit = source.Implementations.Single(); var conditional = (Bpl.IfCmd)unit.StructuredStmts!.BigBlocks[0].ec;
    var guard = Assert.IsType<Bpl.NAryExpr>(conditional.Guard);
    Assert.Equal(Bpl.BinaryOperator.Opcode.Iff, Assert.IsType<Bpl.BinaryOperator>(guard.Fun).Op);
    if (operation == "!=") { Assert.IsType<Bpl.UnaryOperator>(Assert.IsType<Bpl.NAryExpr>(guard.Args[1]).Fun); }
    var before = Emit(source, options); B3StructuredCfgCorrespondence.Validate(unit);
    var result = B3Normalizer.Normalize(source, unit, options);
    Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
    Assert.Equal(before, Emit(source, options));
  }

  [Theory]
  [InlineData("==", "changed")]
  [InlineData("==", "same-polarity")]
  [InlineData("==", "swapped")]
  [InlineData("!=", "changed")]
  [InlineData("!=", "same-polarity")]
  [InlineData("!=", "swapped")]
  public void BooleanComplementMatchingDoesNotGuessCapturesPolarityOrOperandOrder(string operation, string mutation) {
    var (source, options) = B3VisibilityTests.Parse("procedure P(a: bool, b: bool, c: bool); implementation P(a: bool, b: bool, c: bool) { " +
      "if (a " + operation + " b) { assert true; } else { assert true; } }");
    var unit = source.Implementations.Single(); var conditional = (Bpl.IfCmd)unit.StructuredStmts!.BigBlocks[0].ec;
    var guard = (Bpl.NAryExpr)conditional.Guard;
    var second = operation == "==" ? guard.Args[1] : ((Bpl.NAryExpr)guard.Args[1]).Args[0];
    var original = (Bpl.AssumeCmd)Assert.Single(conditional.ElseBlock.PrefixCommands);
    original.Expr = mutation switch {
      "changed" => Bpl.Expr.Iff(guard.Args[0], new Bpl.IdentifierExpr(unit.tok, unit.InParams[2])),
      "same-polarity" => guard,
      "swapped" => Bpl.Expr.Iff(second, Bpl.Expr.Not(guard.Args[0])),
      _ => throw new ArgumentException(mutation)
    };
    Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options)); Reject(source, options);
  }

  [Theory]
  [InlineData("insert-assumption")]
  [InlineData("replace-assumption")]
  [InlineData("insert-assignment")]
  [InlineData("replace-assignment")]
  [InlineData("reorder")]
  public void RawOrdinaryCommandMutationsCannotChangeAStillMappedGoal(string mutation) {
    var (source, options) = Source("var x: int; x := 0; assume x == 0; assert x == 0;");
    var unit = source.Implementations.Single(); var original = unit.Blocks[0].Cmds;
    var raw = original.ToList();
    var assignment = (Bpl.AssignCmd)original[0];
    var changedAssignment = new Bpl.AssignCmd(assignment.tok, assignment.Lhss, new List<Bpl.Expr> { Bpl.Expr.Literal(1) });
    var changedAssumption = new Bpl.AssumeCmd(Bpl.Token.NoToken, Bpl.Expr.False);
    switch (mutation) {
      case "insert-assumption": raw.Insert(2, changedAssumption); break;
      case "replace-assumption": raw[1] = changedAssumption; break;
      case "insert-assignment": raw.Insert(2, changedAssignment); break;
      case "replace-assignment": raw[0] = changedAssignment; break;
      case "reorder": (raw[0], raw[1]) = (raw[1], raw[0]); break;
    }
    unit.Blocks[0].Cmds = raw;
    Assert.Same(original.Last(), raw.Last()); // assertion inventory has not changed
    Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options));
    Reject(source, options);
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void RawRedirectedOrRemovedBranchEdgesCannotBypassAnOriginalPath(bool remove) {
    var (source, options) = Source("if (*) { assert true; } else { assert false; } assert true;");
    var unit = source.Implementations.Single(); var jump = (Bpl.GotoCmd)unit.Blocks[0].TransferCmd;
    var target = jump.LabelTargets[0];
    unit.Blocks[0].TransferCmd = new Bpl.GotoCmd(jump.tok,
      remove ? new List<string> { target.Label } : new List<string> { target.Label, target.Label },
      remove ? new List<Bpl.Block> { target } : new List<Bpl.Block> { target, target });
    Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options));
    Reject(source, options);
  }

  [Fact]
  public void RawGuardReplacementCannotStrengthenTheOriginalBranch() {
    var (source, options) = Source("var x: int; if (x < 0) { assert x < 0; } else { assert true; }");
    var unit = source.Implementations.Single(); var branch = unit.Blocks.First(block => block.Cmds.OfType<Bpl.AssumeCmd>().Any());
    var raw = branch.Cmds.ToList(); raw[0] = new Bpl.AssumeCmd(Bpl.Token.NoToken, Bpl.Expr.False) {
      Attributes = new Bpl.QKeyValue(Bpl.Token.NoToken, "partition", new List<object>(), null)
    }; branch.Cmds = raw;
    Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options)); Reject(source, options);
  }

  [Fact]
  public void GeneratedPrefixPartitionMetadataMustHaveExactShape() {
    var (source, options) = Source("var x: int; if (x < 0) { assert true; }");
    var unit = source.Implementations.Single(); var conditional = (Bpl.IfCmd)unit.StructuredStmts!.BigBlocks[0].ec;
    var prefix = Assert.IsType<Bpl.AssumeCmd>(Assert.Single(conditional.Thn.PrefixCommands));
    prefix.Attributes = new Bpl.QKeyValue(prefix.tok, "partition", new List<object> { Bpl.Expr.True }, null);
    Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options)); Reject(source, options);
  }

  [Fact]
  public void RawReplacedExplicitReturnMustRetainItsOriginalSourceObject() {
    var (source, options) = Source("assert true; return;"); var unit = source.Implementations.Single();
    unit.Blocks[0].TransferCmd = new Bpl.ReturnCmd(Bpl.Token.NoToken);
    Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options)); Reject(source, options);
  }

  [Fact]
  public void ExtraRawBlockCannotBeIgnoredEvenWhenUnreachable() {
    var (source, options) = Source("assert true;"); var unit = source.Implementations.Single();
    unit.Blocks.Add(new Bpl.Block(Bpl.Token.NoToken, "Extra", new List<Bpl.Cmd>(), new Bpl.ReturnCmd(Bpl.Token.NoToken)));
    Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options)); Reject(source, options);
  }
  private static (Bpl.Program Source, DafnyOptions Options) Source(string body) =>
    B3VisibilityTests.Parse("procedure P(); implementation P() { " + body + " }");
  private static void Reject(Bpl.Program source, DafnyOptions options) {
    var unit = source.Implementations.Single();
    Assert.Throws<B3StructuredCfgCorrespondence.Rejection>(() => B3StructuredCfgCorrespondence.Validate(unit));
    var result = B3Normalizer.Normalize(source, unit, options); Assert.False(result.Success);
    Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "b3_cfg_correspondence");
  }
  private static string Emit(Bpl.Program source, DafnyOptions options) {
    using var output = new StringWriter(); using var writer = new Bpl.TokenTextWriter(output, options); source.Emit(writer); return output.ToString();
  }
}
