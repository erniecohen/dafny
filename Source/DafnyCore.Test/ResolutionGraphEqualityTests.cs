using Microsoft.Dafny;
using Xunit;

namespace DafnyCore.Test;

public class ResolutionGraphEqualityTests {
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
}
