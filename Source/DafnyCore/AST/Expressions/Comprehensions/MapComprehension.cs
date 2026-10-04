#nullable enable

using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Microsoft.Dafny;

public class MapComprehension : ComprehensionExpr, ICloneable<MapComprehension> {
  public override string WhatKind => "map comprehension";

  public bool Finite;
  public Expression? TermLeft;

  /// <summary>
  /// Translation-only projections for a general map comprehension, in the final
  /// BoundVars order. Projection i takes a predicate on the complete backend
  /// witness tuple and returns the representation of BoundVars[i]. Every component
  /// of a selected tuple receives the same predicate for the current key and
  /// environment; source-scoped facts establish their coordinated correctness.
  /// Ordinary AST clones get new symbols. Substitution with the same BoundVars
  /// shares the declaration source: relation parameters carry the changed captures,
  /// while symbol identity keeps rebuilt characteristic lambdas equivalent.
  /// Translated predicates are never cached here because their captures depend on
  /// the active translator.
  /// </summary>
  [FilledInDuringTranslation]
  public List<Boogie.Function>? ProjectionFunctions;

  // Same-binder substitutions share this source even before its symbols have
  // been declared. Keeping only a currently-null symbol list would lose identity.
  [FilledInDuringTranslation]
  internal MapComprehension? ProjectionFunctionsSource;

  public MapComprehension Clone(Cloner cloner) {
    return new MapComprehension(cloner, this);
  }

  public MapComprehension(Cloner cloner, MapComprehension original) : base(cloner, original) {
    TermLeft = cloner.CloneExpr(original.TermLeft);
    Finite = original.Finite;
  }

  [SyntaxConstructor]
  public MapComprehension(IOrigin origin, bool finite, List<BoundVar> boundVars, Expression range, Expression? termLeft, Expression term, Attributes? attributes = null)
    : base(origin, boundVars, range, term, attributes) {
    Contract.Requires(1 <= boundVars.Count);
    Contract.Requires(termLeft != null || boundVars.Count == 1);

    Finite = finite;
    TermLeft = termLeft;
  }

  /// <summary>
  /// IsGeneralMapComprehension returns true for general map comprehensions.
  /// In other words, it returns false if either no TermLeft was given or if
  /// the given TermLeft is the sole bound variable.
  /// This property getter requires that the expression has been successfully
  /// resolved.
  /// </summary>
  public bool IsGeneralMapComprehension {
    get {
      Contract.Requires(WasResolved());
      if (TermLeft == null) {
        return false;
      } else if (BoundVars.Count != 1) {
        return true;
      }
      var lhs = StripParens(TermLeft).Resolved;
      if (lhs is IdentifierExpr ide && ide.Var == BoundVars[0]) {
        // TermLeft is the sole bound variable, so this is the same as
        // if TermLeft wasn't given at all
        return false;
      }
      return true;
    }
  }

  public override IEnumerable<Expression> SubExpressions {
    get {
      foreach (var e in Attributes.SubExpressions(Attributes)) {
        yield return e;
      }
      if (Range != null) { yield return Range; }
      if (TermLeft != null) { yield return TermLeft; }
      yield return Term;
    }
  }
}