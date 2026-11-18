using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Microsoft.Dafny {
  /// <summary>
  /// A scope consists of a set of identifiers.
  /// Two scopes overlap if the intersections of their identifiers is not empty
  /// </summary>
  public class VisibilityScope {
    private static long maxScopeId = -1;
    private readonly object syncRoot = new();

    private readonly SortedSet<uint> scopeTokens = [];

    // Only for debugging
    private readonly SortedSet<string> scopeIds = [];

    private static bool Overlaps(SortedSet<uint> set1, SortedSet<uint> set2) {
      // This conditional implements a performance optimization,
      // since there is an iteration over the second argument to Overlaps
      if (set1.Count < set2.Count) {
        return set2.Overlaps(set1);
      } else {
        return set1.Overlaps(set2);
      }
    }

    private Dictionary<VisibilityScope, Tuple<int, bool>> cached = new();

    // By convention, the "null" scope sees all
    private uint[] Tokens() {
      lock (syncRoot) {
        return System.Linq.Enumerable.ToArray(scopeTokens);
      }
    }

    public bool VisibleInScope(VisibilityScope other) {
      if (other == null) {
        return true;
      }
      // A cache hit must not copy the other scope's tokens: this check runs for nearly every
      // declaration the translator meets. Scopes only grow, and growing this scope clears its
      // cache, so a cached overlap stays true and a cached non-overlap is still exact while the
      // other scope's token count is unchanged.
      int otherCount;
      lock (other.syncRoot) {
        otherCount = other.scopeTokens.Count;
      }
      lock (syncRoot) {
        if (cached.TryGetValue(other, out var result) && (result.Item2 || result.Item1 == otherCount)) {
          return result.Item2;
        }
      }
      var otherTokens = other.Tokens();
      lock (syncRoot) {
        var overlaps = scopeTokens.Overlaps(otherTokens);
        cached[other] = new Tuple<int, bool>(otherTokens.Length, overlaps);
        return overlaps;
      }
    }

    [Pure]
    public bool IsEmpty() {
      lock (syncRoot) {
        return scopeTokens.Count == 0;
      }
    }

    /// <summary>
    /// Adds the scopes of "other" to this scope.  Returns whether this scope changed.
    /// </summary>
    public bool Augment(VisibilityScope other) {
      if (other == null || ReferenceEquals(this, other)) {
        return false;
      }
      var tokens = other.Tokens();
      string[] ids;
      lock (other.syncRoot) {
        ids = System.Linq.Enumerable.ToArray(other.scopeIds);
      }
      lock (syncRoot) {
        var count = scopeTokens.Count;
        scopeTokens.UnionWith(tokens);
        scopeIds.UnionWith(ids);
        if (scopeTokens.Count == count) {
          return false;
        }
        cached.Clear();
        return true;
      }
    }

    public VisibilityScope(string name) {
      var id = System.Threading.Interlocked.Increment(ref maxScopeId);
      scopeTokens.Add(checked((uint)id));
      scopeIds.Add(name);
    }

    public VisibilityScope() {
    }
  }
}