//-----------------------------------------------------------------------------
//
// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
//
//-----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// HigherOrderHeapAllocationChecker rejects storing a function with read effects in memory, by the
/// static type of the stored value.  In generic code that type is a type parameter, so the check does
/// not see the function, and a generic class could store a reading function that reads itself
/// (https://github.com/erniecohen/dafny/issues/481).  This checker closes that hole.
///
/// It infers which type parameters of the module's declarations may be stored in memory.  A type
/// parameter is stored if it occurs in the type of a mutable field of its class, trait or iterator, if
/// a value whose type mentions it is stored in a mutable field or an array element, or put in a new array by
/// an initializer, or if it occurs in a type argument passed to a stored type parameter of another
/// declaration.  The characteristic (!reads) declares a type parameter stored.  Then:
///   * a stored type parameter must not be instantiated with a type that contains a function with
///     read effects;
///   * a type parameter of an overriding member, or of a declaration in a refining module, may be
///     stored only if the corresponding type parameter of what it overrides or refines is.
/// The elements of an array are not stored by fiat: a write to an array element is a store in the
/// code that makes it.  Type parameters of other modules were inferred when those modules were
/// resolved.  The hidden --legacy-generic-function-stores turns the checker off.
/// </summary>
class HigherOrderHeapAllocationCheckerGeneric : ASTVisitor<IASTVisitorContext> {
  private const string Prefix = "To prevent the creation of non-terminating functions, ";

  private readonly ErrorReporter reporter;
  private readonly HashSet<TypeParameter> local = [];
  private readonly List<(TypeParameter Formal, Type Actual, IOrigin Origin, object Context)> instantiations = [];
  private readonly HashSet<(object, string)> reported = [];

  public HigherOrderHeapAllocationCheckerGeneric(ErrorReporter reporter) {
    this.reporter = reporter;
  }

  public override IASTVisitorContext GetContext(IASTVisitorContext astVisitorContext, bool inFunctionPostcondition) {
    return astVisitorContext;
  }

  public void Check(List<TopLevelDecl> declarations) {
    foreach (var decl in declarations) {
      CollectSignature(decl);
    }
    VisitDeclarations(declarations);
    Propagate();
    foreach (var (formal, actual, origin, context) in instantiations) {
      if (!IsStored(formal) || !ContainsRead(actual, [])) {
        continue;
      }
      var message = Prefix + "a type parameter whose values may be stored in memory cannot be instantiated with a type that contains a function with read effects: " +
                    $"type parameter '{formal.Name}' of '{ParentName(formal)}' is instantiated with '{actual}', and {Reason(formal)}";
      // Report an instantiation once in each declaration, and once at each place, also when a refining
      // module copies the declaration.
      if (reported.Add((context, $"{formal.Name}|{ParentName(formal)}|{actual}")) &&
          reported.Add((null, $"{origin?.Uri}|{origin?.line}:{origin?.col}|{formal.Name}|{ParentName(formal)}|{actual}"))) {
        reporter.Error(MessageSource.Resolver, origin, message);
      }
    }
    foreach (var decl in declarations) {
      CheckOverridesAndRefinements(decl);
    }
  }

  private static bool IsStored(TypeParameter tp) {
    return tp.StoredInMemoryReason != null || tp.Characteristics.ContainsNoReadingFunctions;
  }

  private static string Reason(TypeParameter tp) {
    return tp.StoredInMemoryReason ?? "it is declared with the characteristic (!reads)";
  }

  private static string ParentName(TypeParameter tp) {
    return tp.Parent switch {
      MemberDecl { EnclosingClass: DefaultClassDecl } member => member.Name,
      MemberDecl member => $"{member.EnclosingClass.Name}.{member.Name}",
      TopLevelDecl decl => decl.Name,
      _ => tp.Parent?.FullName ?? "?"
    };
  }

  private void MarkStored(TypeParameter tp, string reason) {
    if (local.Contains(tp) && tp.StoredInMemoryReason == null) {
      tp.StoredInMemoryReason = reason;
    }
  }

  private void MarkStored(Type type, string reason) {
    foreach (var tp in TypeParametersIn(type)) {
      MarkStored(tp, reason);
    }
  }

  private static HashSet<TypeParameter> TypeParametersIn(Type type) {
    var result = new HashSet<TypeParameter>();
    void Walk(Type t) {
      var normalized = t?.Normalize();
      if (normalized == null) {
        return;
      }
      if (normalized is UserDefinedType { ResolvedClass: TypeParameter tp }) {
        result.Add(tp);
      }
      normalized.TypeArgs.ForEach(Walk);
    }
    Walk(type);
    return result;
  }

  /// <summary>
  /// Records, for every user-defined type in "type", that its type arguments instantiate the type
  /// parameters of its declaration.  The type is not expanded: a type synonym or a subset type is an
  /// instantiation of its own type parameters, whose right-hand side is checked where it is declared.
  /// </summary>
  private void Instantiations(Type type, IOrigin origin, object context) {
    var normalized = type?.Normalize();
    if (normalized == null) {
      return;
    }
    if (normalized is UserDefinedType { ResolvedClass: { } resolved } udt) {
      var decl = resolved is NonNullTypeDecl nonNull ? nonNull.Class : resolved;
      if (decl.TypeArgs.Count == udt.TypeArgs.Count) {
        for (var i = 0; i < udt.TypeArgs.Count; i++) {
          instantiations.Add((decl.TypeArgs[i], udt.TypeArgs[i], origin ?? udt.Origin, context));
        }
      }
    }
    foreach (var typeArg in normalized.TypeArgs) {
      Instantiations(typeArg, origin, context);
    }
  }

  private void Instantiations(List<TypeParameter> formals, List<Type> actuals, IOrigin origin, object context) {
    if (formals == null || actuals == null || formals.Count != actuals.Count) {
      return;
    }
    for (var i = 0; i < formals.Count; i++) {
      instantiations.Add((formals[i], actuals[i], origin, context));
      Instantiations(actuals[i], origin, context);
    }
  }

  /// <summary>
  /// The type parameters a declaration introduces, the parent types it extends, the arguments of its
  /// datatype constructors, and its mutable fields, which the AST visitor does not show as such.
  /// </summary>
  private void CollectSignature(TopLevelDecl decl) {
    local.UnionWith(decl.TypeArgs);
    if (CharacteristicsOf(decl) is { ContainsNoReadingFunctions: true } characteristics) {
      reporter.Error(MessageSource.Resolver, characteristics.SourceOrigin ?? decl.Origin,
        "the type characteristic (!reads) is allowed only on a type parameter");
    }
    if (decl is TopLevelDeclWithMembers withMembers) {
      foreach (var parent in withMembers.Traits ?? []) {
        Instantiations(parent, null, decl);
      }
      foreach (var member in withMembers.Members) {
        switch (member) {
          case Function function:
            local.UnionWith(function.TypeArgs);
            if (function is ExtremePredicate { PrefixPredicate: { } prefixPredicate }) {
              local.UnionWith(prefixPredicate.TypeArgs);
            }
            if (function.ByMethodDecl is { } byMethod) {
              local.UnionWith(byMethod.TypeArgs);
            }
            break;
          case MethodOrConstructor method:
            local.UnionWith(method.TypeArgs);
            if (method is ExtremeLemma { PrefixLemma: { } prefixLemma }) {
              local.UnionWith(prefixLemma.TypeArgs);
            }
            break;
        }
      }
      foreach (var field in withMembers.Members.OfType<Field>().Where(field => field.IsMutable && !field.IsStatic)) {
        MarkStored(field.Type, $"the mutable field '{field.Name}' of '{decl.Name}' has a type that mentions it");
      }
    }
    if (decl is DatatypeDecl datatype) {
      foreach (var formal in datatype.Ctors.SelectMany(ctor => ctor.Formals)) {
        Instantiations(formal.Type, null, decl);
      }
    }
  }

  private static TypeParameterCharacteristics CharacteristicsOf(TopLevelDecl decl) {
    return decl switch {
      AbstractTypeDecl abstractType => abstractType.Characteristics,
      TypeSynonymDecl synonym => synonym.Characteristics,
      _ => TypeParameterCharacteristics.Default()
    };
  }

  private void Propagate() {
    var changed = true;
    while (changed) {
      changed = false;
      foreach (var (formal, actual, _, _) in instantiations) {
        if (!IsStored(formal)) {
          continue;
        }
        foreach (var tp in TypeParametersIn(actual).Where(tp => local.Contains(tp) && tp.StoredInMemoryReason == null)) {
          tp.StoredInMemoryReason = $"it instantiates type parameter '{formal.Name}' of '{ParentName(formal)}', whose values may be stored in memory";
          changed = true;
        }
      }
    }
  }

  /// <summary>
  /// Whether a value of "type" may contain a function with read effects, also inside a datatype,
  /// a newtype, a subset type or a type synonym, whatever the scope shows of its definition.
  /// </summary>
  private static bool ContainsRead(Type type, HashSet<TopLevelDecl> visited) {
    var normalized = type.Normalize();
    if (normalized is UserDefinedType { ResolvedClass: ArrowTypeDecl }) {
      return true;
    }
    if (normalized.TypeArgs.Any(typeArg => ContainsRead(typeArg, visited))) {
      return true;
    }
    if (normalized is not UserDefinedType { ResolvedClass: { } decl } udt ||
        decl.EnclosingModuleDefinition?.Name == "_System" ||
        decl.TypeArgs.Count != udt.TypeArgs.Count || !visited.Add(decl)) {
      return false;
    }
    IEnumerable<Type> components = decl switch {
      DatatypeDecl datatype => datatype.Ctors.SelectMany(ctor => ctor.Formals)
        .Select(formal => formal.Type.Subst(TypeParameter.SubstitutionMap(datatype.TypeArgs, udt.TypeArgs))),
      NewtypeDecl { BaseType: not null } newtype => [newtype.RhsWithArgument(udt.TypeArgs)],
      TypeSynonymDecl synonym => [synonym.RhsWithArgumentIgnoringScope(udt.TypeArgs)],
      _ => []
    };
    return components.Any(component => ContainsRead(component, visited));
  }

  protected override void VisitUserProvidedType(Type type, IASTVisitorContext context) {
    Instantiations(type, null, context);
  }

  protected override bool VisitOneExpression(Expression expr, IASTVisitorContext context) {
    if (expr.Type != null) {
      Instantiations(expr.Type, expr.Origin, context);
    }
    switch (expr) {
      case MemberSelectExpr memberSelect:
        var memberTypeArgs = memberSelect.Member switch {
          Function function => function.TypeArgs,
          MethodOrConstructor method => method.TypeArgs,
          _ => null
        };
        Instantiations(memberTypeArgs, memberSelect.TypeApplicationJustMember, expr.Origin, context);
        break;
      case FunctionCallExpr { Function: { } function } call:
        Instantiations(function.TypeArgs, call.TypeApplication_JustFunction, expr.Origin, context);
        break;
    }
    return base.VisitOneExpression(expr, context);
  }

  private static bool IsMemoryLocation(Expression lhs) {
    return lhs?.Resolved is MemberSelectExpr { Member: Field { IsMutable: true } } or SeqSelectExpr or MultiSelectExpr;
  }

  protected override bool VisitOneStatement(Statement stmt, IASTVisitorContext context) {
    var reason = $"a value whose type mentions it is stored in memory in '{ContextName(context)}'";
    switch (stmt) {
      case VarDeclStmt varDecl:
        foreach (var localVariable in varDecl.Locals) {
          Instantiations(localVariable.Type, localVariable.Origin, context);
        }
        break;
      case SingleAssignStmt assign:
        if (assign.Rhs is TypeRhs typeRhs) {
          Instantiations(typeRhs.Type, stmt.Origin, context);
          if (typeRhs is AllocateArray { ElementType: { } elementType } allocate &&
              (allocate.ElementInit != null || allocate.InitDisplay != null)) {
            MarkStored(elementType, reason);
          }
        }
        if (IsMemoryLocation(assign.Lhs)) {
          MarkStored(assign.Lhs.Type, reason);
          if (assign.Rhs is ExprRhs { Expr.Type: { } rhsType }) {
            MarkStored(rhsType, reason);
          }
        }
        break;
      case CallStmt call:
        foreach (var lhs in call.Lhs.Where(IsMemoryLocation)) {
          MarkStored(lhs.Type, reason);
        }
        break;
    }
    return base.VisitOneStatement(stmt, context);
  }

  private static string ContextName(IASTVisitorContext context) {
    return context switch {
      MemberDecl { EnclosingClass: DefaultClassDecl } member => member.Name,
      MemberDecl member => $"{member.EnclosingClass.Name}.{member.Name}",
      TopLevelDecl decl => decl.Name,
      _ => "?"
    };
  }

  private void CheckOverridesAndRefinements(TopLevelDecl decl) {
    if (decl.CardinalityRefinementBase is { } baseDecl && baseDecl != decl) {
      CheckCounterparts(decl.TypeArgs, baseDecl.TypeArgs, decl.Origin, "a declaration in a refining module",
        $"the declaration it refines in module '{baseDecl.EnclosingModuleDefinition?.Name}'");
    }
    if (decl is not TopLevelDeclWithMembers withMembers) {
      return;
    }
    foreach (var member in withMembers.Members) {
      var typeArgs = member switch {
        Function function => function.TypeArgs,
        MethodOrConstructor method => method.TypeArgs,
        _ => null
      };
      if (typeArgs == null) {
        continue;
      }
      var overridden = member switch {
        Function function => (MemberDecl)function.OverriddenFunction,
        MethodOrConstructor method => method.OverriddenMethod,
        _ => null
      };
      if (overridden != null) {
        CheckCounterparts(typeArgs, TypeArgsOf(overridden), member.Origin, $"an overriding {member.WhatKind}",
          $"the {overridden.WhatKind} it overrides in '{overridden.EnclosingClass?.Name}'");
      }
      if (member.RefinementBase is { } refined && refined != member) {
        CheckCounterparts(typeArgs, TypeArgsOf(refined), member.Origin, $"a {member.WhatKind} in a refining module",
          $"the {refined.WhatKind} it refines in module '{refined.EnclosingClass?.EnclosingModuleDefinition?.Name}'");
      }
    }
  }

  private static List<TypeParameter> TypeArgsOf(MemberDecl member) {
    return member switch {
      Function function => function.TypeArgs,
      MethodOrConstructor method => method.TypeArgs,
      _ => null
    };
  }

  private void CheckCounterparts(List<TypeParameter> typeArgs, List<TypeParameter> baseTypeArgs, IOrigin origin, string what, string baseWhat) {
    if (baseTypeArgs == null || typeArgs.Count != baseTypeArgs.Count) {
      return;
    }
    for (var i = 0; i < typeArgs.Count; i++) {
      var tp = typeArgs[i];
      var baseTp = baseTypeArgs[i];
      if (IsStored(tp) && !IsStored(baseTp)) {
        reporter.Error(MessageSource.Resolver, origin,
          Prefix + $"{what} may store values of a type parameter in memory only if {baseWhat} may: " +
          $"values of type parameter '{tp.Name}' of '{ParentName(tp)}' may be stored in memory, because {Reason(tp)}, " +
          $"but those of the corresponding type parameter '{baseTp.Name}' of {baseWhat} may not " +
          "(declare it with the characteristic (!reads) to allow it)");
      }
    }
  }
}
