using Microsoft.Dafny;
using Xunit;

namespace DafnyCore.Test;

public class ResolutionGraphEqualityTests {
  private static readonly System.Reflection.FieldInfo QuantifierId = typeof(QuantifierExpr)
    .GetField("UniqueId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

  [Fact]
  public void PreservesNestedValuePaths() {
    var expected = new List<object> { new List<string> { "same", "expected" } };
    var actual = new List<object> { new List<string> { "same", "actual" } };
    Assert.Equal("root/0/1: value expected != actual", new ResolutionGraphEquality(_ => false).Difference(expected, actual));
  }

  [Fact]
  public void RejectsSplitAndMergedIdentity() {
    var shared = new List<string> { "same" };
    var one = new List<object> { shared, shared };
    var two = new List<object> { new List<string> { "same" }, new List<string> { "same" } };
    Assert.Equal("root/1: split identity", new ResolutionGraphEquality(_ => false).Difference(one, two));
    Assert.Equal("root/1: merged identity", new ResolutionGraphEquality(_ => false).Difference(two, one));
  }

  [Fact]
  public void HandlesCyclesAndDeepPaths() {
    var expected = new List<object>();
    var actual = new List<object>();
    expected.Add(expected);
    actual.Add(actual);
    Assert.Null(new ResolutionGraphEquality(_ => false).Difference(expected, actual));
    object left = "left", right = "right";
    for (var i = 0; i < 300; i++) { left = new List<object> { left }; right = new List<object> { right }; }
    Assert.Equal("root" + string.Concat(Enumerable.Repeat("/0", 300)) + ": value left != right",
      new ResolutionGraphEquality(_ => false).Difference(left, right));
  }

  private static ForallExpr Quantifier(bool value = true) => new(Token.NoToken, [], null,
    new LiteralExpr(Token.NoToken, value));

  [Fact]
  public void QuantifierIdsMayBeConsistentlyRenamed() {
    var expected = new[] { Quantifier(), Quantifier() };
    var actual = new[] { Quantifier(), Quantifier() };
    Assert.NotEqual(expected[0].FullName, actual[0].FullName);
    Assert.Null(new ResolutionGraphEquality(_ => false).Difference(expected, actual));
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void QuantifierIdRenamingRejectsCollisions(bool reverse) {
    var distinct = new[] { Quantifier(), Quantifier() };
    var collision = new[] { Quantifier(), Quantifier() };
    QuantifierId.SetValue(collision[1], QuantifierId.GetValue(collision[0]));
    var difference = new ResolutionGraphEquality(_ => false).Difference(
      reverse ? collision : distinct, reverse ? distinct : collision);
    Assert.Contains(reverse ? "split name" : "merged name", difference);
  }

  [Fact]
  public void QuantifierIdRenamingStillComparesContents() {
    Assert.Contains("Term", new ResolutionGraphEquality(_ => false).Difference(Quantifier(), Quantifier(false)));
    Assert.NotNull(new ResolutionGraphEquality(_ => false).Difference(new[] { 1, 2 }, new[] { 3, 4 }));
  }
}
