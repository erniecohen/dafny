using System.Collections.Generic;
using System.Linq;
using Microsoft.Boogie;
using Bpl = Microsoft.Boogie;
using static Microsoft.Dafny.Util;

namespace Microsoft.Dafny;

// Domain tags on function values (https://github.com/erniecohen/dafny/issues/374), under
// --additional-axioms.  See docs/dev/additional-axioms.md, "Function domains".
//
// With the option, every handle the translator builds records its domain:
//  - a lambda is built by TagHandleN(D1, .., DN, h, r, rd), where Di is the declared type of
//    its i-th parameter, instead of HandleN(h, r, rd);
//  - $FunDomN(f, bx1, .., bxN) is membership in f's domain;
//  - the application, precondition and reads selectors of TagHandleN hold only inside the
//    domain.  Outside it they are unspecified, so two handles with the same domain that agree
//    on it can be equal.
// Two kinds of handle keep unguarded selectors, because their values outside the domain are
// already determined by their values inside it, or are free:
//  - a lambda whose parameters all have type int, real, bool or ORDINAL is built by
//    BasicHandleN, with the same tags.  Its maps read each box only through an unboxing to
//    that type, and every box unboxes to some value of it, so its value at any box is its
//    value at a box in the domain;
//  - F#Handle applies F to the unboxed arguments, and F is constrained only through
//    F#canCall, which the translator assumes only for arguments in F's domain.  This does
//    not hold for the .requires and .reads members of an arrow type: their "F" is the
//    Requires or Reads selector itself, which is constrained at every box.  Their handles
//    keep guarded selectors.
// Restrict and ExactDom, declared in the standard library with {:functionDomain}, are
// interpreted here.
public partial class BoogieGenerator {

  public const string FunctionDomainAttributeName = "functionDomain";

  bool FunctionDomainTags => options.Get(CommonOptionBag.AdditionalAxioms) || FunctionExtensionalityAxiom;

  // M2 of the trial: extensionality as an axiom triggered at each equality of total arrows.
  bool FunctionExtensionalityAxiom => options.Get(CommonOptionBag.FunctionExtensionalityAxiom);

  public static string ExactDom(int arity) {
    return "$ExactDom" + arity;
  }

  public static string FunEq(int arity) {
    return "$FunEq" + arity;
  }

  public static string TagHandle(int arity) {
    return "TagHandle" + arity;
  }

  public static string BasicHandle(int arity) {
    return "BasicHandle" + arity;
  }

  /// <summary>
  /// True for a type whose every Boogie value is a value of the type: int, real, bool and
  /// ORDINAL, but not a subset type or a newtype of one.
  /// </summary>
  internal static bool IsBasicDomainType(Type t) {
    return t.NormalizeExpand(true) is IntType or RealType or BoolType or BigOrdinalType;
  }

  public static string FunDom(int arity) {
    return "$FunDom" + arity;
  }

  /// <summary>
  /// Declares TagHandleN, BasicHandleN and $FunDomN.  TagHandleN's selector axioms are each
  /// guarded by membership of the arguments in the tags (BasicHandleN's are the same, unguarded):
  ///   forall t.., d.., h, r, rd, heap, bx.. ::
  ///     $IsBox(bx1, d1) && .. && $IsBox(bxN, dN) ==>
  ///       ApplyN(t.., heap, TagHandleN(d.., h, r, rd), bx..) == h[heap, bx..]
  ///   (and r[heap, bx..] ==> RequiresN(..), and Reads membership == rd membership)
  ///   forall d.., h, r, rd, bx.. :: $FunDomN(TagHandleN(d.., h, r, rd), bx..) <==> $IsBox(bx1, d1) && ..
  /// </summary>
  private void AddTaggedHandleAxioms(ArrowTypeDecl ad, Bpl.Type applyTy, Bpl.Type requiresTy, Bpl.Type readsTy,
    Bpl.Type objsetTy) {
    var arity = ad.Arity;
    var tok = ad.Origin;
    foreach (var constructor in new[] { TagHandle(arity), BasicHandle(arity) }) {
      var args = new List<Bpl.Variable>();
      for (var i = 0; i < arity; i++) {
        args.Add(BplFormalVar(null, Predef.Ty, true));
      }
      args.Add(BplFormalVar(null, applyTy, true));
      args.Add(BplFormalVar(null, requiresTy, true));
      args.Add(BplFormalVar(null, readsTy, true));
      sink.AddTopLevelDeclaration(new Bpl.Function(Token.NoToken, constructor, args,
        BplFormalVar(null, Predef.HandleType, false)));
    }
    {
      var args = new List<Bpl.Variable> { BplFormalVar(null, Predef.HandleType, true) };
      for (var i = 0; i < arity; i++) {
        args.Add(BplFormalVar(null, Predef.BoxType, true));
      }
      sink.AddTopLevelDeclaration(new Bpl.Function(Token.NoToken, FunDom(arity), args,
        BplFormalVar(null, Bpl.Type.Bool, false)));
    }

    void Selector(string constructor, string selector, Bpl.Type selectorTy, string selectorVar) {
      var bvars = new List<Bpl.Variable>();
      var types = Map(Enumerable.Range(0, arity + 1), i => BplBoundVar("t" + i, Predef.Ty, bvars));
      var tags = Map(Enumerable.Range(0, arity), i => BplBoundVar("d" + i, Predef.Ty, bvars));
      var heap = BplBoundVar("heap", Predef.HeapType, bvars);
      var maps = new Dictionary<string, Bpl.Expr> {
        ["h"] = BplBoundVar("h", applyTy, bvars),
        ["r"] = BplBoundVar("r", requiresTy, bvars),
        ["rd"] = BplBoundVar("rd", readsTy, bvars)
      };
      var boxes = Map(Enumerable.Range(0, arity), i => BplBoundVar("bx" + i, Predef.BoxType, bvars));
      var handle = FunctionCall(tok, constructor, Predef.HandleType,
        Concat(tags, new List<Bpl.Expr> { maps["h"], maps["r"], maps["rd"] }));
      Bpl.Expr lhs = FunctionCall(tok, selector, selectorTy, Concat(types, Cons(heap, Cons(handle, boxes))));
      Bpl.Expr rhs = new Bpl.NAryExpr(tok, new Bpl.MapSelect(tok, arity + 1), Cons(maps[selectorVar], Cons(heap, boxes)));
      Bpl.Expr body;
      if (selectorVar == "rd") {
        var bx = BplBoundVar("bx", Predef.BoxType, bvars);
        lhs = IsSetMember(tok, lhs, bx, true);
        body = Bpl.Expr.Eq(lhs, IsSetMember(tok, rhs, bx, true));
      } else if (selectorVar == "r") {
        body = BplImp(rhs, lhs);
      } else {
        body = Bpl.Expr.Eq(lhs, rhs);
      }
      if (constructor == TagHandle(arity)) {
        body = BplImp(BplAnd(boxes.Zip(tags, (b, d) => MkIs(b, d, true))), body);
      }
      AddOtherDefinition(GetOrCreateTypeConstructor(ad), new Axiom(tok, BplForall(bvars, BplTrigger(lhs), body)));
    }
    foreach (var constructor in new[] { TagHandle(arity), BasicHandle(arity) }) {
      Selector(constructor, Apply(arity), Predef.BoxType, "h");
      Selector(constructor, Requires(arity), Bpl.Type.Bool, "r");
      Selector(constructor, Reads(arity), objsetTy, "rd");
    }

    foreach (var constructor in new[] { TagHandle(arity), BasicHandle(arity) }) {
      var bvars = new List<Bpl.Variable>();
      var tags = Map(Enumerable.Range(0, arity), i => BplBoundVar("d" + i, Predef.Ty, bvars));
      var h = BplBoundVar("h", applyTy, bvars);
      var r = BplBoundVar("r", requiresTy, bvars);
      var rd = BplBoundVar("rd", readsTy, bvars);
      var boxes = Map(Enumerable.Range(0, arity), i => BplBoundVar("bx" + i, Predef.BoxType, bvars));
      var handle = FunctionCall(tok, constructor, Predef.HandleType, Concat(tags, new List<Bpl.Expr> { h, r, rd }));
      var dom = FunctionCall(tok, FunDom(arity), Bpl.Type.Bool, Cons(handle, boxes));
      var guard = BplAnd(boxes.Zip(tags, (b, d) => MkIs(b, d, true)));
      AddOtherDefinition(GetOrCreateTypeConstructor(ad), new Axiom(tok,
        BplForall(bvars, BplTrigger(dom), BplIff(dom, guard))));
    }
    if (arity == 1) {
      AddExactDomainAxioms(ad);
    }
  }

  /// <summary>
  ///   function $ExactDom1(Ty, HandleType): bool
  ///   forall a, f :: $ExactDom1(a, f) <==> (forall bx :: $FunDom1(f, bx) <==> $IsBox(bx, a))
  ///   forall b, f :: $Is(f, TotalFunc1(T, b)) ==> $ExactDom1(T, f), for T among int, real, bool and ORDINAL
  /// and, with --function-extensionality-axiom,
  ///   function $FunEq1(Ty, Ty, HandleType, HandleType): bool
  ///   forall a, b, f, g :: $FunEq1(a, b, f, g) <==> f == g
  ///   forall a, b, f, g :: $Is(f, TotalFunc1(a, b)) && $Is(g, TotalFunc1(a, b)) && $ExactDom1(a, f) && $ExactDom1(a, g) &&
  ///     (forall bx :: $IsBox(bx, a) ==> Apply1(a, b, $OneHeap, f, bx) == Apply1(a, b, $OneHeap, g, bx))
  ///     ==> $FunEq1(a, b, f, g)
  /// </summary>
  private void AddExactDomainAxioms(ArrowTypeDecl ad) {
    var tok = ad.Origin;
    var typeConstructor = GetOrCreateTypeConstructor(ad);
    sink.AddTopLevelDeclaration(new Bpl.Function(Token.NoToken, ExactDom(1),
      [BplFormalVar(null, Predef.Ty, true), BplFormalVar(null, Predef.HandleType, true)],
      BplFormalVar(null, Bpl.Type.Bool, false)));
    {
      var bvars = new List<Bpl.Variable>();
      var a = BplBoundVar("a", Predef.Ty, bvars);
      var fn = BplBoundVar("f", Predef.HandleType, bvars);
      var exact = FunctionCall(tok, ExactDom(1), Bpl.Type.Bool, a, fn);
      var bxVar = new Bpl.BoundVariable(tok, new Bpl.TypedIdent(tok, "bx", Predef.BoxType));
      var bx = new Bpl.IdentifierExpr(tok, bxVar);
      var dom = FunctionCall(tok, FunDom(1), Bpl.Type.Bool, fn, bx);
      var same = BplForall(bxVar, BplTrigger(dom), BplIff(dom, MkIs(bx, a, true)));
      AddOtherDefinition(typeConstructor, new Axiom(tok, BplForall(bvars, BplTrigger(exact), BplIff(exact, same))));
    }
    // The verifier's values of int, real, bool and ORDINAL are all values of the Dafny type, and no
    // Dafny type has a box of one of them together with a box of another type; so a domain that
    // contains every box of T is exactly T's boxes.
    var total = program.SystemModuleManager.TotalArrowTypeDecls[1];
    foreach (var t in new[] { Type.Int, Type.Real, Type.Bool, (Type)new BigOrdinalType() }) {
      var bvars = new List<Bpl.Variable>();
      var b = BplBoundVar("b", Predef.Ty, bvars);
      var fn = BplBoundVar("f", Predef.HandleType, bvars);
      var a = TypeToTy(t);
      var exact = FunctionCall(tok, ExactDom(1), Bpl.Type.Bool, a, fn);
      var isTotal = MkIs(fn, ClassTyCon(total, [a, b]));
      AddOtherDefinition(typeConstructor, new Axiom(tok,
        BplForall(bvars, new Bpl.Trigger(tok, true, [exact, isTotal]), BplImp(isTotal, exact))));
    }
    if (!FunctionExtensionalityAxiom) {
      return;
    }
    sink.AddTopLevelDeclaration(new Bpl.Function(Token.NoToken, FunEq(1),
      [BplFormalVar(null, Predef.Ty, true), BplFormalVar(null, Predef.Ty, true),
        BplFormalVar(null, Predef.HandleType, true), BplFormalVar(null, Predef.HandleType, true)],
      BplFormalVar(null, Bpl.Type.Bool, false)));
    {
      var bvars = new List<Bpl.Variable>();
      var a = BplBoundVar("a", Predef.Ty, bvars);
      var b = BplBoundVar("b", Predef.Ty, bvars);
      var f = BplBoundVar("f", Predef.HandleType, bvars);
      var g = BplBoundVar("g", Predef.HandleType, bvars);
      var eq = FunctionCall(tok, FunEq(1), Bpl.Type.Bool, a, b, f, g);
      AddOtherDefinition(typeConstructor, new Axiom(tok, BplForall(bvars, BplTrigger(eq), BplIff(eq, Bpl.Expr.Eq(f, g)))));
    }
    {
      var bvars = new List<Bpl.Variable>();
      var a = BplBoundVar("a", Predef.Ty, bvars);
      var b = BplBoundVar("b", Predef.Ty, bvars);
      var f = BplBoundVar("f", Predef.HandleType, bvars);
      var g = BplBoundVar("g", Predef.HandleType, bvars);
      var eq = FunctionCall(tok, FunEq(1), Bpl.Type.Bool, a, b, f, g);
      var arrow = ClassTyCon(total, [a, b]);
      var bxVar = new Bpl.BoundVariable(tok, new Bpl.TypedIdent(tok, "bx", Predef.BoxType));
      var bx = new Bpl.IdentifierExpr(tok, bxVar);
      var oneHeap = NewOneHeapExpr(tok);
      var fx = FunctionCall(tok, Apply(1), Predef.BoxType, a, b, oneHeap, f, bx);
      var gx = FunctionCall(tok, Apply(1), Predef.BoxType, a, b, oneHeap, g, bx);
      var pointwise = BplForall(bxVar, new Bpl.Trigger(tok, true, [fx], new Bpl.Trigger(tok, true, [gx])),
        BplImp(MkIs(bx, a, true), Bpl.Expr.Eq(fx, gx)));
      var premises = BplAnd(BplAnd(MkIs(f, arrow), MkIs(g, arrow)),
        BplAnd(FunctionCall(tok, ExactDom(1), Bpl.Type.Bool, a, f), FunctionCall(tok, ExactDom(1), Bpl.Type.Bool, a, g)));
      AddOtherDefinition(typeConstructor, new Axiom(tok,
        BplForall(bvars, BplTrigger(eq), BplImp(BplAnd(premises, pointwise), eq))));
    }
  }

  /// <summary>
  /// With --function-extensionality-axiom, an equality between total arrows of one argument is $FunEq1
  /// at the left operand's type arguments; otherwise null.
  /// </summary>
  internal Bpl.Expr FunctionEquality(IOrigin tok, Type t0, Type t1, Bpl.Expr e0, Bpl.Expr e1) {
    if (!FunctionExtensionalityAxiom || !IsTotalArrowOfOne(t0) || !IsTotalArrowOfOne(t1)) {
      return null;
    }
    var args = t0.AsArrowType.TypeArgs;
    return FunctionCall(tok, FunEq(1), Bpl.Type.Bool, TypeToTy(args[0]), TypeToTy(args[1]), e0, e1);
  }

  private static bool IsTotalArrowOfOne(Type t) {
    return t.AsArrowType is { Arity: 1 } && t.IsArrowTypeWithoutPreconditions && t.IsArrowTypeWithoutReadEffects;
  }

  /// <summary>
  /// The domain of F#Handle is the product of F's parameter types:
  ///   forall tyvars, [layer, reveal, prevHeap, self,] bx.. :: $FunDomN(F#Handle(..), bx..) <==> $IsBox(bx1, T1) && ..
  /// </summary>
  private void AddFunctionHandleDomainAxiom(Function f, Bpl.Expr fhandle, List<Bpl.Variable> handleVars,
    List<Bpl.Variable> boxVars, List<Bpl.Expr> boxes) {
    var dom = FunctionCall(f.Origin, FunDom(f.Ins.Count), Bpl.Type.Bool, Cons(fhandle, boxes));
    AddOtherDefinition(GetOrCreateFunction(f), new Axiom(f.Origin,
      BplForall(Concat(handleVars, boxVars), BplTrigger(dom), BplIff(dom, FunctionHandleDomainGuard(f, boxes)))));
  }

  private Bpl.Expr FunctionHandleDomainGuard(Function f, List<Bpl.Expr> boxes) {
    return BplAnd(boxes.Zip(f.Ins, (b, formal) => MkIsBox(b, formal.Type)));
  }

  /// <summary>
  /// Returns "restrict" or "exactDom" when f is a well-formed {:functionDomain} declaration:
  ///   ghost function {:axiom} {:functionDomain "restrict"} Restrict<A, B>(f: A -> B): A -> B
  ///   ghost predicate {:axiom} {:functionDomain "exactDom"} ExactDom<A, B>(f: A -> B)
  /// with no body, precondition, postcondition or reads clause.  Otherwise reports an error
  /// for a malformed one and returns null.
  /// </summary>
  private string FunctionDomainKind(Function f) {
    var args = Attributes.FindExpressions(f.Attributes, FunctionDomainAttributeName);
    if (args == null) {
      return null;
    }
    string kind = args.Count == 1 && args[0] is StringLiteralExpr { Value: string s } ? s : null;
    var ok = (kind == "restrict" || kind == "exactDom") &&
             f.Body == null && f.Req.Count == 0 && f.Ens.Count == 0 && f.Reads.Expressions.Count == 0 &&
             f.IsStatic && f.EnclosingClass is DefaultClassDecl && f.TypeArgs.Count == 2 && f.Ins.Count == 1 &&
             !f.IsFuelAware() && !f.IsOpaque && !f.IsMadeImplicitlyOpaque(options) && f is not TwoStateFunction &&
             IsTotalArrowOver(f.Ins[0].Type, f.TypeArgs[0], f.TypeArgs[1]) &&
             (kind == "restrict" ? IsTotalArrowOver(f.ResultType, f.TypeArgs[0], f.TypeArgs[1]) : f.ResultType.IsBoolType);
    if (!ok) {
      reporter.Error(MessageSource.Verifier, f.Origin,
        "{:" + FunctionDomainAttributeName + "} expects \"restrict\" on a body-less ghost function <A, B>(f: A -> B): A -> B, " +
        "or \"exactDom\" on a body-less ghost predicate <A, B>(f: A -> B), with no other specification");
      return null;
    }
    return kind;
  }

  private bool IsTotalArrowOver(Type t, TypeParameter a, TypeParameter b) {
    return program.SystemModuleManager.TotalArrowTypeDecls.TryGetValue(1, out var total) &&
           t is UserDefinedType { ResolvedClass: var cl } udt && cl == total && udt.TypeArgs.Count == 2 &&
           udt.TypeArgs[0].AsTypeParameter == a && udt.TypeArgs[1].AsTypeParameter == b;
  }

  /// <summary>
  /// For Restrict(A, B, f):
  ///   $FunDom1(Restrict(A, B, f), bx) <==> $IsBox(bx, A)
  ///   $IsBox(bx, A) ==> Apply1(t0, t1, heap, Restrict(A, B, f), bx) == Apply1(t0, t1, heap, f, bx), and so for Requires1 and Reads1
  ///   $ExactDom1(A, f) ==> Restrict(A, B, f) == f
  /// For ExactDom(A, B, f):
  ///   ExactDom(A, B, f) <==> $ExactDom1(A, f)
  /// </summary>
  private void AddFunctionDomainAxioms(Function f) {
    var kind = FunctionDomainKind(f);
    if (kind == null) {
      return;
    }
    var tok = f.Origin;
    var name = f.FullSanitizedName;
    if (kind == "restrict") {
      {
        var bvars = new List<Bpl.Variable>();
        var a = BplBoundVar("A", Predef.Ty, bvars);
        var b = BplBoundVar("B", Predef.Ty, bvars);
        var fn = BplBoundVar("f", Predef.HandleType, bvars);
        var bx = BplBoundVar("bx", Predef.BoxType, bvars);
        var restricted = FunctionCall(tok, name, Predef.HandleType, a, b, fn);
        var dom = FunctionCall(tok, FunDom(1), Bpl.Type.Bool, restricted, bx);
        AddOtherDefinition(GetOrCreateFunction(f), new Axiom(tok,
          BplForall(bvars, BplTrigger(dom), BplIff(dom, MkIs(bx, a, true))), "domain of " + name));
      }
      void Selector(string selector, Bpl.Type ty, bool member) {
        var bvars = new List<Bpl.Variable>();
        var a = BplBoundVar("A", Predef.Ty, bvars);
        var b = BplBoundVar("B", Predef.Ty, bvars);
        var fn = BplBoundVar("f", Predef.HandleType, bvars);
        var t0 = BplBoundVar("t0", Predef.Ty, bvars);
        var t1 = BplBoundVar("t1", Predef.Ty, bvars);
        var heap = BplBoundVar("heap", Predef.HeapType, bvars);
        var bx = BplBoundVar("bx", Predef.BoxType, bvars);
        var restricted = FunctionCall(tok, name, Predef.HandleType, a, b, fn);
        Bpl.Expr lhs = FunctionCall(tok, selector, ty, t0, t1, heap, restricted, bx);
        Bpl.Expr rhs = FunctionCall(tok, selector, ty, t0, t1, heap, fn, bx);
        if (member) {
          var o = BplBoundVar("o", Predef.BoxType, bvars);
          lhs = IsSetMember(tok, lhs, o, true);
          rhs = IsSetMember(tok, rhs, o, true);
        }
        AddOtherDefinition(GetOrCreateFunction(f), new Axiom(tok,
          BplForall(bvars, BplTrigger(lhs), BplImp(MkIs(bx, a, true), Bpl.Expr.Eq(lhs, rhs))), selector + " of " + name));
      }
      Selector(Apply(1), Predef.BoxType, false);
      Selector(Requires(1), Bpl.Type.Bool, false);
      Selector(Reads(1), TrType(program.SystemModuleManager.ObjectSetType()), true);
      {
        var bvars = new List<Bpl.Variable>();
        var a = BplBoundVar("A", Predef.Ty, bvars);
        var b = BplBoundVar("B", Predef.Ty, bvars);
        var fn = BplBoundVar("f", Predef.HandleType, bvars);
        var restricted = FunctionCall(tok, name, Predef.HandleType, a, b, fn);
        var exact = FunctionCall(tok, ExactDom(1), Bpl.Type.Bool, a, fn);
        AddOtherDefinition(GetOrCreateFunction(f), new Axiom(tok,
          BplForall(bvars, BplTrigger(restricted), BplImp(exact, Bpl.Expr.Eq(restricted, fn))), "exact domain of " + name));
      }
    } else {
      var bvars = new List<Bpl.Variable>();
      var a = BplBoundVar("A", Predef.Ty, bvars);
      var b = BplBoundVar("B", Predef.Ty, bvars);
      var fn = BplBoundVar("f", Predef.HandleType, bvars);
      var exact = FunctionCall(tok, name, Bpl.Type.Bool, a, b, fn);
      AddOtherDefinition(GetOrCreateFunction(f), new Axiom(tok,
        BplForall(bvars, BplTrigger(exact), BplIff(exact, FunctionCall(tok, ExactDom(1), Bpl.Type.Bool, a, fn))),
        "definition of " + name));
    }
  }
}
