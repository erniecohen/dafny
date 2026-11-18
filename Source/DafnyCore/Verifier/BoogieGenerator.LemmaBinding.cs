//-----------------------------------------------------------------------------
//
// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
//
//-----------------------------------------------------------------------------
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Bpl = Microsoft.Boogie;

namespace Microsoft.Dafny {
  public partial class BoogieGenerator {
    /// <summary>
    /// The lemmas whose results are bound in an expression that this translation has met (see LetExpr.LemmaBinding),
    /// each with the functions that stand for its chosen results.
    /// </summary>
    private readonly Dictionary<Lemma, LemmaBindingInfo> lemmaBindingInfo = new();

    /// <summary>
    /// For a lemma
    ///     lemma L<X>(x: A) returns (y: B, z: C)  requires Pre  ensures Post
    /// whose results are bound somewhere, the translation declares, once:
    ///     function L#bind$canCall(G): bool;
    ///     function L#bind#y(G): B;
    ///     function L#bind#z(G): C;
    ///     axiom (forall G :: { L#bind#y(G) } { L#bind#z(G) }
    ///       L#bind$canCall(G) ==>
    ///         typeOf(L#bind#y(G), L#bind#z(G)) && CanCall[[ Post ]] && Post)   // Post at y,z := L#bind#y(G), L#bind#z(G)
    /// where G is the type arguments (those of the enclosing type, then X), the heap if Post depends on it, the
    /// receiver if L is an instance lemma, and the in-parameters.
    ///
    /// The axiom says that the functions pick, among the results the postcondition allows, one that depends on G
    /// alone. L#bind$canCall(G) is assumed only where a call L(G) has been checked (AssumeLemmaBinding), and such a
    /// call shows that some results satisfy the postcondition. Nothing is said about what the body of L computes.
    /// </summary>
    public class LemmaBindingInfo {
      public readonly Lemma Lemma;
      public readonly bool UsesHeap;
      /// <summary>
      /// Whether the postcondition is one that the results can be bound for (condition 3 of LemmaBindings). If not,
      /// the functions are declared, but there is no axiom about them and L#bind$canCall is never assumed.
      /// </summary>
      public readonly bool PostconditionCanBeBound;

      public LemmaBindingInfo(Lemma lemma, bool usesHeap, bool postconditionCanBeBound) {
        Contract.Requires(lemma != null);
        Lemma = lemma;
        UsesHeap = usesHeap;
        PostconditionCanBeBound = postconditionCanBeBound;
      }

      public string CanCallFunctionName => Lemma.FullSanitizedName + "#bind$canCall";

      public string ResultFunctionName(int outIndex) {
        Contract.Requires(0 <= outIndex && outIndex < Lemma.Outs.Count);
        return Lemma.FullSanitizedName + "#bind#" + Lemma.Outs[outIndex].Name;
      }
    }

    LemmaBindingInfo GetLemmaBindingInfo(Lemma lemma) {
      Contract.Requires(lemma != null);
      if (lemmaBindingInfo.TryGetValue(lemma, out var existingInfo)) {
        return existingInfo;
      }
      var info = new LemmaBindingInfo(lemma, lemma.PostconditionUsesHeap(options),
        LemmaBindings.WhyPostconditionCannotBeBound(lemma, options) == null);
      // Record the functions before the axiom is built, since the postcondition may itself bind the results of a lemma.
      lemmaBindingInfo.Add(lemma, info);
      var tok = lemma.Origin;

      // The parameters G, as formals of the functions and as bound variables of the axiom.
      List<Bpl.Variable> Parameters(bool asFormals) {
        var parameters = new List<Bpl.Variable>();
        void Add(string name, Bpl.Type type) {
          var typedIdent = new Bpl.TypedIdent(tok, name, type);
          parameters.Add(asFormals ? new Bpl.Formal(tok, typedIdent, true) : new Bpl.BoundVariable(tok, typedIdent));
        }
        foreach (var typeParameter in GetTypeParams(lemma)) {
          Add(NameTypeParam(typeParameter), Predef.Ty);
        }
        if (info.UsesHeap) {
          Add("$heap", Predef.HeapType);
        }
        if (!lemma.IsStatic) {
          Add("this", TrReceiverType(lemma));
        }
        foreach (var formal in lemma.Ins) {
          Add(formal.AssignUniqueName(lemma.IdGenerator), TrType(formal.Type));
        }
        return parameters;
      }

      Bpl.Function Declare(string name, Bpl.Type resultType) {
        var result = new Bpl.Formal(tok, new Bpl.TypedIdent(tok, Bpl.TypedIdent.NoName, resultType), false);
        var function = new Bpl.Function(tok, name, Parameters(true), result);
        if (InsertChecksums) {
          InsertChecksum(lemma, function, true);
        }
        sink.AddTopLevelDeclaration(function);
        return function;
      }
      var canCallFunction = Declare(info.CanCallFunctionName, Bpl.Type.Bool);
      for (var i = 0; i < lemma.Outs.Count; i++) {
        Declare(info.ResultFunctionName(i), TrType(lemma.Outs[i].Type));
      }

      if (!info.PostconditionCanBeBound) {
        // The resolver rejects a binding of such a lemma. If one gets here anyhow, say nothing about its results.
        return info;
      }

      // The axiom
      var boundVariables = Parameters(false);
      var g = boundVariables.ConvertAll(variable => (Bpl.Expr)new Bpl.IdentifierExpr(tok, variable));
      var index = GetTypeParams(lemma).Count;
      // When the postcondition does not depend on the heap, it is translated as a heap-independent definition is,
      // with the placeholder $OneHeap (compare AddLetSuchThenCanCallAxiom).
      var heap = info.UsesHeap ? g[index++] : NewOneHeapExpr(tok);
      var etran = new ExpressionTranslator(this, Predef, heap, heap, null);
      Expression receiverReplacement = null;
      if (!lemma.IsStatic) {
        receiverReplacement = new BoogieWrapper(g[index++], ModuleResolver.GetReceiverType(tok, lemma));
      }
      var substMap = new Dictionary<IVariable, Expression>();
      foreach (var formal in lemma.Ins) {
        substMap.Add(formal, new BoogieWrapper(g[index++], formal.Type));
      }
      Contract.Assert(index == g.Count);

      Bpl.Trigger trigger = null;
      Bpl.Expr consequent = Bpl.Expr.True;
      for (var i = 0; i < lemma.Outs.Count; i++) {
        var formal = lemma.Outs[i];
        var result = FunctionCall(tok, info.ResultFunctionName(i), TrType(formal.Type), g);
        trigger = new Bpl.Trigger(tok, true, new List<Bpl.Expr> { result }, trigger);
        substMap.Add(formal, new BoogieWrapper(result, formal.Type));
        var typeOf = GetWhereClause(tok, result, formal.Type, etran, NOALLOC);
        if (typeOf != null) {
          consequent = BplAnd(consequent, typeOf);
        }
      }
      foreach (var postcondition in lemma.Ens) {
        var p = Substitute(postcondition.E, receiverReplacement, substMap);
        consequent = BplAnd(consequent, BplAnd(etran.CanCallAssumption(p), etran.TrExpr(p)));
      }
      var canCall = FunctionCall(tok, info.CanCallFunctionName, Bpl.Type.Bool, g);
      var axiom = new Bpl.Axiom(tok, BplForall(boundVariables, trigger, BplImp(canCall, consequent)),
        $"results of lemma {lemma.FullDafnyName}, bound in expressions");
      AddOtherDefinition(canCallFunction, axiom);
      return info;
    }

    /// <summary>
    /// The arguments G (see LemmaBindingInfo) for a use of the lemma at the given type arguments, receiver and
    /// in-arguments, in the heap of "etran".
    /// </summary>
    List<Bpl.Expr> LemmaBindingArguments(LemmaBindingInfo info, Bpl.IToken tok, Dictionary<TypeParameter, Type> typeMap,
      Expression receiver, List<Expression> arguments, ExpressionTranslator etran) {
      var lemma = info.Lemma;
      Contract.Assert(arguments.Count == lemma.Ins.Count);
      var g = TrTypeArgs(typeMap, GetTypeParams(lemma));
      if (info.UsesHeap) {
        g.Add(etran.HeapExpr);
      }
      if (!lemma.IsStatic) {
        g.Add(BoxifyForTraitParent(tok, etran.TrExpr(receiver), lemma, receiver.Type));
      }
      for (var i = 0; i < arguments.Count; i++) {
        var argument = arguments[i];
        g.Add(AdaptBoxing(tok, etran.TrExpr(argument), argument.Type, lemma.Ins[i].Type));
      }
      return g;
    }

    /// <summary>
    /// "call" is the call statement of an expression that binds the results of a lemma (see LetExpr.LemmaBinding).
    /// Return L#bind$canCall(G) for that call, or null if the call is not one whose results can be bound.
    ///
    /// This is computed from the call statement alone, never from the result expressions that follow the call. So
    /// what is assumed about the results of a lemma at G is assumed only where a call at G is checked.
    /// </summary>
    Bpl.Expr LemmaBindingCanCall(CallStmt call, ExpressionTranslator etran) {
      Contract.Requires(call != null);
      Contract.Requires(etran != null);
      var typeMap = call.MethodSelect.TypeArgumentSubstitutionsWithParents();
      if (call.Method is not Lemma lemma || LemmaBindings.WhyResultTypesCannotBeBound(lemma, typeMap) != null) {
        // The resolver rejects such a binding. If one gets here anyhow, assume nothing about the results.
        return null;
      }
      var info = GetLemmaBindingInfo(lemma);
      if (!info.PostconditionCanBeBound) {
        return null;
      }
      var tok = GetToken(call);
      return FunctionCall(tok, info.CanCallFunctionName, Bpl.Type.Bool,
        LemmaBindingArguments(info, tok, typeMap, call.Receiver, call.Args, etran));
    }

    /// <summary>
    /// Clones a postcondition of a lemma for a check at an expression that binds the lemma's results. Each node
    /// gets the origin of the binding, with its own origin as a related location, so that an error is reported at
    /// the binding. A parameter of the lemma for which "actuals" gives a variable is replaced by that variable, with
    /// the origin the variable has at the binding, so that an error message that mentions it shows what was written
    /// there. The same goes for a variable that names such a variable as an argument of a binding in the
    /// postcondition.
    /// </summary>
    private class PostconditionAtBindingCloner : Cloner {
      private readonly IOrigin binding;
      private readonly Dictionary<IVariable, IdentifierExpr> actuals;
      private readonly Dictionary<IVariable, IOrigin> writtenAs = new();

      public PostconditionAtBindingCloner(IOrigin binding, Dictionary<IVariable, IdentifierExpr> actuals) : base(false, true) {
        this.binding = binding;
        this.actuals = actuals;
      }

      public override Expression CloneExpr(Expression expr) {
        if (expr is IdentifierExpr identifierExpr) {
          if (actuals.TryGetValue(identifierExpr.Var, out var actual)) {
            return new IdentifierExpr(actual.Origin, actual.Var);
          } else if (writtenAs.TryGetValue(identifierExpr.Var, out var origin)) {
            return new IdentifierExpr(origin, CloneIVariable(identifierExpr.Var, true));
          }
        } else if (expr is LetExpr { LemmaBinding: { HasArgumentLet: true } } argumentLet) {
          for (var i = 0; i < argumentLet.LHSs.Count; i++) {
            if (argumentLet.RHSs[i].Resolved is IdentifierExpr argument) {
              if (actuals.TryGetValue(argument.Var, out var actual)) {
                writtenAs[argumentLet.LHSs[i].Var] = actual.Origin;
              } else if (writtenAs.TryGetValue(argument.Var, out var origin)) {
                writtenAs[argumentLet.LHSs[i].Var] = origin;
              }
            }
          }
        }
        return base.CloneExpr(expr);
      }

      public override IOrigin Origin(IOrigin origin) {
        return origin == null ? null : new NestedOrigin(binding, origin, "this is in the postcondition of the lemma whose results are bound");
      }
    }

    /// <summary>
    /// "call" is the call statement of an expression that binds the results of a lemma, and that statement has
    /// just been translated: its precondition and termination have been checked, and its postcondition has been
    /// assumed of the values in the left-hand sides of "call". Add what remains for the well-formedness of the
    /// binding: the reads checks of the postcondition at those values, and the assumption L#bind$canCall(G).
    /// </summary>
    void AssumeLemmaBinding(CallStmt call, WFOptions wfOptions, Variables locals, BoogieStmtListBuilder builder,
      ExpressionTranslator etran) {
      var canCall = LemmaBindingCanCall(call, etran);
      if (canCall == null) {
        return;
      }
      var lemma = (Lemma)call.Method;
      if (wfOptions.DoReadsChecks && GetLemmaBindingInfo(lemma).UsesHeap) {
        // The chosen results depend on the heap, as the postcondition does. What the postcondition reads, at any
        // results that satisfy it, must be in the enclosing frame. The values that the call returned are arbitrary
        // such results. The other well-formedness conditions of the postcondition were checked with the lemma and
        // follow here from its precondition and the postcondition clauses assumed so far.
        var typeMap = call.MethodSelect.TypeArgumentSubstitutionsWithParents();
        var substMap = new Dictionary<IVariable, Expression>();
        var variables = new Dictionary<IVariable, IdentifierExpr>();
        void Replace(Formal formal, Expression actual) {
          if (actual.Resolved is IdentifierExpr identifierExpr) {
            variables.Add(formal, identifierExpr);
          } else {
            substMap.Add(formal, actual);
          }
        }
        for (var i = 0; i < lemma.Ins.Count; i++) {
          Replace(lemma.Ins[i], call.Args[i]);
        }
        for (var i = 0; i < lemma.Outs.Count; i++) {
          Replace(lemma.Outs[i], call.Lhs[i]);
        }
        var cloner = new PostconditionAtBindingCloner(call.Origin, variables);
        foreach (var postcondition in lemma.Ens) {
          var p = Substitute(cloner.CloneExpr(postcondition.E), lemma.IsStatic ? null : call.Receiver, substMap, typeMap);
          CheckWellformed(p, wfOptions, locals, builder, etran);
        }
      }
      builder.Add(TrAssumeCmd(call.Origin, canCall));
    }
  }
}
