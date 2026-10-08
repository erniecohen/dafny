using DafnyCore.Verifier;
using Microsoft.Dafny;
using Bpl = Microsoft.Boogie;

namespace DafnyCore.Test.Verification;

public class ObligationCertifiedPreparationTests {
  private static Bpl.IdentifierExpr Id(string name) => new(Bpl.Token.NoToken, name, Bpl.Type.Int);
  private static Bpl.StmtList Statements(params Bpl.Cmd[] commands) {
    var builder = new Bpl.StmtListBuilder();
    foreach (var command in commands) { builder.Add(command); }
    return builder.Collect(Bpl.Token.NoToken);
  }
  private static Bpl.IfCmd Branch(Bpl.Expr guard, params Bpl.Cmd[] commands) =>
    new(Bpl.Token.NoToken, guard, Statements(commands), null, null);

  [Fact]
  public void PrivateArgumentBindingKeepsTheOriginalGuardOnItsFact() {
    var guard = Bpl.Expr.Gt(Id("x"), Bpl.Expr.Literal(0));
    var value = Bpl.Expr.Add(Id("x"), Bpl.Expr.Literal(1));
    var assignment = Bpl.Cmd.SimpleAssign(Bpl.Token.NoToken, Id("arg"), value);
    var fact = Bpl.Expr.Gt(Id("arg"), Bpl.Expr.Literal(0));
    var attributes = new Bpl.QKeyValue(Bpl.Token.NoToken, "id", ["support"], null);
    List<object> commands = [Branch(guard, assignment, new Bpl.AssumeCmd(Bpl.Token.NoToken, fact, attributes))];
    var normalized = CertifiedContractPreparation.Normalize(commands, new HashSet<string> { "arg" });
    Assert.Same(assignment, normalized[0]);
    var assumption = Assert.IsType<Bpl.AssumeCmd>(normalized[1]);
    Assert.Equal(Bpl.Expr.Imp(guard, fact).ToString(), assumption.Expr.ToString());
    Assert.Same(attributes, assumption.Attributes);
  }

  [Fact]
  public void ASourceVariableAssignmentKeepsTheOriginalBranch() {
    List<object> commands = [Branch(Bpl.Expr.True,
      Bpl.Cmd.SimpleAssign(Bpl.Token.NoToken, Id("source"), Bpl.Expr.Literal(0)))];
    Assert.Same(commands, CertifiedContractPreparation.Normalize(commands, new HashSet<string> { "arg" }));
  }

  [Fact]
  public void RepeatedArgumentAssignmentsKeepTheOriginalBranch() {
    List<object> commands = [Branch(Bpl.Expr.True,
      Bpl.Cmd.SimpleAssign(Bpl.Token.NoToken, Id("arg"), Bpl.Expr.Literal(0)),
      Bpl.Cmd.SimpleAssign(Bpl.Token.NoToken, Id("arg"), Bpl.Expr.Literal(1)))];
    Assert.Same(commands, CertifiedContractPreparation.Normalize(commands, new HashSet<string> { "arg" }));
  }

  [Fact]
  public void AGuardReadingAnAssignedArgumentKeepsTheOriginalBranch() {
    List<object> commands = [Branch(Bpl.Expr.Eq(Id("arg"), Bpl.Expr.Literal(0)),
      Bpl.Cmd.SimpleAssign(Bpl.Token.NoToken, Id("arg"), Bpl.Expr.Literal(1)))];
    Assert.Same(commands, CertifiedContractPreparation.Normalize(commands, new HashSet<string> { "arg" }));
  }

  [Fact]
  public void ARealProofCheckKeepsTheOriginalBranch() {
    List<object> commands = [Branch(Bpl.Expr.True, new Bpl.AssertCmd(Bpl.Token.NoToken, Bpl.Expr.False))];
    Assert.Same(commands, CertifiedContractPreparation.Normalize(commands, new HashSet<string>()));
  }

  [Fact]
  public void AnEmptyBranchRetainsItsGuardExpression() {
    var guard = Bpl.Expr.Gt(Id("x"), Bpl.Expr.Literal(0));
    List<object> commands = [Branch(guard)];
    var normalized = CertifiedContractPreparation.Normalize(commands, new HashSet<string>());
    Assert.Same(guard, Assert.IsType<Bpl.IfCmd>(Assert.Single(normalized)).Guard);
  }

  [Fact]
  public void UnchangedPreparationKeepsLabelOrigins() {
    var context = new BodyTranslationContext(false);
    var source = new BoogieStmtListBuilder(null, null, context);
    var origin = new Token(3, 4);
    source.Add(new Bpl.AssumeCmd(Bpl.Token.NoToken, Bpl.Expr.True));
    source.AddLabelCmd(origin, "after");
    var target = new BoogieStmtListBuilder(null, null, context);
    target.AppendAlreadyTranslated(source,
      CertifiedContractPreparation.Normalize(source.Commands, new HashSet<string>()));
    Assert.Same(origin, target.Collect(Bpl.Token.NoToken).BigBlocks[0].tok);
  }
}
