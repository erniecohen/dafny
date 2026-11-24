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
    AddExactDomainAxioms(ad);
  }

  /// <summary>
  /// ExactDomN compares a handle's domain with the product of the given types' box sets.
  /// Under --function-extensionality-axiom, FunEqN is Leibniz equality, with a second
  /// axiom deriving it from totality, exact domains and pointwise agreement at OneHeap.
  /// The empty product is the singleton nullary argument tuple.
  /// </summary>
  private void AddExactDomainAxioms(ArrowTypeDecl ad) {
    var arity = ad.Arity;
    var tok = ad.Origin;
    var typeConstructor = GetOrCreateTypeConstructor(ad);
    sink.AddTopLevelDeclaration(new Bpl.Function(Token.NoToken, ExactDom(arity),
      Enumerable.Range(0, arity).Select(_ => (Bpl.Variable)BplFormalVar(null, Predef.Ty, true))
        .Append(BplFormalVar(null, Predef.HandleType, true)).ToList(),
      BplFormalVar(null, Bpl.Type.Bool, false)));
    {
      var bvars = new List<Bpl.Variable>();
      var domains = Map(Enumerable.Range(0, arity), i => BplBoundVar(arity == 1 ? "a" : "a" + i, Predef.Ty, bvars));
      var fn = BplBoundVar("f", Predef.HandleType, bvars);
      var exact = FunctionCall(tok, ExactDom(arity), Bpl.Type.Bool, Snoc(domains, fn));
      var boxVars = new List<Bpl.Variable>();
      var boxes = Map(Enumerable.Range(0, arity), i => BplBoundVar(arity == 1 ? "bx" : "bx" + i, Predef.BoxType, boxVars));
      var dom = FunctionCall(tok, FunDom(arity), Bpl.Type.Bool, Cons(fn, boxes));
      var inDomain = BplAnd(boxes.Zip(domains, (bx, a) => MkIs(bx, a, true)));
      var same = BplForall(boxVars, BplTrigger(dom), BplIff(dom, inDomain));
      AddOtherDefinition(typeConstructor, new Axiom(tok, BplForall(bvars, BplTrigger(exact), BplIff(exact, same))));
    }
    // A basic domain has no proper supertype. A product of basic domains therefore
    // cannot be the proper subset of a domain built by the translator. This includes
    // the empty product. Keep the unary axioms unchanged for --additional-axioms alone.
    var total = program.SystemModuleManager.TotalArrowTypeDecls[arity];
    var basicTypes = new[] { Type.Int, Type.Real, Type.Bool, (Type)new BigOrdinalType() };
    if (arity == 1) {
      foreach (var t in basicTypes) {
        var bvars = new List<Bpl.Variable>();
        var b = BplBoundVar("b", Predef.Ty, bvars);
        var fn = BplBoundVar("f", Predef.HandleType, bvars);
        var a = TypeToTy(t);
        var exact = FunctionCall(tok, ExactDom(1), Bpl.Type.Bool, a, fn);
        var isTotal = MkIs(fn, ClassTyCon(total, [a, b]));
        AddOtherDefinition(typeConstructor, new Axiom(tok,
          BplForall(bvars, new Bpl.Trigger(tok, true, [exact, isTotal]), BplImp(isTotal, exact))));
      }
    } else {
      var bvars = new List<Bpl.Variable>();
      var domains = Map(Enumerable.Range(0, arity), i => BplBoundVar("a" + i, Predef.Ty, bvars));
      var b = BplBoundVar("b", Predef.Ty, bvars);
      var fn = BplBoundVar("f", Predef.HandleType, bvars);
      var exact = FunctionCall(tok, ExactDom(arity), Bpl.Type.Bool, Snoc(domains, fn));
      var isTotal = MkIs(fn, ClassTyCon(total, Snoc(domains, b)));
      var basicDomains = BplAnd(domains.Select(a =>
        BplOr(basicTypes.Select(t => Bpl.Expr.Eq(a, TypeToTy(t))))));
      AddOtherDefinition(typeConstructor, new Axiom(tok,
        BplForall(bvars, new Bpl.Trigger(tok, true, [exact, isTotal]),
          BplImp(BplAnd(isTotal, basicDomains), exact))));
    }
    if (!FunctionExtensionalityAxiom) {
      return;
    }
    sink.AddTopLevelDeclaration(new Bpl.Function(Token.NoToken, FunEq(arity),
      Enumerable.Range(0, arity + 1).Select(_ => (Bpl.Variable)BplFormalVar(null, Predef.Ty, true))
        .Concat(new[] { BplFormalVar(null, Predef.HandleType, true), BplFormalVar(null, Predef.HandleType, true) }).ToList(),
      BplFormalVar(null, Bpl.Type.Bool, false)));
    {
      var bvars = new List<Bpl.Variable>();
      var types = Map(Enumerable.Range(0, arity + 1), i => BplBoundVar("t" + i, Predef.Ty, bvars));
      var f = BplBoundVar("f", Predef.HandleType, bvars);
      var g = BplBoundVar("g", Predef.HandleType, bvars);
      var eq = FunctionCall(tok, FunEq(arity), Bpl.Type.Bool, Concat(types, new List<Bpl.Expr> { f, g }));
      AddOtherDefinition(typeConstructor, new Axiom(tok, BplForall(bvars, BplTrigger(eq), BplIff(eq, Bpl.Expr.Eq(f, g)))));
    }
    {
      var bvars = new List<Bpl.Variable>();
      var domains = Map(Enumerable.Range(0, arity), i => BplBoundVar("a" + i, Predef.Ty, bvars));
      var b = BplBoundVar("b", Predef.Ty, bvars);
      var types = Snoc(domains, b);
      var f = BplBoundVar("f", Predef.HandleType, bvars);
      var g = BplBoundVar("g", Predef.HandleType, bvars);
      var eq = FunctionCall(tok, FunEq(arity), Bpl.Type.Bool, Concat(types, new List<Bpl.Expr> { f, g }));
      var arrow = ClassTyCon(total, types);
      var boxVars = new List<Bpl.Variable>();
      var boxes = Map(Enumerable.Range(0, arity), i => BplBoundVar("bx" + i, Predef.BoxType, boxVars));
      var oneHeap = NewOneHeapExpr(tok);
      var fx = FunctionCall(tok, Apply(arity), Predef.BoxType, Concat(types, Cons(oneHeap, Cons(f, boxes))));
      var gx = FunctionCall(tok, Apply(arity), Predef.BoxType, Concat(types, Cons(oneHeap, Cons(g, boxes))));
      var inDomain = BplAnd(boxes.Zip(domains, (bx, a) => MkIs(bx, a, true)));
      var pointwise = BplForall(boxVars, new Bpl.Trigger(tok, true, [fx], new Bpl.Trigger(tok, true, [gx])),
        BplImp(inDomain, Bpl.Expr.Eq(fx, gx)));
      var premises = BplAnd(BplAnd(MkIs(f, arrow), MkIs(g, arrow)),
        BplAnd(FunctionCall(tok, ExactDom(arity), Bpl.Type.Bool, Snoc(domains, f)),
          FunctionCall(tok, ExactDom(arity), Bpl.Type.Bool, Snoc(domains, g))));
      AddOtherDefinition(typeConstructor, new Axiom(tok,
        BplForall(bvars, BplTrigger(eq), BplImp(BplAnd(premises, pointwise), eq))));
    }
  }

  /// <summary>
  /// With --function-extensionality-axiom, an equality between total arrows is FunEqN
  /// at the left operand's type arguments; otherwise null.
  /// </summary>
  internal Bpl.Expr FunctionEquality(IOrigin tok, Type t0, Type t1, Bpl.Expr e0, Bpl.Expr e1) {
    if (!FunctionExtensionalityAxiom || !IsTotalArrow(t0) || !IsTotalArrow(t1)) {
      return null;
    }
    var arrow = t0.AsArrowType;
    return FunctionCall(tok, FunEq(arrow.Arity), Bpl.Type.Bool,
      arrow.TypeArgs.Select(TypeToTy).Concat(new[] { e0, e1 }).ToList());
  }

  private static bool IsTotalArrow(Type t) {
    return t.AsArrowType != null && t.IsArrowTypeWithoutPreconditions && t.IsArrowTypeWithoutReadEffects;
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
  /// Recognizes the intrinsic domain operations at any arity. Restrict may also have
  /// the checked eta-expansion body (x_1, ..., x_N) => f(x_1, ..., x_N).
  /// The explicit-call equality predicate must have the ordinary body f == g;
  /// its extensionality instance is triggered only by a call of that predicate.
  /// Pointwise exposes agreement over the full product without allocation guards.
  /// </summary>
  private string FunctionDomainKind(Function f) {
    var args = Attributes.FindExpressions(f.Attributes, FunctionDomainAttributeName);
    if (args == null) {
      return null;
    }
    string kind = args.Count == 1 && args[0] is StringLiteralExpr { Value: string s } ? s : null;
    var pair = kind is "equal" or "pointwise";
    var signature = (kind is "restrict" or "exactDom" or "equal" or "pointwise") &&
                    f.Req.Count == 0 && f.Ens.Count == 0 && f.Reads.Expressions.Count == 0 &&
                    f.IsGhost && f.IsStatic && f.EnclosingClass is DefaultClassDecl &&
                    f.TypeArgs.Count >= 1 && f.Ins.Count == (pair ? 2 : 1) &&
                    (kind != "pointwise" || f.Ins.All(p => !p.IsOlder)) &&
                    (kind != "pointwise" || f.ResultType.NormalizeExpandKeepConstraints() is BoolType) &&
                    !f.IsFuelAware() && !f.IsOpaque && !f.IsMadeImplicitlyOpaque(options) &&
                    f is not TwoStateFunction &&
                    f.Ins.All(p => IsTotalArrowOver(p.Type, f.TypeArgs)) &&
                    (kind == "restrict" ? IsTotalArrowOver(f.ResultType, f.TypeArgs) : f.ResultType.IsBoolType);
    var body = signature && (kind switch {
      "restrict" => f.Body == null || IsRestrictionBody(f),
      "exactDom" or "pointwise" => f.Body == null,
      "equal" => f.Body?.Resolved is BinaryExpr { Op: BinaryExpr.Opcode.Eq } eq &&
                 IsParameter(eq.E0, f.Ins[0]) && IsParameter(eq.E1, f.Ins[1]),
      _ => false
    });
    if (!body) {
      reporter.Error(MessageSource.Verifier, f.Origin,
        kind == "pointwise"
          ? "{:functionDomain \"pointwise\"} expects a static body-less ghost predicate with type parameters " +
            "<A1, ..., An, B>, arguments (f: (A1, ..., An) -> B, g: (A1, ..., An) -> B), and no other specification"
          : "{:" + FunctionDomainAttributeName + "} expects a static ghost function with type parameters <A1, ..., An, B> " +
        "and no other specification: \"restrict\" on (f: (A1, ..., An) -> B): (A1, ..., An) -> B, " +
        "body-less or with body (x1: A1, ..., xn: An) => f(x1, ..., xn); \"exactDom\" on a body-less " +
        "predicate (f: (A1, ..., An) -> B); or \"equal\" on a predicate (f: (A1, ..., An) -> B, " +
        "g: (A1, ..., An) -> B) with body f == g");
      return null;
    }
    return kind;
  }

  private static bool IsParameter(Expression e, IVariable parameter) {
    return e.Resolved is IdentifierExpr id && id.Var == parameter;
  }

  private bool IsTotalArrowOver(Type t, List<TypeParameter> parameters) {
    var arity = parameters.Count - 1;
    return program.SystemModuleManager.TotalArrowTypeDecls.TryGetValue(arity, out var total) &&
           t is UserDefinedType { ResolvedClass: var cl } udt && cl == total &&
           udt.TypeArgs.Count == parameters.Count &&
           udt.TypeArgs.Zip(parameters, (actual, formal) => actual.AsTypeParameter == formal).All(x => x);
  }

  private static bool IsRestrictionBody(Function f) {
    return f.Body.Resolved is LambdaExpr lambda && lambda.Range == null &&
           lambda.Reads.Expressions.Count == 0 && lambda.BoundVars.Count == f.TypeArgs.Count - 1 &&
           lambda.BoundVars.Zip(f.TypeArgs, (v, t) => v.Type.AsTypeParameter == t).All(x => x) &&
           lambda.Body.Resolved is ApplyExpr apply && IsParameter(apply.Function, f.Ins[0]) &&
           apply.Args.Count == lambda.BoundVars.Count &&
           apply.Args.Zip(lambda.BoundVars, (e, v) => IsParameter(e, v)).All(x => x);
  }

  /// <summary>
  /// Restrict's domain is the product of its argument types. Its application agrees
  /// with f on that product, and restriction is the identity exactly on exact domains.
  /// ExactDom compares sets of argument tuples, including the singleton empty tuple.
  /// Equal adds the existing extensionality rule at an explicit predicate call; its
  /// checked source body supplies the implication from the predicate to Leibniz equality.
  /// </summary>
  private void AddFunctionDomainAxioms(Function f) {
    var kind = FunctionDomainKind(f);
    if (kind == null) {
      return;
    }
    var tok = f.Origin;
    var name = f.FullSanitizedName;
    var arity = f.TypeArgs.Count - 1;
    var bvars = new List<Bpl.Variable>();
    var domains = Map(Enumerable.Range(0, arity), i => BplBoundVar("A" + i, Predef.Ty, bvars));
    var b = BplBoundVar("B", Predef.Ty, bvars);
    var types = Snoc(domains, b);
    var fn = BplBoundVar("f", Predef.HandleType, bvars);
    var callArgs = Snoc(types, fn);
    var exact = FunctionCall(tok, ExactDom(arity), Bpl.Type.Bool, Snoc(domains, fn));
    if (kind == "restrict") {
      var restricted = FunctionCall(tok, name, Predef.HandleType, callArgs);
      {
        var vars = new List<Bpl.Variable>(bvars);
        var boxes = Map(Enumerable.Range(0, arity), i => BplBoundVar("bx" + i, Predef.BoxType, vars));
        var dom = FunctionCall(tok, FunDom(arity), Bpl.Type.Bool, Cons(restricted, boxes));
        var inDomain = BplAnd(boxes.Zip(domains, (bx, a) => MkIs(bx, a, true)));
        AddOtherDefinition(GetOrCreateFunction(f), new Axiom(tok,
          BplForall(vars, BplTrigger(dom), BplIff(dom, inDomain)), "domain of " + name));
      }
      {
        var vars = new List<Bpl.Variable>(bvars);
        var applyTypes = Map(Enumerable.Range(0, arity + 1), i => BplBoundVar("t" + i, Predef.Ty, vars));
        var heap = BplBoundVar("heap", Predef.HeapType, vars);
        var boxes = Map(Enumerable.Range(0, arity), i => BplBoundVar("bx" + i, Predef.BoxType, vars));
        var lhs = FunctionCall(tok, Apply(arity), Predef.BoxType, Concat(applyTypes, Cons(heap, Cons(restricted, boxes))));
        var rhs = FunctionCall(tok, Apply(arity), Predef.BoxType, Concat(applyTypes, Cons(heap, Cons(fn, boxes))));
        var inDomain = BplAnd(boxes.Zip(domains, (bx, a) => MkIs(bx, a, true)));
        AddOtherDefinition(GetOrCreateFunction(f), new Axiom(tok,
          BplForall(vars, BplTrigger(lhs), BplImp(inDomain, Bpl.Expr.Eq(lhs, rhs))), Apply(arity) + " of " + name));
      }
      AddOtherDefinition(GetOrCreateFunction(f), new Axiom(tok,
        BplForall(bvars, BplTrigger(restricted), BplImp(exact, Bpl.Expr.Eq(restricted, fn))), "exact domain of " + name));
    } else if (kind == "exactDom") {
      var call = FunctionCall(tok, name, Bpl.Type.Bool, callArgs);
      AddOtherDefinition(GetOrCreateFunction(f), new Axiom(tok,
        BplForall(bvars, BplTrigger(call), BplIff(call, exact)), "definition of " + name));
    } else {
      var g = BplBoundVar("g", Predef.HandleType, bvars);
      var call = FunctionCall(tok, name, Bpl.Type.Bool, Snoc(callArgs, g));
      var arrow = ClassTyCon(program.SystemModuleManager.TotalArrowTypeDecls[arity], types);
      var boxVars = new List<Bpl.Variable>();
      var boxes = Map(Enumerable.Range(0, arity), i => BplBoundVar("bx" + i, Predef.BoxType, boxVars));
      var heap = NewOneHeapExpr(tok);
      var fx = FunctionCall(tok, Apply(arity), Predef.BoxType, Concat(types, Cons(heap, Cons(fn, boxes))));
      var gx = FunctionCall(tok, Apply(arity), Predef.BoxType, Concat(types, Cons(heap, Cons(g, boxes))));
      var inDomain = BplAnd(boxes.Zip(domains, (bx, a) => MkIs(bx, a, true)));
      var pointwise = BplForall(boxVars, new Bpl.Trigger(tok, true, [fx], new Bpl.Trigger(tok, true, [gx])),
        BplImp(inDomain, Bpl.Expr.Eq(fx, gx)));
      if (kind == "pointwise") {
        // Deliberately no allocation antecedent: this is agreement on the whole
        // mathematical product, including arguments not allocated in any current heap.
        AddOtherDefinition(GetOrCreateFunction(f), new Axiom(tok,
          BplForall(bvars, BplTrigger(call),
            BplImp(BplAnd(MkIs(fn, arrow), MkIs(g, arrow)), BplIff(call, pointwise))),
          "allocation-independent pointwise agreement of " + name));
        return;
      }
      var premises = BplAnd(BplAnd(MkIs(fn, arrow), MkIs(g, arrow)),
        BplAnd(exact, FunctionCall(tok, ExactDom(arity), Bpl.Type.Bool, Snoc(domains, g))));
      AddOtherDefinition(GetOrCreateFunction(f), new Axiom(tok,
        BplForall(bvars, BplTrigger(call), BplImp(BplAnd(premises, pointwise), call)),
        "explicit extensionality of " + name));
    }
  }
}
