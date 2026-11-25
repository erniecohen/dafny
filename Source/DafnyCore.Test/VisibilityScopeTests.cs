using System;
using System.Runtime.CompilerServices;
using Microsoft.Dafny;

namespace DafnyCore.Test;

/// <summary>
/// <see cref="VisibilityScope.VisibleInScope"/> caches its results.  The cache must stay exact as either
/// scope grows, and must not keep the scope asked about alive: a translator asks the scopes of the
/// declarations it meets about scopes of its own, made anew for each module and each request.
/// </summary>
public class VisibilityScopeTests {
  [Fact]
  public void ACachedResultFollowsTheGrowthOfEitherScope() {
    var declaration = new VisibilityScope("A");
    var asked = new VisibilityScope();
    Assert.False(declaration.VisibleInScope(asked));
    Assert.False(declaration.VisibleInScope(asked), "a cached non-overlap");
    Assert.True(asked.Augment(declaration));
    Assert.True(declaration.VisibleInScope(asked), "the scope asked about grew");

    var other = new VisibilityScope("B");
    var growing = new VisibilityScope();
    Assert.False(growing.VisibleInScope(other));
    Assert.True(growing.Augment(other));
    Assert.True(growing.VisibleInScope(other), "the scope asked grew");
    Assert.False(growing.Augment(other), "nothing new");
    Assert.True(growing.VisibleInScope(other));

    Assert.True(declaration.VisibleInScope(null));
    Assert.False(new VisibilityScope().VisibleInScope(new VisibilityScope()), "empty scopes do not overlap");
  }

  // The scope asked about is made, used and dropped in a method of its own, so that no local of the
  // test keeps it alive.
  [MethodImpl(MethodImplOptions.NoInlining)]
  private static WeakReference AskAndForget(VisibilityScope declaration) {
    var asked = new VisibilityScope();
    asked.Augment(declaration);
    Assert.True(declaration.VisibleInScope(asked));
    var unrelated = new VisibilityScope("C");
    Assert.False(declaration.VisibleInScope(unrelated));
    return new WeakReference(unrelated);
  }

  [Fact]
  public void TheCacheDoesNotKeepTheScopeAskedAboutAlive() {
    var declaration = new VisibilityScope("D");
    var references = new[] { AskAndForget(declaration), AskAndForget(declaration) };
    for (var i = 0; i < 10 && Array.Exists(references, r => r.IsAlive); i++) {
      GC.Collect();
      GC.WaitForPendingFinalizers();
    }
    Assert.All(references, r => Assert.False(r.IsAlive, "a declaration's scope keeps a scope it was asked about"));
    GC.KeepAlive(declaration);
  }
}
