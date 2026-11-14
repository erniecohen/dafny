using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Bpl = Microsoft.Boogie;
using BplParser = Microsoft.Boogie.Parser;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Boogie;
using static Microsoft.Dafny.Util;
using Core;
using DafnyCore.Verifier;
using Microsoft.BaseTypes;
using Microsoft.Dafny.Compilers;
using Microsoft.Dafny.Triggers;
using Action = System.Action;
using PODesc = Microsoft.Dafny.ProofObligationDescription;
using static Microsoft.Dafny.GenericErrors;

namespace Microsoft.Dafny;

public partial class BoogieGenerator {

  bool UseConsistentDecreasesOrder => !options.Get(CommonOptionBag.LegacyDecreasesOrder);

  bool CompatibleDecreasesTypesForComparison(Type t, Type u) {
    if (!UseConsistentDecreasesOrder) {
      return CompatibleDecreasesTypes(t, u);
    }
    t = t.NormalizeToAncestorType();
    u = u.NormalizeToAncestorType();
    if (t.IsTypeParameter || t.IsAbstractType || t.IsInternalTypeSynonym) {
      return t.Equals(u); // equality-only order within this opaque family
    }
    if (UseTraitRankDecreases && IsStructurallyRanked(t) && IsStructurallyRanked(u) &&
        (IsValueTraitType(t) || IsValueTraitType(u))) {
      // --trait-rank-decreases: a value of a trait that is not a reference type joins the
      // structural-rank order of inductive datatypes and sequences, through BoxRank.  See
      // docs/dev/design/317-datatype-trait-decreases.md.
      return true;
    }
    if (!CompatibleDecreasesTypes(t, u)) {
      return false;
    }
    // These representations have no shared component order. In particular, do not
    // apply a bitvector operator of one width to an operand of another width.
    return (t is not BitvectorType a || u is not BitvectorType b || a.Width == b.Width) &&
      (t is not SetType c || u is not SetType d || c.Finite == d.Finite) &&
      (!t.IsCoDatatype && !u.IsCoDatatype || t.IsCoDatatype && u.IsCoDatatype);
  }

  bool UseTraitRankDecreases => UseConsistentDecreasesOrder && options.Get(CommonOptionBag.TraitRankDecreases);

  /// <summary>
  /// A trait that is not a reference type, whose values are boxed, so BoxRank gives their structural rank.
  /// </summary>
  static bool IsValueTraitType(Type t) {
    return t.IsTraitType && !t.IsRefType;
  }

  /// <summary>
  /// Under --trait-rank-decreases, the types whose decreases order is the structural rank: sequences,
  /// inductive datatypes and traits that are not reference types.  "t" is normalized to its ancestor type.
  /// </summary>
  static bool IsStructurallyRanked(Type t) {
    return t is SeqType || t.IsIndDatatype || IsValueTraitType(t);
  }

  /// <summary>
  /// Emit to "builder" a check that calleeDecreases is less than contextDecreases.  More precisely,
  /// the check is:
  ///     allowance || (calleeDecreases LESS contextDecreases).
  /// contextDecreases is translated by etranCurrent, in its old state if oldCaller, and calleeDecreases
  /// by etranCallee if it is given, else by etranCurrent.
  /// </summary>
  void CheckCallTermination(IOrigin tok, List<Expression> contextDecreases, List<Expression> calleeDecreases,
                            Expression allowance,
                            Expression receiverReplacement, Dictionary<IVariable, Expression> substMap,
                            Dictionary<IVariable, Expression> directSubstMap,
                            Dictionary<TypeParameter, Type> typeMap,
                            ExpressionTranslator etranCurrent, bool oldCaller, BoogieStmtListBuilder builder, bool inferredDecreases, string hint,
                            ExpressionTranslator etranCallee = null, bool nonincrease = false) {
    Contract.Requires(tok != null);
    Contract.Requires(Cce.NonNullElements(contextDecreases));
    Contract.Requires(Cce.NonNullElements(calleeDecreases));
    Contract.Requires(Cce.NonNullDictionaryAndValues(substMap));
    Contract.Requires(etranCurrent != null);
    Contract.Requires(builder != null);
    etranCallee ??= etranCurrent;

    // The interpretation of the given decreases-clause expression tuples is as a lexicographic tuple, extended into
    // an infinite tuple by appending TOP elements.  The TOP element is strictly larger than any other value given
    // by a Dafny expression.  Each Dafny types has its own ordering, and these orderings are combined into a partial
    // order where elements from different Dafny types are incomparable.  Thus, as an optimization below, if two
    // components from different types are compared, the answer is taken to be false.

    if (Contract.Exists(calleeDecreases, e => e is WildcardExpr)) {
      // no check needed
      return;
    }

    int N = Math.Min(contextDecreases.Count, calleeDecreases.Count);
    var toks = new List<IOrigin>();
    var callee = new List<Expr>();
    var caller = new List<Expr>();
    var oldExpressions = new List<Expression>();
    var newExpressions = new List<Expression>();
    Bpl.Expr canCalls = Bpl.Expr.True;
    if (tok.IsInherited(currentModule) && contextDecreases.All(e => !e.Origin.IsInherited(currentModule))) {
      // the call site is inherited but all the context decreases expressions are new
      tok = new ForceCheckOrigin(tok);
    }
    for (int i = 0; i < N; i++) {
      Expression e0 = Substitute(calleeDecreases[i], receiverReplacement, substMap, typeMap);
      Expression e0direct = Substitute(calleeDecreases[i], receiverReplacement, directSubstMap, typeMap);
      Expression e1 = contextDecreases[i];
      if (oldCaller) {
        e1 = new OldExpr(e1.Origin, e1) {
          Type = e1.Type // To ensure that e1 stays resolved
        };
      }
      if (!CompatibleDecreasesTypesForComparison(e0.Type, e1.Type)) {
        N = i;
        break;
      }
      oldExpressions.Add(e1);
      // The comparison takes its types from these expressions, and "callee" below translates e0.  A trait-typed
      // formal can be given an argument of a subtype, which e0direct has and whose representation differs (a
      // datatype value, not a box), so under --trait-rank-decreases the comparison uses e0.  Otherwise the two
      // types are of one family; the diagnostic below uses e0direct either way.
      newExpressions.Add(UseTraitRankDecreases ? e0 : e0direct);
      toks.Add(new NestedOrigin(tok, e1.Origin, "this decreases clause was not satisfied"));
      canCalls = BplAnd(canCalls, etranCurrent.CanCallAssumptionForVerification(e1));
      canCalls = BplAnd(canCalls, etranCallee.CanCallAssumptionForVerification(e0direct));
      callee.Add(etranCallee.TrExpr(e0));
      caller.Add(etranCurrent.TrExpr(e1));
    }
    bool endsWithWinningTopComparison = N == contextDecreases.Count && N < calleeDecreases.Count;
    builder.Add(TrAssumeCmd(tok, canCalls));
    // At the first unpaired coordinate, proper < TOP; TOP is weakly below only TOP.
    // An incompatible pair instead stops the comparison with false. This terminal
    // value is lifted with the same strict/weak recurrence as every other component.
    bool tailComparison = N == contextDecreases.Count &&
      (N < calleeDecreases.Count || nonincrease && N == calleeDecreases.Count);
    Bpl.Expr decrExpr = DecreasesCheck(toks, null, newExpressions, oldExpressions, callee, caller, builder, "",
      endsWithWinningTopComparison, false, tailComparison);
    if (allowance != null) {
      decrExpr = BplOr(etranCurrent.TrExpr(allowance), decrExpr);
    }
    if (UseConsistentDecreasesOrder) {
      // Report the actual padded-tuple comparison, rather than the truncated
      // prefix used by the legacy diagnostic adapter.
      oldExpressions = contextDecreases.ConvertAll(e => oldCaller
        ? (Expression)new OldExpr(e.Origin, e) { Type = e.Type } : e);
      newExpressions = calleeDecreases.ConvertAll(e => Substitute(e, receiverReplacement, directSubstMap, typeMap));
    }
    builder.Add(Assert(tok, decrExpr, new
      Terminates(inferredDecreases, null, allowance,
        oldExpressions, newExpressions, UseConsistentDecreasesOrder ? nonincrease : endsWithWinningTopComparison, hint), builder.Context));
  }

  /// <summary>
  /// Under --modular-termination, the global bound of "callable" (its "decreases {:global}" clause, or
  /// else that of the member it overrides, recursively), in terms of its own formals; null if it has none.
  /// The method of a function's "by method" body has the function's bound.
  /// </summary>
  List<Expression> GlobalBound(ICallable callable) {
    if (callable is Method { FunctionFromWhichThisIsByMethodDecl: { } function }) {
      return GlobalBound(function);
    }
    var declared = ModularTermination.DeclaredGlobalBound(callable);
    if (declared != null) {
      return declared;
    }
    if (callable is not MemberDecl { OverriddenMember: ICallable parent } member ||
        GlobalBound(parent) is not { } parentBound) {
      return null;
    }
    var substMap = new Dictionary<IVariable, Expression>();
    for (var i = 0; i < callable.Ins.Count; i++) {
      substMap.Add(parent.Ins[i], Expression.CreateIdentExpr(callable.Ins[i]));
    }
    var typeMap = GetTypeArgumentSubstitutionMap((MemberDecl)parent, member);
    var sub = new FunctionCallSubstituter(substMap, typeMap, (TraitDecl)((MemberDecl)parent).EnclosingClass,
      (TopLevelDeclWithMembers)member.EnclosingClass);
    return parentBound.ConvertAll(sub.Substitute);
  }

  /// <summary>
  /// Under --modular-termination, emit to "builder" the check of the global bounds for a call from "codeContext"
  /// to "callee".  The caller's bound is captured on entry, and a missing bound is TOP, above every other.
  /// * A call dispatched to implementations that other modules may provide uses the smaller-bound rule: the
  ///   callee's bound must be below the caller's.
  /// * A call to a known callee that reaches such a dispatch (see <see cref="ModularTerminationAnalysis"/>) uses
  ///   the same-bound rule: the callee runs under the caller's bound, so its own bound must not exceed it.
  /// A call from a caller without a bound needs no check, except that a dispatched callee must have one; a call
  /// from a caller with a bound fails when the callee has none.  Other calls get no check here: the existing termination checks order them.
  /// </summary>
  void CheckModularTermination(IOrigin tok, ICodeContext codeContext, MemberDecl callee, Expression receiverReplacement,
                               Dictionary<IVariable, Expression> substMap, Dictionary<IVariable, Expression> directSubstMap,
                               Dictionary<TypeParameter, Type> typeMap, ExpressionTranslator etranCurrent,
                               BoogieStmtListBuilder builder, ExpressionTranslator etranCallee) {
    if (!options.Get(CommonOptionBag.ModularTermination) || codeContext is not ICallable caller) {
      return;
    }
    var analysis = ModularTerminationAnalysis.Of(program);
    var dispatched = ModularTermination.IsOpenDispatch(callee, analysis.OpenTraits);
    if (!dispatched && !analysis.ReachesOpenDispatch(callee, caller.EnclosingModule)) {
      return;
    }
    var calleeBound = GlobalBound((ICallable)callee);
    // Inside a lambda passed for a parameter with a callback bound (M3b), the caller's bound is that callback bound,
    // a value fixed where the lambda is created.
    var lambdaBound = CurrentLambdaBound;
    var callerBound = lambdaBound ?? GlobalBound(caller);
    if (callerBound == null && (calleeBound != null || !dispatched)) {
      return;
    }
    string hint;
    if (dispatched) {
      hint = calleeBound == null
        ? "the dispatched callee has no 'decreases {:global}' bound, which --modular-termination requires"
        : "--modular-termination: the dispatched callee's 'decreases {:global}' bound must be below the caller's";
    } else {
      hint = calleeBound == null
        ? "the callee can reach a call dispatched to implementations that other modules may provide, so under --modular-termination it runs under the caller's 'decreases {:global}' bound and needs a bound of its own that does not exceed it"
        : "--modular-termination: the callee can reach a call dispatched to implementations that other modules may provide, so its 'decreases {:global}' bound must not exceed the caller's";
    }
    var oldCaller = lambdaBound == null && codeContext is MethodOrConstructor and not TwoStateLemma;
    var contextBound = callerBound ?? [];
    var comparedBound = calleeBound ?? [];
    var nonincrease = !dispatched && calleeBound != null;
    if (!UseConsistentDecreasesOrder && nonincrease) {
      AtMost(tok, callerBound, calleeBound, out contextBound, out comparedBound);
    }
    CheckCallTermination(tok, contextBound, comparedBound, null, receiverReplacement, substMap, directSubstMap, typeMap,
      etranCurrent, oldCaller, builder, false, hint, etranCallee, nonincrease);
  }

  /// <summary>
  /// The bounds to give CheckCallTermination, which checks "below", to check that "lower" is at most "upper" in the
  /// order in which missing components are TOP: a strict decrease of "lower" against the components of "upper"
  /// that it has, if "upper" has more; otherwise no increase in the components of "upper", which an extra
  /// component on the lower side makes CheckCallTermination allow.
  /// </summary>
  static void AtMost(IOrigin tok, List<Expression> upper, List<Expression> lower,
                     out List<Expression> contextBound, out List<Expression> comparedBound) {
    if (lower.Count < upper.Count) {
      contextBound = upper.Take(lower.Count).ToList();
      comparedBound = lower;
    } else {
      contextBound = upper;
      comparedBound = lower.Append(Expression.CreateIntLiteral(tok, 0)).ToList();
    }
  }

  // M3b: the callback bounds of the lambda expressions passed for parameters that have one, snapshotted where the
  // call evaluates its arguments, and the bounds under which the lambda bodies being checked run.
  private readonly Dictionary<LambdaExpr, List<Expression>> modularTerminationLambdaBounds = new(ReferenceEqualityComparer.Instance);
  private readonly List<List<Expression>> modularTerminationLambdaStack = [];

  /// <summary>
  /// The bound under which the code being checked runs, if it is inside a lambda passed for a parameter with a
  /// callback bound (or inside a lambda inside one); null otherwise, when it is the enclosing callable's.
  /// </summary>
  List<Expression> CurrentLambdaBound => modularTerminationLambdaStack.Count == 0 ? null : modularTerminationLambdaStack[^1];

  bool InsideLambda => modularTerminationLambdaStack.Count != 0;

  /// <summary>
  /// Enter the body of "lambda": it runs under its registered callback bound, or else under the current bound.
  /// Outside any lambda, the current bound is the enclosing callable's global bound, which is translated here, by
  /// the translator of the code around the lambda, on entry: the lambda's body is checked in a heap of its own.
  /// </summary>
  void EnterLambda(LambdaExpr lambda, ICodeContext codeContext, ExpressionTranslator etran) {
    if (!options.Get(CommonOptionBag.ModularTermination)) {
      return;
    }
    var bound = modularTerminationLambdaBounds.GetValueOrDefault(lambda) ?? CurrentLambdaBound;
    if (bound == null && !InsideLambda && codeContext is ICallable caller && GlobalBound(caller) is { } callerBound) {
      var oldCaller = codeContext is MethodOrConstructor and not TwoStateLemma;
      bound = callerBound.ConvertAll(e => {
        var onEntry = oldCaller ? new OldExpr(e.Origin, e) { Type = e.Type } : e;
        return DecreasesByExpr.WithOrderOf(e, new BoogieWrapper(etran.TrExpr(onEntry), e.Type));
      });
    }
    modularTerminationLambdaStack.Add(bound);
  }

  void ExitLambda() {
    if (!options.Get(CommonOptionBag.ModularTermination)) {
      return;
    }
    modularTerminationLambdaStack.RemoveAt(modularTerminationLambdaStack.Count - 1);
  }

  /// <summary>
  /// Under --modular-termination, the callback bound of the in-parameter at "index" of "callable" (its
  /// "decreases {:callback f} E" clause, or else that of the member it overrides), in terms of its own formals;
  /// null if it has none.  The method of a function's "by method" body has the function's.
  /// </summary>
  List<Expression> CallbackBound(ICallable callable, int index) {
    if (callable is Method { FunctionFromWhichThisIsByMethodDecl: { } function }) {
      return CallbackBound(function, index);
    }
    if (index >= callable.Ins.Count) {
      return null;
    }
    var declared = ModularTermination.DeclaredCallbackBound(callable, callable.Ins[index]);
    if (declared != null) {
      return declared;
    }
    if (callable is not MemberDecl { OverriddenMember: ICallable parent } member ||
        CallbackBound(parent, index) is not { } parentBound) {
      return null;
    }
    var substMap = new Dictionary<IVariable, Expression>();
    for (var i = 0; i < callable.Ins.Count; i++) {
      substMap.Add(parent.Ins[i], Expression.CreateIdentExpr(callable.Ins[i]));
    }
    var typeMap = GetTypeArgumentSubstitutionMap((MemberDecl)parent, member);
    var sub = new FunctionCallSubstituter(substMap, typeMap, (TraitDecl)((MemberDecl)parent).EnclosingClass,
      (TopLevelDeclWithMembers)member.EnclosingClass);
    return parentBound.ConvertAll(sub.Substitute);
  }

  /// <summary>
  /// Under --modular-termination, before a call of "callee" evaluates its arguments: each lambda expression passed
  /// for a parameter with a callback bound gets that bound, at the call's arguments, as the bound under which its
  /// body is checked.  The bound is translated here, in the caller's state, so that the body, checked in a heap of
  /// its own, compares with the value at the call.
  /// </summary>
  void RegisterCallbackLambdas(MemberDecl callee, Expression receiver, List<Expression> args,
                               Dictionary<TypeParameter, Type> typeMap, ExpressionTranslator etran) {
    if (!options.Get(CommonOptionBag.ModularTermination) || callee is not ICallable callable) {
      return;
    }
    Dictionary<IVariable, Expression> directSubstMap = null;
    for (var i = 0; i < args.Count && i < callable.Ins.Count; i++) {
      if (args[i].Resolved is not LambdaExpr lambda || CallbackBound(callable, i) is not { } bound) {
        continue;
      }
      if (directSubstMap == null) {
        directSubstMap = new Dictionary<IVariable, Expression>();
        for (var j = 0; j < args.Count && j < callable.Ins.Count; j++) {
          directSubstMap.Add(callable.Ins[j], args[j]);
        }
      }
      modularTerminationLambdaBounds[lambda] = bound.ConvertAll(e => {
        var actual = Substitute(e, receiver, directSubstMap, typeMap);
        return DecreasesByExpr.WithOrderOf(actual, new BoogieWrapper(etran.TrExpr(actual), actual.Type));
      });
    }
  }

  /// <summary>
  /// Under --modular-termination, at a call of "callee": an in-parameter of the caller with a callback bound, passed
  /// for a parameter of the callee with one, must not go to a larger bound (outside a lambda; the resolver rejects
  /// the other arguments that are neither lambdas nor known code): the caller's callback bound, evaluated on entry,
  /// must be at most the callee's, at the call's arguments.
  /// </summary>
  void CheckCallbackArguments(IOrigin tok, ICodeContext codeContext, MemberDecl callee, List<Expression> args,
                              Expression receiverReplacement, Dictionary<IVariable, Expression> substMap,
                              Dictionary<TypeParameter, Type> typeMap, ExpressionTranslator etranCurrent,
                              BoogieStmtListBuilder builder) {
    if (!options.Get(CommonOptionBag.ModularTermination) || codeContext is not ICallable caller ||
        callee is not ICallable callable || InsideLambda) {
      return;
    }
    for (var i = 0; i < args.Count && i < callable.Ins.Count; i++) {
      if (CallbackBound(callable, i) is not { } calleeBound || args[i].Resolved is not IdentifierExpr { Var: Formal formal }) {
        continue;
      }
      var k = caller.Ins.IndexOf(formal);
      if (k < 0 || CallbackBound(caller, k) is not { } callerBound) {
        continue;
      }
      // Both sides in the caller's terms: the callee's bound at the call's arguments, in the current state; the
      // caller's on entry.
      var upper = calleeBound.ConvertAll(e => Substitute(e, receiverReplacement, substMap, typeMap));
      var oldCaller = codeContext is MethodOrConstructor and not TwoStateLemma;
      var lower = oldCaller ? callerBound.ConvertAll(e => (Expression)new OldExpr(e.Origin, e) { Type = e.Type }) : callerBound;
      var contextBound = upper;
      var comparedBound = lower;
      if (!UseConsistentDecreasesOrder) {
        AtMost(tok, upper, lower, out contextBound, out comparedBound);
      }
      CheckCallTermination(tok, contextBound, comparedBound, null, null, new Dictionary<IVariable, Expression>(),
        new Dictionary<IVariable, Expression>(), new Dictionary<TypeParameter, Type>(), etranCurrent, false, builder, false,
        $"--modular-termination: the 'decreases {{:callback}}' bound of '{formal.Name}' must not exceed that of the parameter it is passed for",
        nonincrease: true);
    }
  }

  /// <summary>
  /// Under --modular-termination, at an application of "value" (or a call of its "requires" or "reads"): if it is
  /// an in-parameter of the caller with a callback bound, and the application is not inside a lambda, the callback
  /// bound must be below the caller's global bound, both evaluated on entry (smaller-bound rule).  A caller without
  /// a bound is above every bound.  Other applications are checked by the resolver.
  /// </summary>
  void CheckCallbackApplication(IOrigin tok, ICodeContext codeContext, Expression value, ExpressionTranslator etranCurrent,
                                BoogieStmtListBuilder builder) {
    if (!options.Get(CommonOptionBag.ModularTermination) || codeContext is not ICallable caller || InsideLambda ||
        value.Resolved is not IdentifierExpr { Var: Formal formal }) {
      return;
    }
    var k = caller.Ins.IndexOf(formal);
    if (k < 0 || CallbackBound(caller, k) is not { } callbackBound || GlobalBound(caller) is not { } callerBound) {
      return;
    }
    var oldCaller = codeContext is MethodOrConstructor and not TwoStateLemma;
    var compared = oldCaller ? callbackBound.ConvertAll(e => (Expression)new OldExpr(e.Origin, e) { Type = e.Type }) : callbackBound;
    CheckCallTermination(tok, callerBound, compared, null, null, new Dictionary<IVariable, Expression>(),
      new Dictionary<IVariable, Expression>(), new Dictionary<TypeParameter, Type>(), etranCurrent, oldCaller, builder, false,
      $"--modular-termination: the 'decreases {{:callback}}' bound of '{formal.Name}' must be below the caller's 'decreases {{:global}}' bound");
  }

  /// <summary>
  /// Returns the expression that says whether or not the decreases function has gone down (if !allowNoChange)
  /// or has gone down or stayed the same (if allowNoChange).
  /// ee0 represents the new values and ee1 represents old values.
  /// If builder is non-null, then the check '0 ATMOST decr' is generated to builder.
  /// Requires all types in types0 and types1 to be non-proxy non-synonym types (that is, callers should invoke NormalizeExpand)
  /// </summary>
  Bpl.Expr DecreasesCheck(List<IOrigin> toks, List<VarDeclStmt> prevGhostLocals,
                          List<Expression> dafny0, List<Expression> dafny1, List<Bpl.Expr> ee0, List<Bpl.Expr> ee1,
                          BoogieStmtListBuilder builder, string suffixMsg, bool allowNoChange, bool includeLowerBound, bool? tailComparison = null) {
    Contract.Requires(Cce.NonNullElements(toks));
    Contract.Requires(Cce.NonNullElements(dafny0));
    Contract.Requires(Cce.NonNullElements(dafny1));
    Contract.Requires(Cce.NonNullElements(ee0));
    Contract.Requires(Cce.NonNullElements(ee1));
    Contract.Requires(Predef != null);
    Contract.Requires(dafny0.Count == dafny1.Count && dafny0.Count == ee0.Count && ee0.Count == ee1.Count);
    Contract.Requires(builder == null || suffixMsg != null);
    Contract.Ensures(Contract.Result<Bpl.Expr>() != null);

    if (UseConsistentDecreasesOrder) {
      return ConsistentDecreasesCheck(toks, dafny0, dafny1, ee0, ee1, tailComparison ?? allowNoChange);
    }

    int N = dafny0.Count;

    // compute eq and less for each component of the lexicographic tuple
    List<Bpl.Expr> Eq = new List<Bpl.Expr>(N);
    List<Bpl.Expr> Less = new List<Bpl.Expr>(N);
    List<Expression> EqDafny = new List<Expression>(N);
    List<Expression> LessDafny = new List<Expression>(N);
    for (int i = 0; i < N; i++) {
      Bpl.Expr less, atmost, eq;
      var ty0 = dafny0[i].Type.NormalizeExpandKeepConstraints();
      var ty1 = dafny1[i].Type.NormalizeExpandKeepConstraints();
      ComputeLessEq(toks[i], ty0, ty1, ee0[i], ee1[i], out less, out atmost, out eq, includeLowerBound);
      Eq.Add(eq);
      EqDafny.Add(Expression.CreateEq(dafny0[i], dafny1[i], dafny0[i].Type.NormalizeExpand()));
      Less.Add(allowNoChange ? atmost : less);
      LessDafny.Add(
        allowNoChange
          ? Expression.CreateAtMost(dafny0[i], dafny1[i])
          : Expression.CreateLess(dafny0[i], dafny1[i]));
    }
    if (builder != null) {
      // check: 0 <= ee1
      // more precisely, for component k of the lexicographic decreases function, check:
      //   ee0[0] < ee1[0] || ee0[1] < ee1[1] || ... || ee0[k-1] < ee1[k-1] || ee0[k] == ee1[k] || 0 <= ee1[k]
      for (int k = 0; k < N; k++) {
        // we only need to check lower bound for integers--sets, sequences, booleans, references, and datatypes all have natural lower bounds
        Bpl.Expr prefixIsLess = Bpl.Expr.False;
        for (int i = 0; i < k; i++) {
          prefixIsLess = BplOr(prefixIsLess, Less[i]);
        };

        Bpl.Expr zero = null;
        Expression dafnyZero = null;
        string zeroStr = null;
        if (dafny0[k].Type.NormalizeExpandKeepConstraints().IsNumericBased(Type.NumericPersuasion.Int)) {
          zero = Bpl.Expr.Literal(0);
          dafnyZero = Expression.CreateIntLiteral(dafny0[k].Origin, 0);
          zeroStr = "0";
        } else if (dafny0[k].Type.NormalizeExpandKeepConstraints().IsNumericBased(Type.NumericPersuasion.Real)) {
          zero = Bpl.Expr.Literal(BaseTypes.BigDec.ZERO);
          dafnyZero = Expression.CreateRealLiteral(dafny0[k].Origin, BigDec.ZERO);
          zeroStr = "0.0";
        }
        if (zero != null) {
          Bpl.Expr bounded = Bpl.Expr.Le(zero, ee1[k]);
          Expression boundedDafny = Expression.CreateAtMost(dafnyZero, dafny1[k]);
          for (int i = 0; i < k; i++) {
            bounded = BplOr(bounded, Less[i]);
            boundedDafny = Expression.CreateOr(boundedDafny, LessDafny[i]);
          }

          Expression dafnyBound = Expression.CreateOr(boundedDafny, EqDafny[k]);
          Bpl.Cmd cmd = Assert(toks[k], BplOr(bounded, Eq[k]),
            new DecreasesBoundedBelow(N, k, zeroStr, prevGhostLocals, dafnyBound, suffixMsg), builder.Context);
          builder.Add(cmd);
        }
      }
    }
    // check: ee0 < ee1 (or ee0 <= ee1, if allowNoChange)
    Bpl.Expr decrCheck = allowNoChange ? Bpl.Expr.True : Bpl.Expr.False;
    for (int i = N; 0 <= --i;) {
      Bpl.Expr less = Less[i];
      Bpl.Expr eq = Eq[i];
      if (allowNoChange) {
        // decrCheck = atmost && (eq ==> decrCheck)
        decrCheck = BplAnd(less, BplImp(eq, decrCheck));
      } else {
        // decrCheck = less || (eq && decrCheck)
        decrCheck = BplOr(less, BplAnd(eq, decrCheck));
      }
    }
    return decrCheck;
  }

  /// <summary>
  /// Lift compatible component pairs (s,w) from right to left. The terminal value
  /// is false for strict comparison, true for nonincrease, or the result of the
  /// first TOP/incompatible comparison after a shared prefix. Lower bounds belong
  /// to the component relations, never to an equality-controlled side condition.
  /// See docs/dev/decreases-order.md for the component and lifting arguments.
  /// </summary>
  Bpl.Expr ConsistentDecreasesCheck(List<IOrigin> toks, List<Expression> dafny0, List<Expression> dafny1,
    List<Bpl.Expr> ee0, List<Bpl.Expr> ee1, bool tailComparison) {
    Bpl.Expr result = tailComparison ? Bpl.Expr.True : Bpl.Expr.False;
    for (var i = dafny0.Count; 0 <= --i;) {
      var ty0 = dafny0[i].Type.NormalizeToAncestorType();
      var ty1 = dafny1[i].Type.NormalizeToAncestorType();
      Bpl.Expr strict, weak;
      var order0 = DecreasesByExpr.OrderOf(dafny0[i]);
      var order1 = DecreasesByExpr.OrderOf(dafny1[i]);
      if (order0 != null || order1 != null) {
        // A component "e by O" (--decreases-orders) is compared by O.gt(caller, callee), and only with a component with the same O.
        if (order0 != null && order1 != null && DecreasesOrders.SameOrder(order0, order1) &&
            DecreasesOrders.WfoOf(order1.Type) != null && CompatibleDecreasesTypesForComparison(ty0, ty1)) {
          OrderedComponent(toks[i], order1, dafny0[i].Type, ee0[i], ee1[i], out strict, out weak);
        } else {
          strict = weak = Bpl.Expr.False;
        }
      } else if (!CompatibleDecreasesTypesForComparison(ty0, ty1)) {
        strict = weak = Bpl.Expr.False;
      } else if (ty0.IsNumericBased(Type.NumericPersuasion.Int) || ty0.IsNumericBased(Type.NumericPersuasion.Real)) {
        var real = ty0.IsNumericBased(Type.NumericPersuasion.Real);
        var zero = real ? Bpl.Expr.Literal(BigDec.ZERO) : Bpl.Expr.Literal(0);
        var oldNonnegative = Bpl.Expr.Le(zero, ee1[i]);
        strict = BplAnd(oldNonnegative, real
          ? Bpl.Expr.Le(ee0[i], Bpl.Expr.Sub(ee1[i], Bpl.Expr.Literal(BigDec.FromInt(1))))
          : Bpl.Expr.Lt(ee0[i], ee1[i]));
        weak = BplOr(Bpl.Expr.Eq(ee0[i], ee1[i]), BplAnd(oldNonnegative, Bpl.Expr.Le(ee0[i], ee1[i])));
        // Splitting a surrounding conjunction can use the component expression's
        // token as its diagnostic origin, just as in ComputeLessEq below.
        strict.tok = toks[i];
        weak.tok = toks[i];
      } else if (ty0.IsCoDatatype || ty0.IsTypeParameter || ty0.IsAbstractType || ty0.IsInternalTypeSynonym) {
        // Coinductive and opaque values have no well-founded structural rank.
        strict = Bpl.Expr.False;
        weak = Bpl.Expr.Eq(ee0[i], ee1[i]);
      } else {
        // All inductive datatypes and sequences embed in the same natural-rank
        // order, including pairs with different representations. Use its <= even
        // when the legacy comparison's "eq" deliberately returns false.
        ComputeLessEq(toks[i], ty0, ty1, ee0[i], ee1[i], out strict, out weak, out _, false);
      }
      result = BplOr(strict, BplAnd(weak, result));
    }
    return result;
  }

  /// <summary>
  /// The strict and weak relations of a component "e by O", with new value e0 (callee) and old value e1 (caller):
  /// strict is O.gt(e1, e0), and weak is e0 == e1.  Weak must be equality, since O.gt need not be transitive and the
  /// lift in ConsistentDecreasesCheck needs the absorption laws (docs/dev/decreases-order.md).  O is closed, so it
  /// is translated the same in any state.
  /// </summary>
  void OrderedComponent(IOrigin tok, Expression order, Type type, Bpl.Expr e0, Bpl.Expr e1,
                        out Bpl.Expr strict, out Bpl.Expr weak) {
    var etran = new ExpressionTranslator(this, Predef, tok, null);
    var callee = new BoogieWrapper(e0, type);
    var caller = new BoogieWrapper(e1, type);
    var gt = DecreasesOrders.Member(DecreasesOrders.WfoTrait(order.Type)!, DecreasesOrders.GtName);
    var call = new FunctionCallExpr(tok, gt.NameNode, order, tok, Token.NoToken, [caller, callee]) {
      Function = gt,
      Type = Type.Bool,
      TypeApplication_AtEnclosingClass = [DecreasesOrders.WfoOf(order.Type)!],
      TypeApplication_JustFunction = []
    };
    // O is closed and well-formed (its well-formedness is checked with the decreases clause), so its can-call
    // facts hold in every state.  O.gt has no precondition and reads nothing.  The can-call facts let the
    // verifier unfold the functions that O and O.gt call.
    strict = BplImp(etran.CanCallAssumption(call), etran.TrExpr(call));
    weak = etran.TrExpr(Expression.CreateEq(callee, caller, type));
    strict.tok = tok;
    weak.tok = tok;
  }

  static bool CompatibleDecreasesTypes(Type t, Type u) {
    Contract.Requires(t != null);
    Contract.Requires(u != null);
    t = t.NormalizeToAncestorType();
    u = u.NormalizeToAncestorType();
    if (t is BoolType) {
      return u is BoolType;
    } else if (t is CharType) {
      return u is CharType;
    } else if (t is IntType) {
      // we can allow different kinds of int-based types
      return u is IntType;
    } else if (t is RealType) {
      // we can allow different kinds of real-based types
      return u is RealType;
    } else if (t is SetType) {
      return u is SetType;
    } else if (t is SeqType) {
      return u is SeqType || u.IsIndDatatype;
    } else if (t.IsDatatype) {
      return u.IsDatatype || (t.IsIndDatatype && u is SeqType);
    } else if (t.IsRefType) {
      return u.IsRefType;
    } else if (t is MultiSetType) {
      return u is MultiSetType;
    } else if (t is MapType) {
      return u is MapType && ((MapType)t).Finite == ((MapType)u).Finite;
    } else if (t is ArrowType) {
      return u is ArrowType;
    } else if (t is BitvectorType) {
      return u is BitvectorType;
    } else if (t is BigOrdinalType) {
      return u is BigOrdinalType;
    } else if (t.IsTypeParameter || t.IsAbstractType || t.IsInternalTypeSynonym) {
      return false;  // don't consider any type parameters to be the same (since we have no comparison function for them anyway)
    } else {
      return t.Equals(u);
    }
  }

  Nullable<BuiltinFunction> RankFunction(Type/*!*/ ty) {
    Contract.Requires(ty != null);
    if (ty is SeqType) {
      return BuiltinFunction.SeqRank;
    } else if (ty.IsIndDatatype) {
      return BuiltinFunction.DtRank;
    } else if (UseTraitRankDecreases && IsValueTraitType(ty)) {
      return BuiltinFunction.BoxRank;
    } else {
      return null;
    }
  }

  void ComputeLessEq(IOrigin tok, Type ty0, Type ty1, Bpl.Expr e0, Bpl.Expr e1, out Bpl.Expr less, out Bpl.Expr atmost, out Bpl.Expr eq, bool includeLowerBound) {
    Contract.Requires(tok != null);
    Contract.Requires(ty0 != null);
    Contract.Requires(ty1 != null);
    Contract.Requires(e0 != null);
    Contract.Requires(e1 != null);
    Contract.Requires(Predef != null);
    Contract.Ensures(Contract.ValueAtReturn(out less) != null);
    Contract.Ensures(Contract.ValueAtReturn(out atmost) != null);
    Contract.Ensures(Contract.ValueAtReturn(out eq) != null);

    ty0 = ty0.NormalizeToAncestorType();
    ty1 = ty1.NormalizeToAncestorType();
    var rk0 = RankFunction(ty0);
    var rk1 = RankFunction(ty1);
    if (rk0 != null && rk1 != null && rk0 != rk1) {
      eq = Bpl.Expr.False;
      Bpl.Expr b0 = FunctionCall(tok, rk0.Value, null, e0);
      Bpl.Expr b1 = FunctionCall(tok, rk1.Value, null, e1);
      less = Bpl.Expr.Lt(b0, b1);
      atmost = Bpl.Expr.Le(b0, b1);
    } else if (rk0 == BuiltinFunction.BoxRank && rk1 == BuiltinFunction.BoxRank) {
      // --trait-rank-decreases: two values of traits that are not reference types
      Bpl.Expr b0 = FunctionCall(tok, BuiltinFunction.BoxRank, null, e0);
      Bpl.Expr b1 = FunctionCall(tok, BuiltinFunction.BoxRank, null, e1);
      eq = Bpl.Expr.Eq(b0, b1);
      less = Bpl.Expr.Lt(b0, b1);
      atmost = Bpl.Expr.Le(b0, b1);
    } else if (ty0 is BoolType) {
      eq = BplIff(e0, e1);
      less = BplAnd(Bpl.Expr.Not(e0), e1);
      atmost = BplImp(e0, e1);
    } else if (ty0 is CharType) {
      eq = Bpl.Expr.Eq(e0, e1);
      var operand0 = FunctionCall(e0.tok, BuiltinFunction.CharToInt, null, e0);
      var operand1 = FunctionCall(e0.tok, BuiltinFunction.CharToInt, null, e1);
      less = Bpl.Expr.Binary(tok, BinaryOperator.Opcode.Lt, operand0, operand1);
      atmost = Bpl.Expr.Binary(tok, BinaryOperator.Opcode.Le, operand0, operand1);
    } else if (ty0.IsNumericBased(Type.NumericPersuasion.Int) || ty0 is SeqType || ty0.IsDatatype) {
      Bpl.Expr b0, b1;
      if (ty0.IsNumericBased(Type.NumericPersuasion.Int)) {
        b0 = e0;
        b1 = e1;
      } else if (ty0 is SeqType) {
        b0 = FunctionCall(tok, BuiltinFunction.SeqRank, null, e0);
        b1 = FunctionCall(tok, BuiltinFunction.SeqRank, null, e1);
      } else if (ty0.IsDatatype) {
        b0 = FunctionCall(tok, BuiltinFunction.DtRank, null, e0);
        b1 = FunctionCall(tok, BuiltinFunction.DtRank, null, e1);
      } else {
        Contract.Assert(false); throw new Cce.UnreachableException();
      }
      eq = Bpl.Expr.Eq(b0, b1);
      less = Bpl.Expr.Lt(b0, b1);
      atmost = Bpl.Expr.Le(b0, b1);
      if (ty0.IsNumericBased(Type.NumericPersuasion.Int) && includeLowerBound) {
        less = BplAnd(Bpl.Expr.Le(Bpl.Expr.Literal(0), b0), less);
        atmost = BplAnd(Bpl.Expr.Le(Bpl.Expr.Literal(0), b0), atmost);
      }

    } else if (ty0.IsNumericBased(Type.NumericPersuasion.Real)) {
      eq = Bpl.Expr.Eq(e0, e1);
      less = Bpl.Expr.Le(e0, Bpl.Expr.Sub(e1, Bpl.Expr.Literal(BaseTypes.BigDec.FromInt(1))));
      atmost = Bpl.Expr.Le(e0, e1);
      if (includeLowerBound) {
        less = BplAnd(Bpl.Expr.Le(Bpl.Expr.Literal(BaseTypes.BigDec.ZERO), e0), less);
        atmost = BplAnd(Bpl.Expr.Le(Bpl.Expr.Literal(BaseTypes.BigDec.ZERO), e0), atmost);
      }

    } else if (ty0 is IteratorDecl.EverIncreasingType) {
      eq = Bpl.Expr.Eq(e0, e1);
      less = Bpl.Expr.Gt(e0, e1);
      atmost = Bpl.Expr.Ge(e0, e1);

    } else if (ty0 is SetType { Finite: true } || ty0 is MapType { Finite: true }) {
      Bpl.Expr b0, b1;
      if (ty0 is SetType) {
        b0 = e0;
        b1 = e1;
      } else {
        // for maps, compare their domains as sets
        b0 = FunctionCall(tok, BuiltinFunction.MapDomain, Predef.MapType, e0);
        b1 = FunctionCall(tok, BuiltinFunction.MapDomain, Predef.MapType, e1);
      }
      eq = FunctionCall(tok, BuiltinFunction.SetEqual, null, b0, b1);
      less = ProperSubset(tok, b0, b1, true);
      atmost = FunctionCall(tok, BuiltinFunction.SetSubset, null, b0, b1);

    } else if (ty0 is SetType || ty0 is MapType) {
      Bpl.Expr b0, b1;
      if (ty0 is SetType) {
        Contract.Assert(!((SetType)ty0).Finite);
        b0 = e0;
        b1 = e1;
      } else {
        Contract.Assert(!((MapType)ty0).Finite);
        // for maps, compare their domains as sets
        b0 = FunctionCall(tok, BuiltinFunction.IMapDomain, Predef.MapType, e0);
        b1 = FunctionCall(tok, BuiltinFunction.IMapDomain, Predef.MapType, e1);
      }
      eq = FunctionCall(tok, BuiltinFunction.ISetEqual, null, b0, b1);
      less = Bpl.Expr.False;
      atmost = BplOr(less, eq);

    } else if (ty0 is MultiSetType) {
      eq = FunctionCall(tok, BuiltinFunction.MultiSetEqual, null, e0, e1);
      less = ProperMultiset(tok, e0, e1);
      atmost = FunctionCall(tok, BuiltinFunction.MultiSetSubset, null, e0, e1);

    } else if (ty0 is ArrowType) {
      eq = Bpl.Expr.Eq(e0, e1);
      less = Bpl.Expr.False;  // TODO: try to do better than this
      atmost = BplOr(less, eq);

    } else if (ty0 is BitvectorType) {
      BitvectorType bv = (BitvectorType)ty0;
      eq = Bpl.Expr.Eq(e0, e1);
      less = FunctionCall(tok, "lt_bv" + bv.Width, Bpl.Type.Bool, e0, e1);
      atmost = FunctionCall(tok, "le_bv" + bv.Width, Bpl.Type.Bool, e0, e1);

    } else if (ty0 is BigOrdinalType) {
      eq = Bpl.Expr.Eq(e0, e1);
      less = FunctionCall(tok, "ORD#Less", Bpl.Type.Bool, e0, e1);
      atmost = BplOr(eq, less);

    } else if (ty0.IsTypeParameter || ty0.IsAbstractType) {
      eq = Bpl.Expr.Eq(e0, e1);
      less = Bpl.Expr.False;
      atmost = BplOr(less, eq);

    } else {
      // reference type
      Contract.Assert(ty0.IsRefType);  // otherwise, unexpected type
      var b0 = Bpl.Expr.Neq(e0, Predef.Null);
      var b1 = Bpl.Expr.Neq(e1, Predef.Null);
      eq = BplIff(b0, b1);
      less = BplAnd(Bpl.Expr.Not(b0), b1);
      atmost = BplImp(b0, b1);
    }

    less.tok = tok;
    atmost.tok = tok;
  }
}
