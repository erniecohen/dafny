using Microsoft.Dafny;

namespace DafnyCore.Test;

/// <summary>
/// The spellings of a prover option that select Z3's int-blasting bit-vector solver, which can prove
/// false assertions (https://github.com/erniecohen/dafny/issues/618), and the ones that do not.
/// </summary>
public class UnsoundSolverOptionsTest {
  [Theory]
  [InlineData("O:smt.bv.solver=2")]
  [InlineData("smt.bv.solver=2")]
  [InlineData("o:smt.bv.solver=2")]
  [InlineData("O:SMT.BV.SOLVER=2")]
  [InlineData("O:Smt.Bv.Solver=2")]
  [InlineData(" O:smt.bv.solver = 2 ")]
  [InlineData("O::smt.bv.solver=2")]
  [InlineData("O:smt.bv.solver=02")]
  [InlineData("O:smt.bv.solver=\"2\"")]
  [InlineData("O:smt.bv.solver=2.0")]
  [InlineData("O:smt.bv.solver=#x2")]
  [InlineData("O:smt.bv.solver=#X02")]
  [InlineData("O:smt.bv.solver=#b10")]
  [InlineData("O:smt.bv.solver=two")]
  [InlineData("O:smt.bv.solver=")]
  [InlineData("O:smt.bv.solver=-2")]
  [InlineData("O:smt.bv.solver=2.5")]
  public void SelectsTheIntBlastingSolver(string option) {
    Assert.True(UnsoundSolverOptions.SelectsIntBlastingBvSolver(option), option);
  }

  [Theory]
  [InlineData("O:smt.bv.solver=0")]
  [InlineData("O:smt.bv.solver=1")]
  [InlineData("O:smt.bv.solver=3")]
  [InlineData("O:smt.bv.solver=12")]
  [InlineData("O:smt.bv.solver=20")]
  [InlineData("O:smt.bv.solver=#x12")]
  [InlineData("O:smt.bv.solver=#b1")]
  [InlineData("O:smt.bv.solver=1.0")]
  [InlineData("O:smt.arith.solver=2")]
  [InlineData("O:sat.bv.solver=2")]
  [InlineData("O:smt.bv.solvers=2")]
  [InlineData("O:smt.bv.enable_int2bv=true")]
  [InlineData("PROVER_PATH=/z3/smt.bv.solver=2")]
  [InlineData("SOLVER=z3")]
  [InlineData("O:smt.bv.solver")]
  public void DoesNotSelectTheIntBlastingSolver(string option) {
    Assert.False(UnsoundSolverOptions.SelectsIntBlastingBvSolver(option), option);
  }

  [Fact]
  public void FindsEachOffendingOptionOnce() {
    var found = UnsoundSolverOptions.Find(["O:smt.arith.solver=2", "O:smt.bv.solver=2", "O:smt.bv.solver=2", "O:smt.bv.solver=1"]);
    Assert.Equal(["O:smt.bv.solver=2"], found);
  }
}
