using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Microsoft.Dafny;

public class Scope<Thing> where Thing : class {
  private DafnyOptions options;
  [Rep]
  readonly List<string> names = [];  // a null means a marker
  [Rep]
  readonly List<Thing> things = [];
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(names != null);
    Contract.Invariant(things != null);
    Contract.Invariant(names.Count == things.Count);
    Contract.Invariant(-1 <= scopeSizeWhereInstancesWereDisallowed && scopeSizeWhereInstancesWereDisallowed <= names.Count);
  }

  int scopeSizeWhereInstancesWereDisallowed = -1;

  public Scope(DafnyOptions options) {
    this.options = options;
  }

  public List<string> Names => names;

  public bool AllowInstance {
    get { return scopeSizeWhereInstancesWereDisallowed == -1; }
    set {
      Contract.Assume(AllowInstance && !value);  // only allowed to change from true to false (that's all that's currently needed in Dafny); Pop is what can make the change in the other direction
      scopeSizeWhereInstancesWereDisallowed = names.Count;
    }
  }

  /// <summary>
  /// A boundary for restoring scopes after an aborted resolution phase. In addition to names,
  /// it saves instance availability, which cannot be restored through AllowInstance's setter.
  /// </summary>
  public readonly struct Checkpoint {
    internal readonly Scope<Thing> Owner;
    internal readonly int Count;
    internal readonly int InstanceBoundary;

    internal Checkpoint(Scope<Thing> owner, int count, int instanceBoundary) {
      Owner = owner;
      Count = count;
      InstanceBoundary = instanceBoundary;
    }
  }

  public Checkpoint CreateCheckpoint() {
    return new Checkpoint(this, names.Count, scopeSizeWhereInstancesWereDisallowed);
  }

  public void RestoreCheckpoint(Checkpoint checkpoint) {
    Contract.Requires(ReferenceEquals(checkpoint.Owner, this));
    Contract.Requires(checkpoint.Count <= names.Count);
    names.RemoveRange(checkpoint.Count, names.Count - checkpoint.Count);
    things.RemoveRange(checkpoint.Count, things.Count - checkpoint.Count);
    scopeSizeWhereInstancesWereDisallowed = checkpoint.InstanceBoundary;
  }

  public void PushMarker() {
    names.Add(null);
    things.Add(null);
  }

  public void PopMarker() {
    int n = names.Count;
    while (true) {
      n--;
      if (names[n] == null) {
        break;
      }
    }
    names.RemoveRange(n, names.Count - n);
    things.RemoveRange(n, things.Count - n);
    if (names.Count < scopeSizeWhereInstancesWereDisallowed) {
      scopeSizeWhereInstancesWereDisallowed = -1;
    }
  }

  public enum PushResult { Duplicate, Shadow, Success }

  /// <summary>
  /// Pushes name-->thing association and returns "Success", if name has not already been pushed since the last marker.
  /// If name already has been pushed since the last marker, does nothing and returns "Duplicate".
  /// If the appropriate command-line option is supplied, then this method will also check if "name" shadows a previous
  /// name; if it does, then it will return "Shadow" instead of "Success".
  /// </summary>
  public PushResult Push(string name, Thing thing) {
    Contract.Requires(name != null);
    Contract.Requires(thing != null);
    if (Find(name, true) != null) {
      return PushResult.Duplicate;
    } else {
      var r = PushResult.Success;
      if (options.WarnShadowing && Find(name, false) != null) {
        r = PushResult.Shadow;
      }
      names.Add(name);
      things.Add(thing);
      return r;
    }
  }

  Thing Find(string name, bool topScopeOnly) {
    Contract.Requires(name != null);
    for (int n = names.Count; 0 <= --n;) {
      if (names[n] == null) {
        if (topScopeOnly) {
          return null;  // not present
        }
      } else if (names[n] == name) {
        Thing t = things[n];
        Contract.Assert(t != null);
        return t;
      }
    }
    return null;  // not present
  }

  public Thing Find(string name) {
    Contract.Requires(name != null);
    return Find(name, false);
  }

  public Thing FindInCurrentScope(string name) {
    Contract.Requires(name != null);
    return Find(name, true);
  }

  public bool ContainsDecl(Thing t) {
    return things.Exists(thing => thing == t);
  }
}