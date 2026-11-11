using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

public sealed record OperatorDescriptor(string Symbol, int Arity);

public static class OperatorResolution {
  public static bool SupportedOwner(TopLevelDecl owner) => owner is ClassDecl or IndDatatypeDecl;

  public static void Validate(IEnumerable<TopLevelDecl> declarations, ModuleResolver resolver) {
    foreach (var owner in declarations) {
      var slots = new HashSet<OperatorDescriptor>();
      var hosts = new List<Declaration> { owner };
      if (owner is TopLevelDeclWithMembers members) {
        hosts.AddRange(members.Members);
      }
      foreach (var host in hosts) {
        var attributes = host.Attributes?.AsEnumerable().Where(a => a.Name == "operator").ToList();
        if (attributes == null || attributes.Count == 0) {
          continue;
        }
        void Error(string message) => resolver.Reporter.Error(MessageSource.Resolver, "operator_declaration", host.Origin, message);
        if (attributes.Count != 1 || attributes[0].Args.Count != 1 || attributes[0].Args[0] is not StringLiteralExpr) {
          Error("operator requires exactly one literal symbol argument; duplicate attributes and override mode are not supported");
          continue;
        }
        var symbol = (string)((StringLiteralExpr)attributes[0].Args[0]).Value;
        if (symbol is "==" or "!=") {
          Error($"operator '{symbol}' cannot be declared; use a named equivalence predicate");
          continue;
        }
        if (symbol is not ("+" or "-" or "*" or "/" or "%" or "<" or "<=" or ">" or ">=")) {
          Error($"operator '{symbol}' is not supported in this milestone");
          continue;
        }
        if (!SupportedOwner(owner) || host is not Function f || f is TwoStateFunction or ExtremePredicate ||
            f.RefinementBase != null || f.OverriddenMember != null) {
          Error("operators require directly declared ordinary functions/predicates on concrete classes or inductive datatypes");
          continue;
        }
        var arity = f.Ins.Count + (f.IsStatic ? 0 : 1);
        if (arity != 2 && !(symbol == "-" && arity == 1)) {
          Error($"operator '{symbol}' has invalid operand arity");
          continue;
        }
        if (f.TypeArgs.Count != 0 || f.Ins.Any(p => p.DefaultValue != null || p.IsNameOnly)) {
          Error("operator members cannot have method type parameters or default/nameonly operands");
          continue;
        }
        if (symbol is "<" or "<=" or ">" or ">=" && !f.ResultType.IsBoolType) {
          Error($"operator '{symbol}' must return bool");
          continue;
        }
        if (f.IsStatic && (f.Ins[0].PreType.NormalizeWrtScope() is not DPreType first ||
            !ReferenceEquals(first.Decl, owner) || first.Arguments.Count != owner.TypeArgs.Count ||
            !first.Arguments.Zip(owner.TypeArgs).All(p => p.First.Normalize() is DPreType d && ReferenceEquals(d.Decl, p.Second)))) {
          Error("a static operator's first formal must have its declaring owner's instantiated nominal type");
          continue;
        }
        var descriptor = new OperatorDescriptor(symbol, arity);
        if (!slots.Add(descriptor)) {
          Error($"operator '{symbol}' has ambiguous slots on '{owner.Name}'");
          continue;
        }
        f.Operator = descriptor;
      }
    }
  }

  // Transitive enabled-source scan: refinement merging precedes descriptor validation.
  // Include dependency signatures and nested views, including refinements which introduce
  // the first descriptor into a previously intrinsic base.
  public static bool FeatureBearing(ModuleDefinition module, HashSet<ModuleDefinition> visited = null) {
    visited ??= new();
    if (module == null || !visited.Add(module)) {
      return false;
    }
    if (module.Implements?.Target.Def is { } target && FeatureBearing(target, visited)) {
      return true;
    }
    foreach (var declaration in module.TopLevelDecls) {
      if (Attributes.Contains(declaration.Attributes, "operator")) {
        return true;
      }
      if (declaration is TopLevelDeclWithMembers members &&
          members.Members.Any(m => m.Operator != null || Attributes.Contains(m.Attributes, "operator"))) {
        return true;
      }
      if (declaration is LiteralModuleDecl literal && FeatureBearing(literal.ModuleDef, visited) ||
          declaration is ModuleDecl import && FeatureBearing(import.Signature?.ModuleDef, visited)) {
        return true;
      }
    }
    return false;
  }
}
