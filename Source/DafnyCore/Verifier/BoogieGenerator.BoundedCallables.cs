using System.Collections.Generic;
using System.Linq;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny;

/// <summary>
/// The Boogie encoding of the built-in type "bounded&lt;O, F&gt;" of --modular-termination (issue 266, M2).
/// A value of the type is a box, built only by "bounded.certify(a, f)", which translates to "make(Box(a), Box(f))";
/// its members "bound" and "erase" are the two projections.
///
/// Soundness: interpret "$IsBox(c, bounded(tO, tF))" as "$IsBox(bound(c), tO) &amp;&amp; $IsBox(erase(c), tF)" and
/// "$IsAllocBox(c, bounded(tO, tF), h)" likewise with "$IsAllocBox".  Under that interpretation every axiom below
/// holds, and so does the typing fact that the well-formedness check of "bounded.certify" assumes after checking
/// both arguments against their formal types.  No axiom says that "make" has the type, so a value of the type
/// cannot be made except by "bounded.certify", whose function argument is checked to run under the bound.
/// </summary>
public partial class BoogieGenerator {
  private Bpl.Function boundedBound, boundedErase, boundedMake;

  void AddBoundedCallableDeclarations(AbstractTypeDecl decl) {
    var tok = decl.Origin;
    var tyCon = GetOrCreateTypeConstructor(decl);
    boundedBound = GetReadonlyField(decl.Members.OfType<SpecialField>().Single(f => f.Name == BoundedCallables.BoundName));
    boundedErase = GetReadonlyField(decl.Members.OfType<SpecialField>().Single(f => f.Name == BoundedCallables.EraseName));
    boundedMake = new Bpl.Function(tok, decl.FullSanitizedName + ".make", [],
      [BplFormalVar(null, Predef.BoxType, true), BplFormalVar(null, Predef.BoxType, true)],
      BplFormalVar(null, Predef.BoxType, false), null, null);
    sink.AddTopLevelDeclaration(boundedBound);
    sink.AddTopLevelDeclaration(boundedErase);
    sink.AddTopLevelDeclaration(boundedMake);

    Bpl.Expr Call(Bpl.Function f, params Bpl.Expr[] args) => new Bpl.NAryExpr(tok, new Bpl.FunctionCall(f), args.ToList());

    // axiom (forall b, h :: { make(b, h) } bound(make(b, h)) == b && erase(make(b, h)) == h);
    {
      var bvars = new List<Bpl.Variable>();
      var b = BplBoundVar("b", Predef.BoxType, bvars);
      var h = BplBoundVar("h", Predef.BoxType, bvars);
      var made = Call(boundedMake, b, h);
      var body = BplAnd(Bpl.Expr.Eq(Call(boundedBound, made), b), Bpl.Expr.Eq(Call(boundedErase, made), h));
      AddOtherDefinition(boundedMake, new Bpl.Axiom(tok, BplForall(bvars, BplTrigger(made), body), "bounded.certify: projections"));
    }

    // axiom (forall c, tO, tF :: { P(c), $IsBox(c, T(tO, tF)) } $IsBox(c, T(tO, tF)) ==> $IsBox(P(c), tX));
    // axiom (forall c, tO, tF, h :: { P(c), $IsAllocBox(c, T(tO, tF), h) } $IsAllocBox(c, T(tO, tF), h) ==> $IsAllocBox(P(c), tX, h));
    // for each projection P with type tX
    foreach (var (projection, index) in new[] { (boundedBound, 0), (boundedErase, 1) }) {
      var bvars = new List<Bpl.Variable>();
      var c = BplBoundVar("c", Predef.BoxType, bvars);
      var tO = BplBoundVar("tO", Predef.Ty, bvars);
      var tF = BplBoundVar("tF", Predef.Ty, bvars);
      var type = Call(tyCon, tO, tF);
      var projected = Call(projection, c);
      var componentType = index == 0 ? tO : tF;
      var isC = MkIs(c, type, true);
      var typing = BplForall(bvars, new Bpl.Trigger(tok, true, [projected, isC]),
        BplImp(isC, MkIs(projected, componentType, true)));
      AddOtherDefinition(projection, new Bpl.Axiom(tok, typing, $"bounded.{projection.Name}: Type axiom"));
      var heap = BplBoundVar("$h", Predef.HeapType, bvars);
      var isAllocC = MkIsAlloc(c, type, heap, true);
      var allocation = BplForall(bvars, new Bpl.Trigger(tok, true, [projected, isAllocC]),
        BplImp(isAllocC, MkIsAlloc(projected, componentType, heap, true)));
      AddOtherDefinition(projection, new Bpl.Axiom(tok, allocation, $"bounded.{projection.Name}: Allocation axiom"));
    }
  }

  /// <summary>
  /// The translation of "bounded.certify(a, f)": make(Box(a), Box(f)).
  /// </summary>
  Bpl.Expr TrBoundedCertify(Bpl.IToken tok, Bpl.Expr bound, Type boundType, Bpl.Expr function, Type functionType) {
    return new Bpl.NAryExpr(tok, new Bpl.FunctionCall(boundedMake),
      [BoxIfNecessary(tok, bound, boundType), BoxIfNecessary(tok, function, functionType)]);
  }
}

public partial class BoogieGenerator {
  /// <summary>
  /// "bounded.certify(a, f)" is well formed when its arguments are, have their formal types, and "f" runs under
  /// "a": a lambda expression's body is checked under it, and any other value's bound must not exceed it.  The
  /// value then has its type, which no axiom gives to "make" (see AddBoundedCallableDeclarations).
  /// </summary>
  void CheckWellformedBoundedCertify(FunctionCallExpr expr, WFOptions options, Variables locals,
                                     BoogieStmtListBuilder builder, ExpressionTranslator etran) {
    var typeMap = expr.GetTypeArgumentSubstitutions();
    RegisterCallbackLambdas(expr.Function, expr.Receiver, expr.Args, typeMap, etran);
    var substMap = new Dictionary<IVariable, Expression>();
    for (var i = 0; i < expr.Args.Count; i++) {
      var arg = expr.Args[i];
      var formal = expr.Function.Ins[i];
      CheckWellformed(arg, options, locals, builder, etran);
      CheckSubrange(arg.Origin, etran.TrExpr(arg), arg.Type, formal.Type.Subst(typeMap), arg, builder, etran: etran);
      substMap.Add(formal, arg);
    }
    CheckCallbackArguments(expr.Origin, codeContext, expr.Function, expr.Args, expr.Receiver, substMap, typeMap,
      options.CallerTerminationTranslator ?? builder.Context.CallerTerminationTranslator ?? etran, builder);
    var type = UserDefinedType.FromTopLevelDecl(expr.Origin, expr.Function.EnclosingClass).Subst(typeMap);
    builder.Add(TrAssumeCmd(expr.Origin, MkIs(etran.TrExpr(expr), type)));
  }
}

public partial class BoogieGenerator {
  /// <summary>
  /// Under --modular-termination, at an application of "X.erase" to "args" (or a call of its "requires" or "reads"),
  /// where "bounded" is "X": the bound of "X" at "args" must be below the bound under which the application runs, that of the
  /// lambda around it, or else the caller's global bound on entry (smaller-bound rule).  Code without a bound is
  /// above every bound.
  /// </summary>
  void CheckBoundedApplication(IOrigin tok, ICodeContext codeContext, Expression bounded, List<Expression> args,
                               ExpressionTranslator etranCurrent, BoogieStmtListBuilder builder) {
    if (codeContext is not ICallable caller) {
      return;
    }
    var lambdaBound = CurrentLambdaBound;
    var callerBound = InsideLambda ? lambdaBound : GlobalBound(caller);
    if (callerBound == null) {
      return;
    }
    var oldCaller = !InsideLambda && codeContext is MethodOrConstructor and not TwoStateLemma;
    CheckCallTermination(tok, callerBound, [BoundedCallables.BoundAt(bounded, args)], null, null, new Dictionary<IVariable, Expression>(),
      new Dictionary<IVariable, Expression>(), new Dictionary<TypeParameter, Type>(), etranCurrent, oldCaller, builder, false,
      InsideLambda
        ? "--modular-termination: the bound of the bounded value must be below the bound under which the enclosing lambda runs"
        : "--modular-termination: the bound of the bounded value must be below the caller's 'decreases {:global}' bound");
  }

  /// <summary>
  /// Under --modular-termination, at a call of "callee": "X.erase", passed for a parameter with a callback bound
  /// (and so for the function of "bounded.certify"), must not go to a larger bound: the bound of "X" must be at
  /// most the parameter's, at the call's arguments.  Unlike a parameter with a callback bound, "X.bound" is a value
  /// of the current state, so this holds inside a lambda too.
  /// </summary>
  void CheckBoundedArguments(IOrigin tok, MemberDecl callee, List<Expression> args, Expression receiverReplacement,
                             Dictionary<IVariable, Expression> substMap, Dictionary<TypeParameter, Type> typeMap,
                             ExpressionTranslator etranCurrent, BoogieStmtListBuilder builder) {
    if (!ModularTerminationActive || callee is not ICallable callable) {
      return;
    }
    for (var i = 0; i < args.Count && i < callable.Ins.Count; i++) {
      if (CallbackBound(callable, i) is not { } calleeBound) {
        continue;
      }
      List<Expression> lower;
      string what;
      if (BoundedCallables.ErasedCallable(args[i].Resolved) is { } bounded) {
        lower = [BoundOf(bounded)];
        what = "the bound of the bounded value";
      } else if (BoundedCallables.IsCertify(callee) && AdmittedMemberBound(args[i]) is { } memberBound) {
        lower = memberBound;
        what = "the 'decreases {:global}' bound of the certified function";
      } else {
        continue;
      }
      var upper = calleeBound.ConvertAll(e =>
        DecreasesOrders.WithTypeOrder(Substitute(e, receiverReplacement, substMap, typeMap), e.Type.Subst(typeMap)));
      var contextBound = upper;
      var comparedBound = lower;
      if (!UseConsistentDecreasesOrder) {
        AtMost(tok, upper, lower, out contextBound, out comparedBound);
      }
      CheckCallTermination(tok, contextBound, comparedBound, null, null, new Dictionary<IVariable, Expression>(),
        new Dictionary<IVariable, Expression>(), new Dictionary<TypeParameter, Type>(), etranCurrent, false, builder, false,
        BoundedCallables.IsCertify(callee)
          ? $"--modular-termination: {what} must not exceed the bound it is certified under"
          : $"--modular-termination: {what} must not exceed the 'decreases {{:callback}}' bound of the parameter '{callable.Ins[i].Name}' it is passed for",
        nonincrease: true);
    }
  }

  /// <summary>
  /// If "value" names a function that "bounded.certify" admits as an exact implementation and that can reach
  /// dispatch, its global bound, with its receiver for "this"; otherwise null.  A function that reaches no dispatched
  /// call runs no code with a bound, so it fits any bound.
  /// </summary>
  List<Expression> AdmittedMemberBound(Expression value) {
    if (ModularTermination.AdmittedMember(options, value, out _) is not { } g ||
        !ModularTerminationAnalysis.Of(program).ReachesOpenDispatch(g, codeContext?.EnclosingModule) ||
        GlobalBound(g) is not { } bound) {
      return null;
    }
    var select = (MemberSelectExpr)value.Resolved;
    var typeMap = select.TypeArgumentSubstitutionsWithParents();
    return bound.ConvertAll(e => Substitute(e, g.IsStatic ? null : select.Obj, new Dictionary<IVariable, Expression>(), typeMap));
  }

  /// <summary>
  /// The expression "X.bound", for a value "X" of a bounded type.
  /// </summary>
  static Expression BoundOf(Expression bounded) {
    return BoundedCallables.Select(bounded, bounded.Type, BoundedCallables.BoundName)!;
  }
}
