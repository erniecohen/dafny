using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// A LemmaResultExpr denotes one result of a lemma at given arguments: out-parameter "OutIndex" of "Lemma",
/// called on "Receiver" with "Args".  The value is a fixed function of the lemma, its type arguments, its receiver
/// and its in-arguments (and of the heap, if the lemma's postcondition depends on the heap), and it satisfies the
/// lemma's postcondition wherever a call of the lemma at those arguments has been checked.
///
/// The node is produced only by resolution, from the expression "var y1, ..., yk := L(args); e" under
/// --lemma-bindings (see LetExpr.LemmaBinding), which also places a call of the lemma, as a statement, in front of
/// the result expressions.  The node itself carries no proof obligation; its meaning comes from that call.
/// </summary>
public class LemmaResultExpr : Expression, ICloneable<LemmaResultExpr> {
  public readonly Lemma Lemma;
  public Expression Receiver;
  public ActualBindings Bindings;
  public List<Expression> Args => Bindings.Arguments;
  public readonly int OutIndex;
  [FilledInDuringResolution] public List<PreType> PreTypeApplicationAtEnclosingClass;
  [FilledInDuringResolution] public List<PreType> PreTypeApplicationJustMember;
  [FilledInDuringResolution] public List<Type> TypeApplicationAtEnclosingClass;
  [FilledInDuringResolution] public List<Type> TypeApplicationJustMember;

  public Formal Out => Lemma.Outs[OutIndex];

  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(Lemma != null);
    Contract.Invariant(Receiver != null);
    Contract.Invariant(Bindings != null);
    Contract.Invariant(0 <= OutIndex && OutIndex < Lemma.Outs.Count);
  }

  public LemmaResultExpr(IOrigin origin, Lemma lemma, Expression receiver, ActualBindings bindings, int outIndex)
    : base(origin) {
    Contract.Requires(origin != null);
    Contract.Requires(lemma != null);
    Contract.Requires(receiver != null);
    Contract.Requires(bindings != null);
    Contract.Requires(0 <= outIndex && outIndex < lemma.Outs.Count);
    Lemma = lemma;
    Receiver = receiver;
    Bindings = bindings;
    OutIndex = outIndex;
  }

  /// <summary>
  /// This constructor is intended to be used when constructing a resolved LemmaResultExpr. The "args" are expected
  /// to be already resolved, and are all given positionally.
  /// </summary>
  public LemmaResultExpr(IOrigin origin, Lemma lemma, Expression receiver, List<Expression> args, int outIndex,
    List<Type> typeApplicationAtEnclosingClass, List<Type> typeApplicationJustMember)
    : this(origin, lemma, receiver, new ActualBindings(args.ConvertAll(e => new ActualBinding(null, e))), outIndex) {
    Bindings.AcceptArgumentExpressionsAsExactParameterList();
    TypeApplicationAtEnclosingClass = typeApplicationAtEnclosingClass;
    TypeApplicationJustMember = typeApplicationJustMember;
  }

  public LemmaResultExpr Clone(Cloner cloner) {
    return new LemmaResultExpr(cloner, this);
  }

  public LemmaResultExpr(Cloner cloner, LemmaResultExpr original) : base(cloner, original) {
    Lemma = original.Lemma;
    Receiver = cloner.CloneExpr(original.Receiver);
    Bindings = new ActualBindings(cloner, original.Bindings);
    OutIndex = original.OutIndex;
    if (cloner.CloneResolvedFields) {
      TypeApplicationAtEnclosingClass = original.TypeApplicationAtEnclosingClass;
      TypeApplicationJustMember = original.TypeApplicationJustMember;
    }
  }

  /// <summary>
  /// Returns a mapping from the type parameters of the lemma and of its enclosing class (and of that class's
  /// parents) to the actual type arguments.  To be called only after successful resolution.
  /// </summary>
  public Dictionary<TypeParameter, Type> TypeArgumentSubstitutionsWithParents() {
    Contract.Requires(WasResolved());
    return MemberSelectExpr.TypeArgumentSubstitutionsWithParentsAux(Receiver.Type, Lemma, TypeApplicationJustMember);
  }

  public override IEnumerable<Expression> SubExpressions {
    get {
      yield return Receiver;
      foreach (var e in Args) {
        yield return e;
      }
    }
  }

  public override IEnumerable<Type> ComponentTypes => Util.Concat(TypeApplicationAtEnclosingClass, TypeApplicationJustMember);
}
