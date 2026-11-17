#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// Issue 410: a refining module must not change what a name in inherited code refers to.
///
/// The refinement transformer copies code from the base module B into the refining module R,
/// where it is resolved again in R's scope. A declaration that R adds, or a module that R binds to
/// an abstract import, can then capture a name, so that the copy means something B never
/// verified. This check implements the exact rule of PR 401 (§4.3): every inherited occurrence
/// must resolve in R to the counterpart of what it resolved to in B, where the counterpart of
/// - a declaration of B (or of a module nested in B) is its copy or refinement in R;
/// - a local variable, bound variable or parameter is its copy;
/// - a declaration reached through an import that R keeps is that same declaration;
/// - a declaration reached through an abstract import that R binds is the declaration of the
///   same name in the bound module, which must refine the import's target;
/// - a built-in is itself;
/// - a member is the member of the same name in the counterpart of its enclosing type, or a
///   member that overrides it.
/// A pattern identifier must also keep its kind: a variable stays a variable, and a constructor
/// or constant stays the same constructor or constant.
/// </summary>
internal sealed class RefinementCaptureCheck {
  private readonly ErrorReporter reporter;
  private readonly ModuleDefinition refining;
  private readonly ModuleDefinition? refined;
  private readonly IReadOnlyDictionary<IVariable, IVariable> refinedLocals;

  public RefinementCaptureCheck(ErrorReporter reporter, ModuleDefinition refining,
    IReadOnlyDictionary<IVariable, IVariable> refinedLocals) {
    this.reporter = reporter;
    this.refining = refining;
    this.refinedLocals = refinedLocals;
    refined = refining.Implements?.Target.Def;
  }

  private sealed record ConstantPattern(string Name, object? Value);

  /// A resolved view of a CasePattern, whose type parameter the check does not need.
  internal sealed record CasePatternView(System.Func<object?> Target, IOrigin Origin, string Id);

  public void Run(IEnumerable<(object Clone, object Original)> occurrences) {
    if (refined == null) {
      return;
    }
    var reported = new HashSet<(string?, int, int, object)>();
    // Occurrences already reported. A qualified name whose qualifier was captured is not
    // reported again; the qualifier is copied, and so recorded, before the name around it.
    var reportedNodes = new HashSet<object>(ReferenceEqualityComparer.Instance);
    foreach (var (clone, original) in occurrences) {
      var qualifier = clone switch {
        ExprDotName d => d.Lhs,
        ApplySuffix a => a.Lhs,
        _ => null
      };
      if (qualifier != null && reportedNodes.Contains(qualifier)) {
        reportedNodes.Add(clone);
        continue;
      }
      var then = Target(original);
      var now = Target(clone);
      if (then == null || now == null || Corresponds(now, then)) {
        continue;
      }
      reportedNodes.Add(clone);
      var origin = OccurrenceOrigin(clone);
      if (origin == null || !reported.Add((origin.Uri?.ToString(), origin.line, origin.col, now))) {
        continue;
      }
      var name = OccurrenceName(original) ?? NameOf(then);
      var at = new RefinementOrigin(origin is RefinementOrigin r ? r.WrappedOrigin : origin, refining);
      reporter.Error(MessageSource.RefinementTransformer, "ref_inherited_name_captured", at,
        $"refining module '{refining.Name}' captures the name '{name}' used in code inherited from module '{refined.Name}': " +
        $"in '{refined.Name}' it refers to {Describe(then)}, but in '{refining.Name}' it would refer to {Describe(now)}");
    }
  }

  private static IOrigin? OccurrenceOrigin(object node) => node switch {
    ApplySuffix a => OccurrenceOrigin(a.Lhs),
    Expression e => e.Origin,
    UserDefinedType t => t.Origin,
    ExtendedPattern p => p.Origin,
    CasePatternView v => v.Origin,
    INode n => n.Origin,
    _ => null
  };

  private static string? OccurrenceName(object node) => node switch {
    NameSegment n => n.Name,
    ExprDotName d => d.SuffixName,
    ApplySuffix a => OccurrenceName(a.Lhs),
    UserDefinedType t => t.Name,
    IdPattern p => p.Id,
    CasePatternView v => v.Id,
    _ => null
  };

  /// What a resolved occurrence denotes: a declaration, a variable or a constant pattern.
  /// Null when the node was not resolved, or denotes nothing the check compares.
  private static object? Target(object node) {
    switch (node) {
      case UserDefinedType t:
        return t.ResolvedClass;
      case IdPattern p:
        if (p.IsWildcardPattern) {
          return null;
        }
        if (p.Ctor != null) {
          return p.Ctor;
        }
        if (p.ResolvedLit != null) {
          return new ConstantPattern(p.Id, p.ResolvedLit.Value);
        }
        return p.BoundVar;
      case CasePatternView v:
        return v.Target();
      case Expression e:
        return ExpressionTarget(e);
      default:
        return null;
    }
  }

  private static object? ExpressionTarget(Expression e) {
    var seen = 0;
    while (e is ConcreteSyntaxExpression { ResolvedExpression: { } resolved } && seen++ < 8) {
      e = resolved;
    }
    return e switch {
      IdentifierExpr id => id.Var,
      MemberSelectExpr select => select.Member,
      FunctionCallExpr call => call.Function,
      DatatypeValue value => value.Ctor,
      ResolverIdentifierExpr rid => rid.Decl,
      _ => null
    };
  }

  private bool Corresponds(object now, object then) {
    if (ReferenceEquals(now, then)) {
      return true;
    }
    switch (then) {
      case ConstantPattern c:
        return now is ConstantPattern d && Equals(c.Value, d.Value);
      case IVariable v:
        if (now is not IVariable u) {
          return false;
        }
        // A copy keeps the position of its original. A parameter may also be the refining
        // module's own redeclaration of the inherited signature, and a local variable the
        // refining module's declaration that refines the inherited one.
        if (refinedLocals.TryGetValue(u, out var refinedLocal)) {
          u = refinedLocal;
        }
        return u == v || SamePosition(u.Origin, v.Origin) || (u is Formal && v is Formal && u.Name == v.Name);
      case TypeParameter tp:
        return now is TypeParameter up &&
               ((up.Name == tp.Name && (SamePosition(up.Origin, tp.Origin) || ParentsCorrespond(up.Parent, tp.Parent))) ||
                IsSynonymParameterCounterpart(up, tp));
      case DatatypeCtor ctor:
        return now is DatatypeCtor uc && uc.Name == ctor.Name &&
               uc.EnclosingDatatype != null && ctor.EnclosingDatatype != null &&
               Corresponds(uc.EnclosingDatatype, ctor.EnclosingDatatype);
      case MemberDecl member:
        if (now is not MemberDecl um) {
          return false;
        }
        for (MemberDecl? m = um; m != null; m = m.OverriddenMember) {
          if (m == member || (m.Name == member.Name && EnclosingCorresponds(m.EnclosingClass, member.EnclosingClass))) {
            return true;
          }
        }
        return false;
      case TopLevelDecl decl:
        return now is TopLevelDecl ud && ud is not TypeParameter && ud.Name == decl.Name &&
               ModulesCorrespond(ud.EnclosingModuleDefinition, decl.EnclosingModuleDefinition);
      default:
        return false;
    }
  }

  private bool EnclosingCorresponds(TopLevelDecl? now, TopLevelDecl? then) {
    if (now == null || then == null) {
      return now == then;
    }
    return Corresponds(now, then) || IsSynonymCounterpart(now, then);
  }

  /// Whether the counterpart of the type `then` is a type synonym for `now`, as when a synonym
  /// refines an abstract type with members (issue 91). A member of `then` then corresponds to the
  /// member of the same name in `now`.
  private bool IsSynonymCounterpart(TopLevelDecl now, TopLevelDecl then) {
    return SynonymCounterpart(now, then) != null;
  }

  /// The type synonym that is the counterpart of `then` and whose right-hand side is `now`, if any.
  private TypeSynonymDeclBase? SynonymCounterpart(TopLevelDecl now, TopLevelDecl then) {
    var candidates = now.EnclosingModuleDefinition?.TopLevelDecls ?? [];
    candidates = candidates.Concat(ModuleAndNested(refining).SelectMany(m => m.TopLevelDecls));
    foreach (var candidate in candidates) {
      if (candidate is TypeSynonymDeclBase synonym && synonym.Name == then.Name &&
          ModulesCorrespond(synonym.EnclosingModuleDefinition, then.EnclosingModuleDefinition) &&
          synonym.Rhs.NormalizeExpand() is UserDefinedType { ResolvedClass: { } target } &&
          (target == now || (target is NonNullTypeDecl nonNull && nonNull.Class == now))) {
        return synonym;
      }
    }
    return null;
  }

  /// Whether `now`, a type parameter of the type that a synonym counterpart of `then`'s type
  /// stands for, is the parameter to which the synonym passes its own parameter in `then`'s
  /// position: for `type O<X> = L<X>`, the X of L corresponds to the X of the inherited O.
  private bool IsSynonymParameterCounterpart(TypeParameter now, TypeParameter then) {
    if (now.Parent is not TopLevelDecl nowType || then.Parent is not TopLevelDecl thenType ||
        SynonymCounterpart(nowType, thenType) is not { } synonym) {
      return false;
    }
    var j = thenType.TypeArgs.IndexOf(then);
    var i = nowType.TypeArgs.IndexOf(now);
    if (i < 0 || j < 0 || synonym.TypeArgs.Count != thenType.TypeArgs.Count ||
        synonym.Rhs.NormalizeExpand() is not UserDefinedType { TypeArgs: var args } || i >= args.Count) {
      return false;
    }
    return args[i].NormalizeExpand() is UserDefinedType { ResolvedClass: TypeParameter passed } &&
           passed == synonym.TypeArgs[j];
  }

  private static IEnumerable<ModuleDefinition> ModuleAndNested(ModuleDefinition module) {
    yield return module;
    foreach (var nested in module.TopLevelDecls.OfType<LiteralModuleDecl>()) {
      foreach (var m in ModuleAndNested(nested.ModuleDef)) {
        yield return m;
      }
    }
  }

  private bool ParentsCorrespond(TypeParameter.ParentType? now, TypeParameter.ParentType? then) {
    return now != null && then != null && Corresponds(now, then);
  }

  private bool ModulesCorrespond(ModuleDefinition? now, ModuleDefinition? then) {
    if (now == null || then == null) {
      return now == then;
    }
    if (now == then) {
      return true;
    }
    if (then == refined) {
      return now == refining;
    }
    if (then.IsFacade) {
      // An abstract import that the refining module binds to a module along the target's chain.
      var target = Unfacade(then);
      var visited = new HashSet<ModuleDefinition>();
      for (var m = Unfacade(now); m != null && visited.Add(m); m = m.Implements?.Target.Def) {
        if (m == target) {
          return true;
        }
      }
      return false;
    }
    // A module nested in the base corresponds to the module of the same name nested in the
    // counterpart of its parent.
    return now.Name == then.Name && now.EnclosingModule != null && then.EnclosingModule != null &&
           now.EnclosingModule != then.EnclosingModule &&
           ModulesCorrespond(now.EnclosingModule, then.EnclosingModule);
  }

  private static ModuleDefinition Unfacade(ModuleDefinition m) {
    var visited = new HashSet<ModuleDefinition>();
    while (m.FacadeOf != null && visited.Add(m)) {
      m = m.FacadeOf;
    }
    return m;
  }

  private static bool SamePosition(IOrigin? a, IOrigin? b) {
    if (a == null || b == null) {
      return false;
    }
    return a.Uri == b.Uri && a.line == b.line && a.col == b.col && a.line != 0;
  }

  private static string NameOf(object target) => target switch {
    ConstantPattern c => c.Value?.ToString() ?? "null",
    IVariable v => v.Name,
    Declaration d => d.Name,
    _ => target.ToString() ?? ""
  };

  private string Describe(object target) {
    switch (target) {
      case ConstantPattern c:
        return $"the constant '{c.Name}', a pattern that matches only {c.Value}";
      case IVariable v:
        var kind = v switch {
          Formal => "parameter",
          LocalVariable => "local variable",
          _ => "bound variable"
        };
        return $"{kind} '{v.Name}'{Where(v.Origin)}";
      case DatatypeCtor ctor:
        var datatype = ctor.EnclosingDatatype == null ? null : QualifiedName(ctor.EnclosingDatatype);
        return $"datatype constructor '{(datatype == null ? ctor.Name : datatype + "." + ctor.Name)}'{Where(ctor.Origin)}";
      case MemberDecl member:
        return $"{member.WhatKind} '{MemberName(member)}'{Where(member.Origin)}";
      case TypeParameter tp:
        return $"type parameter '{tp.Name}'{Where(tp.Origin)}";
      case TopLevelDecl decl:
        return $"{decl.WhatKind} '{QualifiedName(decl)}'{Where(decl.Origin)}";
      default:
        return $"'{NameOf(target)}'";
    }
  }

  // A declaration in the signature facade of an abstract import is named after the module that the
  // facade stands for, where it was declared, rather than after the facade's internal name.
  private static string ModuleName(ModuleDefinition module) => Unfacade(module).FullDafnyName;

  private static string QualifiedName(TopLevelDecl decl) {
    return decl.EnclosingModuleDefinition is not { } module ? decl.Name
      : module.FacadeOf != null ? $"{ModuleName(module)}.{decl.Name}"
      : decl.FullDafnyName;
  }

  private static string MemberName(MemberDecl member) {
    return member.EnclosingClass is { } enclosing && enclosing is not DefaultClassDecl
      ? $"{QualifiedName(enclosing)}.{member.Name}"
      : member.EnclosingClass?.EnclosingModuleDefinition is { } module && module.Name != "_module"
        ? $"{ModuleName(module)}.{member.Name}"
        : member.Name;
  }

  private string Where(IOrigin? origin) {
    if (origin == null || origin.line == 0 || origin.Uri == null) {
      return "";
    }
    return $" (declared at {origin.OriginToString(reporter.Options)})";
  }
}
