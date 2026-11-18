#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// How a formula depends on the set of values of a type parameter: it is monotone (Positive) if it stays true when
/// the type parameter is instantiated with a larger type, antitone (Negative) if it stays true at a smaller one,
/// both (None) if it does not depend on that set, and neither (Mixed) otherwise, or where the analysis cannot tell.
/// </summary>
[Flags]
public enum Polarity { None = 0, Positive = 1, Negative = 2, Mixed = Positive | Negative }

/// <summary>
/// The polarity of the type parameters of a declaration in a formula, for the variance of a type whose values are
/// those of another type that satisfy a formula: a type with member invariants (issue 105, under --member-invariants;
/// section 3.5 of docs/dev/issue105-member-invariants-plan.md), and, by the same argument, a subset type or a newtype
/// (https://github.com/erniecohen/dafny/issues/491).  Such a type may be covariant in a type parameter only where the
/// formula is monotone in it, and contravariant only where the formula is antitone in it.
///
/// The analysis is syntactic and conservative.  A formula's value depends on the set of values of a type only where
/// it ranges over, or tests against, a type built from it; a value of the type, an equality between values, or a
/// conversion does not depend on it.  So:
///  - a universal quantifier over a variable of type U uses the type parameters in U negatively, as U's set of values
///    does, and an existential quantifier or a type test "e is U" uses them positively;
///  - a bound variable that ranges over the values of an expression of its own type U (the elements of a collection,
///    one value, or the subsets of a set: the bounds that the extension condition accepts) does not use U: it ranges
///    over the same values whatever the type arguments are, as long as that expression does not depend on them, which
///    the analysis of the expression, part of the range, decides;
///  - a type parameter is positive in itself, and in a type G&lt;..., U, ...&gt; as G's declared variance and its
///    polarity in U compose: a covariant parameter keeps it, a contravariant one (such as an arrow's parameter
///    types) flips it, and a non-variant one makes it mixed;
///  - a negation and the antecedent of an implication flip the polarity of their operands, a conjunction, a
///    disjunction, the consequent of an implication, the branches of a conditional and the bodies of quantifiers,
///    let and match expressions keep it, and every other operand is mixed (the condition of a conditional, the
///    operands of an equality or an equivalence, the arguments of a call, and any operand that is not boolean);
///  - a comprehension, a lambda expression or a let-such-that expression whose bound variables have a type built
///    from a type parameter uses it mixed, unless, for a set or map comprehension, the variable ranges over the values
///    of an expression of its type, as above (the resolver finds no bounds for a lambda expression); a let-such-that
///    expression is not covered, since the value it chooses may depend on the type arguments even where the values it
///    chooses from do not;
///  - a call of a generic function, or a use of a constant, contributes the polarity of its type parameters in its
///    body, composed with its type arguments; the polarities of the functions a formula reaches are computed
///    together, as a least fixpoint;
///  - where a body is not known (an abstract, bodiless or dynamically dispatched function, or one whose body is not
///    revealed to the module of the declaration analysed), each of its type parameters is mixed; so is each type
///    argument of a function used as a value;
///  - a statement in an expression (an assertion, a lemma call, a calculation) is a proof hint and does not
///    contribute to the formula's meaning, so it is not analysed.
/// The analysis trusts the declared variance of each type it meets, as subtyping does; for a subset type or a
/// newtype that trust is what issue 491 is about.
/// </summary>
public class TypeParameterPolarity {
  /// <summary>
  /// A place where the formula uses a type parameter of interest with a polarity other than None.
  /// </summary>
  public record Occurrence(IOrigin Origin, string Description, Polarity Polarity);

  /// <summary>
  /// The uses of the type parameters of interest: for each, the first occurrence that uses it positively and the first
  /// that uses it negatively.
  /// </summary>
  public class Uses {
    private readonly Dictionary<TypeParameter, (Occurrence? Positive, Occurrence? Negative)> uses = new();

    public Polarity Of(TypeParameter typeParameter) {
      var (positive, negative) = uses.GetValueOrDefault(typeParameter);
      return (positive != null ? Polarity.Positive : Polarity.None) | (negative != null ? Polarity.Negative : Polarity.None);
    }

    public Occurrence? Positive(TypeParameter typeParameter) => uses.GetValueOrDefault(typeParameter).Positive;
    public Occurrence? Negative(TypeParameter typeParameter) => uses.GetValueOrDefault(typeParameter).Negative;

    internal void Add(TypeParameter typeParameter, Polarity polarity, IOrigin origin, string description) {
      if (polarity == Polarity.None) {
        return;
      }
      var (positive, negative) = uses.GetValueOrDefault(typeParameter);
      var occurrence = new Occurrence(origin, description, polarity);
      if (positive == null && polarity.HasFlag(Polarity.Positive)) {
        positive = occurrence;
      }
      if (negative == null && polarity.HasFlag(Polarity.Negative)) {
        negative = occurrence;
      }
      uses[typeParameter] = (positive, negative);
    }
  }

  /// <summary>
  /// What a formula is analysed for: the type parameters of interest, and their uses found so far.
  /// </summary>
  public class Collector {
    private readonly TypeParameterPolarity analysis;
    internal readonly IReadOnlyCollection<TypeParameter> TypeParameters;
    public readonly Uses Uses = new();

    internal Collector(TypeParameterPolarity analysis, IReadOnlyCollection<TypeParameter> typeParameters) {
      this.analysis = analysis;
      TypeParameters = typeParameters;
    }

    /// <summary>
    /// Analyse "formula" where its truth has polarity "context" in the whole.
    /// </summary>
    public void Formula(Expression formula, Polarity context) => analysis.Visit(formula, context, this);

    /// <summary>
    /// Analyse a use of "callee", a function or a constant, with the type arguments "typeArgs" (those of its enclosing
    /// declaration, then its own), whose truth has polarity "context" in the whole.
    /// </summary>
    public void Call(MemberDecl callee, IReadOnlyList<Type> typeArgs, Polarity context, IOrigin origin, string description) =>
      analysis.AddCall(callee, typeArgs, context, origin, description, this);
  }

  private readonly ModuleDefinition module;
  private readonly VisibilityScope scope;

  /// <summary>
  /// For each function or constant reached, the polarity in its body of the type parameters of its enclosing
  /// declaration and its own; an under-approximation until Solve has run.
  /// </summary>
  private readonly Dictionary<MemberDecl, Dictionary<TypeParameter, Polarity>> summaries = new();

  /// <param name="module">The module of the declarations analysed.</param>
  /// <param name="scope">What the module sees of the modules it imports: the bodies it uses must be revealed in it.</param>
  public TypeParameterPolarity(ModuleDefinition module, VisibilityScope scope) {
    this.module = module;
    this.scope = scope;
  }

  // ----- the lattice ------------------------------------------------------------------------------------------

  public static Polarity Flip(Polarity polarity) {
    return (polarity.HasFlag(Polarity.Positive) ? Polarity.Negative : Polarity.None) |
           (polarity.HasFlag(Polarity.Negative) ? Polarity.Positive : Polarity.None);
  }

  /// <summary>
  /// The polarity of a use with polarity "inner" in a place with polarity "outer".
  /// </summary>
  public static Polarity Times(Polarity outer, Polarity inner) {
    return (outer.HasFlag(Polarity.Positive) ? inner : Polarity.None) | (outer.HasFlag(Polarity.Negative) ? Flip(inner) : Polarity.None);
  }

  public static Polarity Of(TypeParameter.TPVariance variance) {
    return variance switch {
      TypeParameter.TPVariance.Co => Polarity.Positive,
      TypeParameter.TPVariance.Contra => Polarity.Negative,
      _ => Polarity.Mixed
    };
  }

  public static string Describe(Polarity polarity) {
    return polarity switch {
      Polarity.Positive => "positively",
      Polarity.Negative => "negatively",
      _ => "both positively and negatively"
    };
  }

  /// <summary>
  /// The polarity of "typeParameter" in the set of values of "type".
  /// </summary>
  public static Polarity InType(TypeParameter typeParameter, Type type) {
    type = type.NormalizeExpandKeepConstraints();
    if (type is UserDefinedType { ResolvedClass: TypeParameter other }) {
      return other == typeParameter ? Polarity.Positive : Polarity.None;
    }
    var typeArgs = type.TypeArgs;
    if (typeArgs.Count == 0) {
      return Polarity.None;
    }
    var variances = type switch {
      CollectionType => typeArgs.ConvertAll(_ => TypeParameter.TPVariance.Co),
      ArrowType arrow => arrow.Variances(),
      UserDefinedType { ResolvedClass: { } decl } when decl.TypeArgs.Count == typeArgs.Count => TypeParameter.Variances(decl.TypeArgs),
      _ => typeArgs.ConvertAll(_ => TypeParameter.TPVariance.Non)
    };
    var result = Polarity.None;
    for (var i = 0; i < typeArgs.Count; i++) {
      result |= Times(Of(variances[i]), InType(typeParameter, typeArgs[i]));
    }
    return result;
  }

  // ----- analysis ---------------------------------------------------------------------------------------------

  /// <summary>
  /// The uses of "typeParameters" in the formulas that "analyse" gives a collector.  It is run twice: once to find the
  /// functions the formulas reach, whose polarities are then computed, and once with them.
  /// </summary>
  public Uses Analyze(IReadOnlyCollection<TypeParameter> typeParameters, Action<Collector> analyse) {
    analyse(new Collector(this, typeParameters));
    Solve();
    var collector = new Collector(this, typeParameters);
    analyse(collector);
    return collector.Uses;
  }

  private void AddType(Type type, Polarity polarity, IOrigin origin, string description, Collector collector) {
    if (polarity == Polarity.None) {
      return;
    }
    foreach (var typeParameter in collector.TypeParameters) {
      collector.Uses.Add(typeParameter, Times(polarity, InType(typeParameter, type)), origin, description);
    }
  }

  private void AddCall(MemberDecl callee, IReadOnlyList<Type> typeArgs, Polarity context, IOrigin origin, string description, Collector collector) {
    var formals = FormalTypeParameters(callee);
    var summary = Summary(callee);
    for (var i = 0; i < formals.Count && i < typeArgs.Count; i++) {
      var polarity = summary.GetValueOrDefault(formals[i]);
      AddType(typeArgs[i], Times(context, polarity), origin, description, collector);
    }
  }

  private static List<TypeParameter> FormalTypeParameters(MemberDecl member) {
    var result = new List<TypeParameter>(member.EnclosingClass?.TypeArgs ?? []);
    if (member is Function function) {
      result.AddRange(function.TypeArgs);
    }
    return result;
  }

  private void Visit(Expression? expr, Polarity context, Collector collector) {
    while (expr is ConcreteSyntaxExpression { ResolvedExpression: { } resolved }) {
      expr = resolved;
    }
    switch (expr) {
      case null:
        return;
      case UnaryOpExpr { Op: UnaryOpExpr.Opcode.Not } not:
        Visit(not.E, Flip(context), collector);
        return;
      case BinaryExpr { ResolvedOp: BinaryExpr.ResolvedOpcode.And or BinaryExpr.ResolvedOpcode.Or } binary:
        Visit(binary.E0, context, collector);
        Visit(binary.E1, context, collector);
        return;
      case BinaryExpr { ResolvedOp: BinaryExpr.ResolvedOpcode.Imp } implication:
        // "A <== B" is parsed as "B ==> A"
        Visit(implication.E0, Flip(context), collector);
        Visit(implication.E1, context, collector);
        return;
      case ITEExpr conditional:
        Visit(conditional.Test, Polarity.Mixed, collector);
        Visit(conditional.Thn, context, collector);
        Visit(conditional.Els, context, collector);
        return;
      case QuantifierExpr quantifier: {
          var universal = quantifier is ForallExpr;
          for (var i = 0; i < quantifier.BoundVars.Count; i++) {
            var boundVar = quantifier.BoundVars[i];
            if (RangesOverAValue(boundVar, quantifier.Bounds?.ElementAtOrDefault(i))) {
              continue;
            }
            AddType(boundVar.Type, Times(context, universal ? Polarity.Negative : Polarity.Positive), boundVar.Origin,
              $"the {(universal ? "universal" : "existential")} quantifier over '{boundVar.Name}: {boundVar.Type}'", collector);
          }
          // "forall x | R :: P" is "forall x :: R ==> P", and "exists x | R :: P" is "exists x :: R && P"
          Visit(quantifier.Range, universal ? Flip(context) : context, collector);
          Visit(quantifier.Term, context, collector);
          return;
        }
      case ComprehensionExpr comprehension: {
          var what = comprehension switch {
            LambdaExpr => "lambda expression",
            MapComprehension => "map comprehension",
            _ => "set comprehension"
          };
          for (var i = 0; i < comprehension.BoundVars.Count; i++) {
            var boundVar = comprehension.BoundVars[i];
            if (RangesOverAValue(boundVar, comprehension.Bounds?.ElementAtOrDefault(i))) {
              continue;
            }
            AddType(boundVar.Type, Polarity.Mixed, boundVar.Origin, $"the {what} over '{boundVar.Name}: {boundVar.Type}'", collector);
          }
          VisitMixed(comprehension.SubExpressions, collector);
          return;
        }
      case LetExpr let:
        if (!let.Exact) {
          foreach (var boundVar in let.BoundVars) {
            AddType(boundVar.Type, Polarity.Mixed, boundVar.Origin, $"the let-such-that expression over '{boundVar.Name}: {boundVar.Type}'", collector);
          }
        }
        VisitMixed(let.RHSs, collector);
        Visit(let.Body, context, collector);
        return;
      case NestedMatchExpr match:
        Visit(match.Source, Polarity.Mixed, collector);
        foreach (var matchCase in match.Cases) {
          VisitMixed(matchCase.Pat.SubExpressions, collector);
          Visit(matchCase.Body, context, collector);
        }
        return;
      case StmtExpr stmtExpr:
        Visit(stmtExpr.E, context, collector);
        return;
      case TypeTestExpr test:
        AddType(test.ToType, Times(context, Polarity.Positive), test.Origin, $"the type test against '{test.ToType}'", collector);
        Visit(test.E, Polarity.Mixed, collector);
        return;
      case FunctionCallExpr { Function: { } function } call:
        if (function.EnclosingClass is not ArrowTypeDecl) {
          // f.requires(x) and f.reads(x), for a function value f, do not depend on the type arguments
          var typeArgs = (call.TypeApplication_AtEnclosingClass ?? []).Concat(call.TypeApplication_JustFunction ?? []).ToList();
          AddCall(function, typeArgs, context, call.Origin, CallDescription(function), collector);
        }
        VisitMixed(call.SubExpressions, collector);
        return;
      case MemberSelectExpr { Member: Function function } select: {
          var typeArgs = (select.TypeApplicationAtEnclosingClass ?? []).Concat(select.TypeApplicationJustMember ?? []);
          foreach (var typeArg in typeArgs) {
            AddType(typeArg, Polarity.Mixed, select.Origin, $"the function '{function.Name}', used as a value,", collector);
          }
          Visit(select.Obj, Polarity.Mixed, collector);
          return;
        }
      case MemberSelectExpr { Member: ConstantField { Rhs: not null } constant } select:
        AddCall(constant, select.TypeApplicationAtEnclosingClass ?? [], context, select.Origin, $"the constant '{constant.Name}'", collector);
        Visit(select.Obj, Polarity.Mixed, collector);
        return;
      case UnaryOpExpr { Op: UnaryOpExpr.Opcode.Allocated } allocated:
        AddType(allocated.E.Type, Polarity.Mixed, allocated.Origin, "the allocated expression", collector);
        Visit(allocated.E, Polarity.Mixed, collector);
        return;
      default:
        VisitMixed(expr.SubExpressions, collector);
        return;
    }
  }

  /// <summary>
  /// Whether "boundVar" ranges over the values of an expression whose type is the type of "boundVar": the elements of
  /// a collection, one value, or the subsets of a set, by the bound "bound" that the resolver found for it (the
  /// bounds that the extension condition accepts, section 3.3 of the plan).  Every value of that expression is then a
  /// value of the variable's type at every instantiation, so the binder ranges over the values of the expression
  /// alone, which do not depend on the set of values of the variable's type.  The types must be the same, constraints
  /// included: a variable of a subset type of the elements ranges over those elements that satisfy its constraint.
  /// (Issue 105, stage 7 of section 7.2 of the plan.)
  /// </summary>
  private static bool RangesOverAValue(BoundVar boundVar, BoundedPool? bound) {
    var source = bound switch {
      CollectionBoundedPool collection => collection.CollectionElementType,
      ExactBoundedPool exact => exact.E.Type,
      SubSetBoundedPool subset => subset.UpperBound.Type,
      _ => null
    };
    return source != null && source.Equals(boundVar.Type, true);
  }

  private void VisitMixed(IEnumerable<Expression> exprs, Collector collector) {
    foreach (var expr in exprs) {
      Visit(expr, Polarity.Mixed, collector);
    }
  }

  private string CallDescription(Function function) {
    return Body(function) == null
      ? $"the call of '{function.Name}', whose body is not known here,"
      : $"the call of '{function.Name}'";
  }

  // ----- the polarities of functions and constants --------------------------------------------------------------

  /// <summary>
  /// The body that defines "member", a function or a constant, for this analysis, or null if it is not known: the
  /// function is abstract, bodiless or dynamically dispatched (a trait's function without a body), or its body is not
  /// revealed to the module analysed.  A function with a body cannot be overridden, so a call of it is bound statically.
  /// </summary>
  private Expression? Body(MemberDecl member) {
    var body = member switch {
      Function function => function.Body,
      ConstantField constant => constant.Rhs,
      _ => null
    };
    if (body == null) {
      return null;
    }
    var home = member.EnclosingClass?.EnclosingModuleDefinition;
    return home == module || member.IsRevealedInScope(scope) ? body : null;
  }

  private Dictionary<TypeParameter, Polarity> Summary(MemberDecl member) {
    if (!summaries.TryGetValue(member, out var summary)) {
      summary = new Dictionary<TypeParameter, Polarity>();
      summaries[member] = summary;
    }
    return summary;
  }

  private Dictionary<TypeParameter, Polarity> ComputeSummary(MemberDecl member) {
    var formals = FormalTypeParameters(member);
    var body = Body(member);
    if (body == null) {
      return formals.ToDictionary(tp => tp, _ => Polarity.Mixed);
    }
    var resultType = member switch {
      Function function => function.ResultType,
      _ => ((ConstantField)member).Type
    };
    var collector = new Collector(this, formals);
    Visit(body, resultType.IsBoolType ? Polarity.Positive : Polarity.Mixed, collector);
    return formals.ToDictionary(tp => tp, tp => collector.Uses.Of(tp));
  }

  /// <summary>
  /// Compute the polarities of the functions and constants reached so far, and of those they reach, as the least
  /// fixpoint of their bodies, starting from None: recursion adds nothing that the body does not.
  /// </summary>
  private void Solve() {
    while (true) {
      var count = summaries.Count;
      var changed = false;
      foreach (var member in summaries.Keys.ToList()) {
        var old = summaries[member];
        var updated = ComputeSummary(member);
        foreach (var (typeParameter, polarity) in updated) {
          var joined = old.GetValueOrDefault(typeParameter) | polarity;
          if (joined != old.GetValueOrDefault(typeParameter)) {
            old[typeParameter] = joined;
            changed = true;
          }
        }
      }
      if (!changed && summaries.Count == count) {
        return;
      }
    }
  }

  // ----- the check for types with member invariants -------------------------------------------------------------

  /// <summary>
  /// Issue 105: a type with member invariants has the variance that its declaration gives each type parameter, so a
  /// covariant (or contravariant) type parameter requires the invariants, own and inherited, to be monotone (or
  /// antitone) in it.  Report each type parameter for which they are not, at its declaration, with the occurrence that
  /// uses it the other way.  "raw D" keeps its declared variance in any case.
  /// </summary>
  public static void CheckInvariantVariance(IEnumerable<TopLevelDecl> declarations, VisibilityScope scope, ErrorReporter reporter) {
    var analyses = new Dictionary<ModuleDefinition, TypeParameterPolarity>();
    foreach (var decl in declarations.OfType<TopLevelDeclWithMembers>()) {
      if (decl.InvariantTypeDecl is not { } invariantTypeDecl) {
        continue;
      }
      var variant = decl.TypeArgs.Where(tp => tp.Variance != TypeParameter.TPVariance.Non).ToList();
      if (variant.Count == 0) {
        continue;
      }
      var module = decl.EnclosingModuleDefinition;
      if (!analyses.TryGetValue(module, out var analysis)) {
        analysis = new TypeParameterPolarity(module, scope);
        analyses[module] = analysis;
      }
      var uses = analysis.Analyze(variant, collector => {
        foreach (var (invariant, receiver) in invariantTypeDecl.InheritedInvariants) {
          collector.Call(invariant, receiver.TypeArgs, Polarity.Positive, invariant.Origin,
            $"the invariant that '{decl.Name}' inherits from '{invariant.EnclosingClass.Name}'");
        }
        foreach (var invariant in InvariantTypeDecl.Invariants(decl)) {
          collector.Formula(invariant.Body, Polarity.Positive);
        }
      });
      foreach (var typeParameter in variant) {
        var covariant = typeParameter.Variance == TypeParameter.TPVariance.Co;
        var occurrence = covariant ? uses.Negative(typeParameter) : uses.Positive(typeParameter);
        if (occurrence == null) {
          continue;
        }
        reporter.Error(MessageSource.Resolver, new NestedOrigin(typeParameter.Origin, occurrence.Origin, "the occurrence"),
          $"type parameter '{typeParameter.Name}' of '{decl.Name}' is declared {(covariant ? "covariant" : "contravariant")}, " +
          $"but the invariants of '{decl.Name}' are not {(covariant ? "monotone" : "antitone")} in it: " +
          $"{occurrence.Description} uses '{typeParameter.Name}' {Describe(occurrence.Polarity)}");
      }
    }
  }
}
