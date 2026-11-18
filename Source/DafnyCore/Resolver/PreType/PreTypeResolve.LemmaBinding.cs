//-----------------------------------------------------------------------------
//
// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
//
//-----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Diagnostics.Contracts;
using ResolutionContext = Microsoft.Dafny.ResolutionContext;

namespace Microsoft.Dafny {
  public partial class PreTypeResolver {
    /// <summary>
    /// If non-null, this is the origin of the callee name in the right-hand side of an expression of the form
    /// "var y1, ..., yk := M(args); e" that is being resolved without --lemma-bindings. It lets the error about a
    /// lemma call in an expression add a hint about that option, without any change to the error itself.
    /// </summary>
    private IOrigin lemmaBindingHintOrigin;

    /// <summary>
    /// The let expression that the most recent "var y :- E; body" was rewritten into. Its right-hand side is "E",
    /// which the user did not write as the right-hand side of a "var y := ..." and which therefore is not a lemma
    /// binding.
    /// </summary>
    private Expression desugaredLetOrFail;

    /// <summary>
    /// The expression "e", which has the form "var y1, ..., yk := M(args); body", has been found to have a method call
    /// as its right-hand side; "callInfo" is what the resolution of that right-hand side returned. The types of the
    /// bound variables have been resolved, and so have the receiver and the actual arguments of the call.
    ///
    /// If M is a lemma that can be used this way, rewrite "e" in place into the form described at
    /// LetExpr.LemmaBinding and return the let expression that binds y1, ..., yk. In that let expression, the
    /// right-hand sides have been resolved, and the left-hand sides and body have not.
    ///
    /// Otherwise, report an error and return null. In that case, "e" is unchanged, except that its right-hand side
    /// has been given a pre-type.
    /// </summary>
    LetExpr ResolveLemmaBinding(LetExpr e, ApplySuffix applySuffix, MethodCallInformation callInfo, ResolutionContext resolutionContext) {
      Contract.Requires(e is { Exact: true, RHSs: { Count: 1 } } && e.RHSs[0] == applySuffix);
      Contract.Requires(callInfo != null);
      Contract.Requires(resolutionContext != null);

      var errorCountBefore = ErrorCount;
      var selection = callInfo.Callee;
      var callee = (MethodOrConstructor)selection.Member;
      if (callee is TwoStateLemma or ExtremeLemma or PrefixLemma) {
        ReportError(applySuffix.Origin, "the results of a {0} cannot be bound in an expression ({1})", callee.WhatKind, callee.Name);
      } else if (callee is not Lemma) {
        ReportError(applySuffix.Origin, "expression is not allowed to invoke a {0} ({1})", callee.WhatKind, callee.Name);
      } else if (e.LHSs.Find(lhs => lhs.Arguments != null) is { } pattern) {
        ReportError(pattern.Origin, "the results of a lemma must be bound to variables, not to general patterns");
      } else if (e.LHSs.Count != callee.Outs.Count) {
        ReportError(applySuffix.Origin, "wrong number of method result arguments (got {0}, expected {1})", e.LHSs.Count, callee.Outs.Count);
      }
      if (ErrorCount != errorCountBefore) {
        applySuffix.PreType = CreatePreTypeProxy("unresolved lemma binding");
        return null;
      }
      var lemma = (Lemma)callee;

      // Name the receiver and each given argument, so that the call and the result expressions can all mention them.
      var argumentVariables = new List<CasePattern<BoundVar>>();
      var argumentValues = new List<Expression>();
      BoundVar Bind(Expression value, string name) {
        var variable = new BoundVar(value.Origin, name, new InferredTypeProxy(), true) { PreType = value.PreType };
        var casePattern = new CasePattern<BoundVar>(value.Origin, variable);
        ResolveCasePattern(casePattern, value.PreType, resolutionContext);
        argumentVariables.Add(casePattern);
        argumentValues.Add(value);
        return variable;
      }
      Expression Use(BoundVar variable) {
        return new IdentifierExpr(variable.Origin, variable) { PreType = variable.PreType };
      }

      // A receiver that is "this" or just names a type is not given a name. Instead, each use gets its own copy.
      System.Func<Expression> receiver;
      var originalReceiver = selection.Obj;
      if (originalReceiver is ThisExpr || originalReceiver is StaticReceiverExpr { ObjectToDiscard: null }) {
        receiver = () => {
          var copy = new Cloner(false, true).CloneExpr(originalReceiver);
          copy.PreType = originalReceiver.PreType;
          return copy;
        };
      } else if (originalReceiver is StaticReceiverExpr staticReceiver) {
        // The object in "obj.L(args)", where L is static, is evaluated but not used. Evaluate it once.
        Bind(staticReceiver.ObjectToDiscard, "_receiver");
        receiver = () => {
          var copy = (StaticReceiverExpr)new Cloner(false, true).CloneExpr(staticReceiver);
          copy.ObjectToDiscard = null;
          copy.PreType = staticReceiver.PreType;
          return copy;
        };
      } else {
        var receiverVariable = Bind(originalReceiver, "_receiver");
        receiver = () => Use(receiverVariable);
      }
      var givenArguments = new List<(ActualBinding, BoundVar)>();
      foreach (var binding in callInfo.ActualParameters) {
        givenArguments.Add((binding, Bind(binding.Actual, "_in" + givenArguments.Count)));
      }
      List<ActualBinding> Arguments() {
        return givenArguments.ConvertAll(given => new ActualBinding(given.Item1.FormalParameterName, Use(given.Item2), given.Item1.IsGhost));
      }

      // The call statement, "var z1, ..., zk := L(arguments);". It is resolved as any call statement is.
      var locals = new List<LocalVariable>();
      var localUses = new List<Expression>();
      for (var i = 0; i < lemma.Outs.Count; i++) {
        var local = new LocalVariable(e.LHSs[i].Origin, "_out" + i, new InferredTypeProxy(), true);
        local.type = local.SyntacticType;
        local.PreType = CreatePreTypeProxy("lemma result");
        locals.Add(local);
        localUses.Add(new IdentifierExpr(local.Origin, local) { PreType = local.PreType });
      }
      var callSelection = new MemberSelectExpr(selection.Origin, receiver(), selection.MemberNameNode) {
        Member = lemma,
        PreTypeApplicationAtEnclosingClass = selection.PreTypeApplicationAtEnclosingClass,
        PreTypeApplicationJustMember = selection.PreTypeApplicationJustMember,
        TypeApplicationJustMember = selection.TypeApplicationJustMember,
        PreType = selection.PreType
      };
      var call = new CallStmt(applySuffix.Origin, localUses, callSelection, Arguments(), callInfo.Tok.ReportingRange);
      ResolveCallStmt(call, resolutionContext, null);
      if (ErrorCount != errorCountBefore) {
        applySuffix.PreType = CreatePreTypeProxy("unresolved lemma binding");
        return null;
      }
      var assignment = new AssignStatement(applySuffix.Origin, localUses, [new ExprRhs(applySuffix)]) {
        ResolvedStatements = [call]
      };
      var callStatement = new VarDeclStmt(applySuffix.Origin, locals, assignment);

      // The result expressions. They use the type arguments of the call statement.
      var typeMap = callSelection.PreTypeArgumentSubstitutionsAtMemberDeclaration();
      var results = new List<Expression>();
      for (var i = 0; i < lemma.Outs.Count; i++) {
        var result = new LemmaResultExpr(applySuffix.Origin, lemma, receiver(), new ActualBindings(Arguments()), i) {
          PreTypeApplicationAtEnclosingClass = callSelection.PreTypeApplicationAtEnclosingClass,
          PreTypeApplicationJustMember = callSelection.PreTypeApplicationJustMember
        };
        ResolveActualParameters(result.Bindings, lemma.Ins, applySuffix.Origin, lemma, resolutionContext, typeMap,
          lemma.IsStatic ? null : result.Receiver);
        result.PreType = lemma.Outs[i].PreType.Substitute(typeMap);
        results.Add(result);
      }
      Contract.Assert(ErrorCount == errorCountBefore); // the call statement was resolved from the same arguments, without error
      results[0] = new StmtExpr(applySuffix.Origin, callStatement, results[0]) { PreType = results[0].PreType };

      // Put the pieces together
      var written = e.LHSs;
      LetExpr resultLet;
      if (argumentVariables.Count == 0) {
        e.RHSs = results;
        resultLet = e;
      } else {
        resultLet = new LetExpr(e.Origin, written, results, e.Body, true);
        e.LHSs = argumentVariables;
        e.RHSs = argumentValues;
        e.Body = resultLet;
      }
      e.LemmaBinding = new LetExpr.LemmaBindingSyntax(applySuffix, argumentVariables.Count != 0);
      return resultLet;
    }
  }
}
