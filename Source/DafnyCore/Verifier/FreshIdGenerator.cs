using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Microsoft.Dafny {
  public abstract class FreshIdGenerator {
    private readonly bool moduleLocal;
    private FreshIdGenerator Scoped => !moduleLocal && ModuleNamingScope.Current is { } scope
      ? scope.Generator(this, () => new ModuleGenerator(commonPrefix, scope.Prefix)) : null;

    private readonly string modulePrefix = "";

    private sealed class ModuleGenerator : FreshIdGenerator {
      public ModuleGenerator(string prefix, string modulePrefix) : base(prefix, true, modulePrefix) {
      }
    }

    string tipString;  // a string representation of Tip
    int tipChildrenCount = 0;
    readonly Stack<Dictionary<string, int>> prefixToCountStack = new();  // invariant PrefixToCount_Stack.Count == Tip.Count

    public /*spec public*/ readonly Stack<int> Tip = new();

    public void Push() {
      if (Scoped is { } scoped) {
        scoped.Push();
        return;
      }
      Tip.Push(tipChildrenCount);
      tipChildrenCount = 0;
      tipString = ComputeTipString();
      prefixToCountStack.Push(new());
    }

    public void Pop() {
      if (Scoped is { } scoped) {
        scoped.Pop();
        return;
      }
      Contract.Requires(Tip.Count > 0);
      int k = Tip.Pop();
      tipChildrenCount = k + 1;
      tipString = ComputeTipString();
      prefixToCountStack.Pop();
    }

    string ComputeTipString() {
      return Tip.Any() ? string.Join('_', Tip.Reverse().Select(t => t.ToString())) : null;
    }

    readonly string commonPrefix = "";

    public FreshIdGenerator() {
      prefixToCountStack.Push(new());
    }

    private FreshIdGenerator(string commonPrefix, bool moduleLocal, string modulePrefix) : this(commonPrefix) {
      this.moduleLocal = moduleLocal;
      this.modulePrefix = modulePrefix;
    }

    protected FreshIdGenerator(string commonPrefix) : this() {
      this.commonPrefix = commonPrefix;
    }

    public void Reset() {
      if (Scoped is { } scoped) {
        scoped.Reset();
        return;
      }
      var prefixToCount = prefixToCountStack.Peek();
      lock (prefixToCount) {
        prefixToCount.Clear();
      }
    }

    public string FreshId(string prefix) {
      if (Scoped is { } scoped) {
        return scoped.FreshId(prefix);
      }
      // Several generated-name prefixes carry meaning (for example, match
      // wildcards start with '_'). Preserve those prefixes when qualifying names.
      return commonPrefix + prefix + modulePrefix + FreshNumericId(prefix);
    }

    public virtual string FreshNumericId(string prefix = "") {
      if (Scoped is { } scoped) {
        return scoped.FreshNumericId(prefix);
      }
      var prefixToCount = prefixToCountStack.Peek();
      lock (prefixToCount) {
        if (!prefixToCount.TryGetValue(prefix, out var old)) {
          old = 0;
        }
        prefixToCount[prefix] = old + 1;
        return tipString == null ? old.ToString() : tipString + "_" + old.ToString();
      }
    }

    public override string ToString() {
      throw new InvalidOperationException("Did not expect to convert the fresh Id generator itself to a string");
    }
  }

  public class CodeGenIdGenerator : FreshIdGenerator {
  }

  public class VerificationIdGenerator : FreshIdGenerator {
    public VerificationIdGenerator() {
    }
    public VerificationIdGenerator(string commonPrefix) : base(commonPrefix) {
    }
    public VerificationIdGenerator NestedFreshIdGenerator(string prefix) {
      return new(FreshId(prefix));
    }
  }
}
