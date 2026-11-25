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

    // The results of checks asked of other scopes with this one as the argument, keyed by the scope
    // asked, with both scopes' token counts when it was computed.  A translator asks the scopes of
    // every declaration it meets about scopes of its own, made anew for each module and each
    // request, so the cache lives with the scope asked about: with the declarations' scopes it would
    // keep every translator's scopes, and an entry for each of them, alive.
    private readonly Dictionary<VisibilityScope, (int Count, int OtherCount, bool Overlaps)> cached = new();

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
      // A cache hit must not copy either scope's tokens: this check runs for nearly every
      // declaration the translator meets. Scopes only grow, so a cached overlap stays true and a
      // cached non-overlap is still exact while neither scope's token count has changed.
      int count;
      lock (syncRoot) {
        count = scopeTokens.Count;
      }
      lock (other.syncRoot) {
        if (other.cached.TryGetValue(this, out var result) &&
            (result.Overlaps || (result.Count == count && result.OtherCount == other.scopeTokens.Count))) {
          return result.Overlaps;
        }
      }
      var otherTokens = other.Tokens();
      bool overlaps;
      lock (syncRoot) {
        count = scopeTokens.Count;
        overlaps = scopeTokens.Overlaps(otherTokens);
      }
      lock (other.syncRoot) {
        other.cached[this] = (count, otherTokens.Length, overlaps);
      }
      return overlaps;
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
        return scopeTokens.Count != count;
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