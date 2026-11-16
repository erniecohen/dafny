using System.Runtime;
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
    var numericCount = 0;
    AssertWarmWindowAllocatesNothing(() => {
      for (var i = 0; i < 1000; i++) {
        numericCount += Type.Int.IsNumericBased() ? 1 : 0;
        numericCount += Type.Real.IsNumericBased(Type.NumericPersuasion.Real) ? 1 : 0;
        numericCount += singleNewtype.IsNumericBased() ? 1 : 0;
      }
    }, onWindowStart: () => numericCount = 0);
    Assert.Equal(3000, numericCount);
  }

  [Fact]
  public void WarmLongWalksReuseEmptyStorageWithoutTraversalAllocations() {
    var integer = Chain("Integer", Type.Int, 512).Type;
    var real = Chain("Real", Type.Real, 512).Type;
    AssertClassification(integer, Type.NumericAncestryKind.Integer);
    AssertClassification(real, Type.NumericAncestryKind.Real);
    var numericCount = 0;
    AssertWarmWindowAllocatesNothing(() => {
      for (var i = 0; i < 100; i++) {
        numericCount += integer.IsNumericBased() ? 1 : 0;
        numericCount += real.IsNumericBased(Type.NumericPersuasion.Real) ? 1 : 0;
      }
    }, onWindowStart: () => numericCount = 0);
    Assert.Equal(200, numericCount);
  }

  [Fact]
  public void SubstitutionFailureReturnsClearedWalkStorage() {
    var (type, baseDeclaration) = Chain("Failing", Type.Int, 64);
    AssertClassification(type, Type.NumericAncestryKind.Integer);
    var t = Parameter("T");
    var failing = Newtype("Throw", new ThrowingSubstitutionType(), t);
    var ancestry = default(Type.NumericAncestry);
    // Each window measures the first walk after a failed one, so the failure is repeated, unmeasured,
    // before every window. A failure that dropped the shared storage would make every window allocate.
    AssertWarmWindowAllocatesNothing(() => ancestry = type.ClassifyNumericAncestry(), onWindowStart: () => {
      baseDeclaration.BaseType = Application(failing, Type.Int);
      Assert.Throws<InvalidOperationException>(() => type.ClassifyNumericAncestry());
      baseDeclaration.BaseType = Type.Int;
    });
    Assert.Equal(Type.NumericAncestryKind.Integer, ancestry.Kind);
  }

  // The two tests below check the measurement itself: it must still charge an allocation that the
  // measured code makes on every run, and it must ignore one that happens in a single window only.

  [Fact]
  public void WarmWindowMeasurementChargesAnAllocationMadeOnEveryRun() {
    object kept = null;
    var windows = MeasureWarmWindows(() => kept = new object[4]);
    GC.KeepAlive(kept);
    Assert.Equal(MaximumWindows, windows.Count);
    Assert.All(windows, window => Assert.True(window.AllocatedBytes > 0, Describe(windows)));
  }

  [Fact]
  public void WarmWindowMeasurementIgnoresAnAllocationInOneWindowOnly() {
    var runs = 0;
    object kept = null;
    var windows = MeasureWarmWindows(() => {
      // Run 1 is the unmeasured warm-up; run 2 is the first measured window.
      if (++runs == 2) {
        kept = new object[4];
      }
    });
    GC.KeepAlive(kept);
    Assert.True(windows.Count >= 2, Describe(windows));
    Assert.True(windows[0].AllocatedBytes > 0, Describe(windows));
    Assert.Equal(0L, windows[^1].AllocatedBytes);
  }

  private const int MaximumWindows = 10;

  private readonly record struct Window(long AllocatedBytes, int JitCompilations);

  /// <summary>
  /// Asserts that <paramref name="window"/>, once warm, allocates nothing on this thread.
  ///
  /// <see cref="GC.GetAllocatedBytesForCurrentThread"/> also counts what the runtime allocates on this
  /// thread, and the runtime keeps recompiling hot code after a warm-up: tier-1 promotion, on-stack
  /// replacement of a running loop, profile-guided recompilation. Those allocations happen a bounded
  /// number of times in a process, at times that depend on timing, so a single measured window
  /// can be charged for one (https://github.com/erniecohen/dafny/issues/394). An allocation made by
  /// the measured code itself recurs in every window. So the window runs once unmeasured, then is
  /// measured up to <see cref="MaximumWindows"/> times, and the assertion holds when one of those
  /// identical windows allocates nothing.
  /// </summary>
  private static void AssertWarmWindowAllocatesNothing(Action window, Action onWindowStart = null) {
    var windows = MeasureWarmWindows(window, onWindowStart);
    Assert.True(windows[^1].AllocatedBytes == 0, Describe(windows));
  }

  /// <summary>
  /// Runs <paramref name="onWindowStart"/> and then <paramref name="window"/> once unmeasured, then up to
  /// <see cref="MaximumWindows"/> more times, measuring only <paramref name="window"/>, and stops after
  /// the first window that allocates nothing.
  /// </summary>
  private static List<Window> MeasureWarmWindows(Action window, Action onWindowStart = null) {
    onWindowStart?.Invoke();
    window();
    var windows = new List<Window>(MaximumWindows);
    while (windows.Count < MaximumWindows) {
      onWindowStart?.Invoke();
      var jitBefore = JitInfo.GetCompiledMethodCount(currentThread: true);
      var before = GC.GetAllocatedBytesForCurrentThread();
      window();
      var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
      var jit = (int)(JitInfo.GetCompiledMethodCount(currentThread: true) - jitBefore);
      windows.Add(new Window(allocated, jit));
      if (allocated == 0) {
        break;
      }
    }
    return windows;
  }

  private static string Describe(List<Window> windows) =>
    "Allocated bytes (JIT compilations on this thread) in each warm window: " +
    string.Join(", ", windows.Select(w => $"{w.AllocatedBytes} ({w.JitCompilations})"));
}

[CollectionDefinition("Numeric ancestry allocation", DisableParallelization = true)]
public class NumericAncestryAllocationCollection { }
