//-----------------------------------------------------------------------------
//
// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
//
//-----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Microsoft.Dafny;

/// <summary>
/// The conditions under which the results of a lemma can be bound in an expression (see LetExpr.LemmaBinding and
/// LemmaResultExpr).  The bound results are a function of the lemma's arguments (and of the heap, if the lemma's
/// postcondition reads it), as the value of a function is.  So,
///
///   1. the lemma is an ordinary lemma (not a two-state, least, greatest or prefix lemma);
///   2. no result may show a reference, since which references exist is not a function of the arguments; and
///   3. the postcondition is an expression that the body of a function may contain: it does not mention "old",
///      "allocated", "fresh", "unchanged" or a two-state function, and it does not quantify over the allocated
///      references.
///
/// Condition 1 is checked when the binding is resolved.  Conditions 2 and 3 are checked by LemmaBindingChecker,
/// once the bounds of quantifiers are known.  The translator asks again, and says nothing about the results of a
/// lemma that does not meet the conditions.
/// </summary>
public static class LemmaBindings {
  /// <summary>
  /// Returns null if the postcondition of "lemma" meets condition 3. Otherwise, returns a phrase that says what the
  /// postcondition does, to follow "because its postcondition ". To be called after bounds discovery of the
  /// module that declares "lemma".
  /// </summary>
  public static string WhyPostconditionCannotBeBound(Lemma lemma) {
    Contract.Requires(lemma != null);
    foreach (var postcondition in lemma.Ens) {
      var why = WhyNotAllowedInFunction(postcondition.E);
      if (why != null) {
        return why;
      }
    }
    return null;
  }

  private static string WhyNotAllowedInFunction(Expression expr) {
    switch (expr) {
      case OldExpr:
        return "uses 'old'";
      case UnchangedExpr:
        return "uses 'unchanged'";
      case FreshExpr:
        return "uses 'fresh'";
      case UnaryOpExpr { Op: UnaryOpExpr.Opcode.Allocated }:
        return "uses 'allocated'";
      case FunctionCallExpr { Function: TwoStateFunction twoStateFunction }:
        return $"uses the {twoStateFunction.WhatKind} '{twoStateFunction.Name}'";
      case MemberSelectExpr { Member: TwoStateFunction twoStateFunction }:
        return $"uses the {twoStateFunction.WhatKind} '{twoStateFunction.Name}'";
      case ComprehensionExpr comprehension and not LambdaExpr:
        // This is the condition that bounds discovery places on a comprehension in a function.
        foreach (var boundVariable in BoundedPool.MissingBounds(comprehension.BoundVars, comprehension.Bounds,
                   BoundedPool.PoolVirtues.IndependentOfAlloc)) {
          return $"has a {comprehension.WhatKind} that depends on the set of allocated references" +
                 $" (values of '{boundVariable.Name}', of type '{boundVariable.Type}', may contain references)";
        }
        break;
    }

    // A statement in a statement expression does not affect the expression's value, so only expressions are visited.
    foreach (var subexpression in expr.SubExpressions) {
      var why = WhyNotAllowedInFunction(subexpression);
      if (why != null) {
        return why;
      }
    }
    return null;
  }

  /// <summary>
  /// Returns null if each result of "lemma", at the type arguments "typeMap", meets condition 2. Otherwise, returns
  /// a phrase that says which result does not, to follow "because ".
  /// </summary>
  public static string WhyResultTypesCannotBeBound(Lemma lemma, Dictionary<TypeParameter, Type> typeMap) {
    Contract.Requires(lemma != null);
    Contract.Requires(typeMap != null);
    foreach (var result in lemma.Outs) {
      var type = result.Type.Subst(typeMap);
      if (type.MayShowReferences) {
        var hint = type.IsTypeParameter || type.IsAbstractType ? $" (perhaps declare its type as '{type}(!new)')" : "";
        return $"values of its out-parameter '{result.Name}' (of type '{type}') may contain references{hint}";
      }
    }
    return null;
  }
}

/// <summary>
/// Reports an error at each expression that binds the results of a lemma that does not meet conditions 2 and 3 of
/// LemmaBindings. To be run after bounds discovery.
/// </summary>
class LemmaBindingChecker : ASTVisitor<IASTVisitorContext> {
  private readonly ErrorReporter reporter;

  public LemmaBindingChecker(ErrorReporter reporter) {
    this.reporter = reporter;
  }

  public override IASTVisitorContext GetContext(IASTVisitorContext astVisitorContext, bool inFunctionPostcondition) {
    return astVisitorContext;
  }

  protected override bool VisitOneExpression(Expression expr, IASTVisitorContext context) {
    if (expr is StmtExpr { LemmaBindingCall: { Method: Lemma lemma } call }) {
      var typeMap = call.MethodSelect.TypeArgumentSubstitutionsWithParents();
      var why = LemmaBindings.WhyResultTypesCannotBeBound(lemma, typeMap);
      if (why == null && LemmaBindings.WhyPostconditionCannotBeBound(lemma) is { } whyPostcondition) {
        why = "its postcondition " + whyPostcondition;
      }
      if (why != null) {
        reporter.Error(MessageSource.Resolver, call.Origin,
          $"the results of lemma '{lemma.Name}' cannot be bound in an expression, because {why}");
      }
    }
    return base.VisitOneExpression(expr, context);
  }
}
