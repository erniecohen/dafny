using Microsoft.Dafny.Caching;
using Xunit;

namespace Microsoft.Dafny.Test;

public class SmtExecutionLimitsTests {
  [Fact]
  public void ActualOverridesWinAndLookalikesInQuotedDataDoNot() {
    var limits = SmtExecutionLimits.Read(new[] {
      "; (set-option :timeout 100)\n(set-option :rlimit 12)\n(set-option :timeout 0)",
      "(declare-fun |(set-option :timeout 77)| () Bool)",
      "(set-info :source \"(set-option :timeout 66)\")",
      "(assert (and true true))"
    });
    Assert.Equal(new SmtExecutionLimits.Limits(12, 0, true), limits);
    Assert.Equal(new SmtExecutionLimits.Limits(0, 30, true), SmtExecutionLimits.Read(new[] {
      "(set-option :rlimit 12)", "(set-option :timeout 0)", "(set-option :rlimit 0)", "(set-option :timeout 30)"
    }));
  }

  [Theory]
  [InlineData("(set-option :timeout unknown)")]
  [InlineData("(set-option :sat.timeout 10)")]
  [InlineData("(set-option :smt.threads 2)")]
  [InlineData("(set-option :parallel.enable true)")]
  [InlineData("(set-option :smtlib2_log \"solver.log\")")]
  [InlineData("(set-option :unknown.future.option true)")]
  [InlineData("(set-option :smt.random_seed NaN)")]
  [InlineData("(include \"extra.smt2\")")]
  [InlineData("(set-logic ALL) (include \"extra.smt2\")")]
  [InlineData("(set-option :smt.mbqi false) (load-plugin \"plugin\")")]
  [InlineData("(set-option :timeout 0")]
  public void UnrecognizedLimitAndParallelProfilesAreRejected(string command) {
    Assert.False(SmtExecutionLimits.Read(new[] { command }).Supported);
  }

  [Fact]
  public void AuditedScalarOverridesAreAdmitted() {
    Assert.True(SmtExecutionLimits.Read(new[] {
      "(set-option :timeout 0)", "(set-option :smt.random_seed 17)",
      "(set-option :smt.qi.eager_threshold 44.5)", "(set-option :smt.mbqi false)",
      "(set-option :smt.arith.nl false)"
    }).Supported);
  }
}
