using Microsoft.Dafny;
using Type = Microsoft.Dafny.Type;

namespace DafnyCore.Test;

// Allocation checks warm a process-wide reusable buffer. Other test collections
// must not borrow or replace that buffer while its allocation count is measured.
[Collection("Numeric ancestry allocation")]
public class NumericAncestryAllocationTests : NumericAncestryTestFixture {
  [Fact]
  public void ScalarClassificationDoesNotAllocateTraversalState() {
    // Warm the normalization paths before measuring this thread's allocations.
    AssertClassification(Type.Int, Type.NumericAncestryKind.Integer);
    AssertClassification(Type.Real, Type.NumericAncestryKind.Real);
    var singleNewtype = Application(Newtype("N", Type.Int));
    AssertClassification(singleNewtype, Type.NumericAncestryKind.Integer);
    var before = GC.GetAllocatedBytesForCurrentThread();
    var numericCount = 0;
    for (var i = 0; i < 1000; i++) {
      numericCount += Type.Int.IsNumericBased() ? 1 : 0;
      numericCount += Type.Real.IsNumericBased(Type.NumericPersuasion.Real) ? 1 : 0;
      numericCount += singleNewtype.IsNumericBased() ? 1 : 0;
    }
    var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    Assert.Equal(3000, numericCount);
    Assert.Equal(0L, allocated);
  }

  [Fact]
  public void WarmLongWalksReuseEmptyStorageWithoutTraversalAllocations() {
    var integer = Chain("Integer", Type.Int, 512).Type;
    var real = Chain("Real", Type.Real, 512).Type;
    AssertClassification(integer, Type.NumericAncestryKind.Integer);
    AssertClassification(real, Type.NumericAncestryKind.Real);
    var before = GC.GetAllocatedBytesForCurrentThread();
    var numericCount = 0;
    for (var i = 0; i < 100; i++) {
      numericCount += integer.IsNumericBased() ? 1 : 0;
      numericCount += real.IsNumericBased(Type.NumericPersuasion.Real) ? 1 : 0;
    }
    var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    Assert.Equal(200, numericCount);
    Assert.Equal(0L, allocated);
  }

  [Fact]
  public void SubstitutionFailureReturnsClearedWalkStorage() {
    var (type, baseDeclaration) = Chain("Failing", Type.Int, 64);
    AssertClassification(type, Type.NumericAncestryKind.Integer);
    var t = Parameter("T");
    var failing = Newtype("Throw", new ThrowingSubstitutionType(), t);
    baseDeclaration.BaseType = Application(failing, Type.Int);
    Assert.Throws<InvalidOperationException>(() => type.ClassifyNumericAncestry());
    baseDeclaration.BaseType = Type.Int;
    var before = GC.GetAllocatedBytesForCurrentThread();
    var ancestry = type.ClassifyNumericAncestry();
    var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    Assert.Equal(Type.NumericAncestryKind.Integer, ancestry.Kind);
    Assert.Equal(0L, allocated);
  }
}

[CollectionDefinition("Numeric ancestry allocation", DisableParallelization = true)]
public class NumericAncestryAllocationCollection { }
