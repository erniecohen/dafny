// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// The fixed integer reference interface (issue 193, M2). Source declarations cannot
/// supply these equations. The functions have raw integer results; in particular no
/// range theorem is assumed while the companion proves agreement with the model.
/// Only the explicit unfold lemmas expose defining equations, one step per call.
/// </summary>
internal sealed class IntegerBitwiseReferences : ClassDecl {
  internal readonly Dictionary<string, Function> References = new();

  // Both the nullable class and its non-null view carry the compiler identity.
  internal static bool IsReferenceDecl(TopLevelDecl declaration) =>
    declaration is IntegerBitwiseReferences or NonNullTypeDecl { Class: IntegerBitwiseReferences };

  internal IntegerBitwiseReferences(SystemModuleManager builtins)
    : base(SourceOrigin.NoToken, new Name("IntegerBitwiseModel"),
      new Attributes("compile", [Expression.CreateBoolLiteral(Token.NoToken, false)], null),
      [], builtins.SystemModule, [], [], false) {
    builtins.CreateArrowTypeDecl(2);
    builtins.CreateArrowTypeDecl(3);
    var shapes = new Dictionary<string, string[]> {
      ["P"] = ["n"], ["norm"] = ["n", "a"],
      ["andNat"] = ["n", "a", "b"], ["notNat"] = ["n", "a"],
      ["orNat"] = ["n", "a", "b"], ["xorNat"] = ["n", "a", "b"],
      ["andNotNat"] = ["n", "a", "b"],
      ["shlNat"] = ["n", "a", "k"], ["shrNat"] = ["n", "a", "k"],
      ["rolNat"] = ["n", "a", "k"], ["rorNat"] = ["n", "a", "k"],
      ["popNat"] = ["n", "a"]
    };
    List<Formal> Formals(string[] names) => names.Select(name =>
      new Formal(Token.NoToken, name, name == "n" ? builtins.Nat() : Type.Int, true, true, null)).ToList();
    void Add(MemberDecl member) {
      member.EnclosingClass = this;
      member.AddVisibilityScope(builtins.SystemModule.VisibilityScope, false);
      Members.Add(member);
    }
    foreach (var (name, arguments) in shapes) {
      var function = new Function(SourceOrigin.NoToken, new Name(name), true, true, false,
        [], Formals(arguments), null, Type.Int, [], new Specification<FrameExpression>([], null),
        [], new Specification<Expression>([], null), null, null, null,
        new Attributes("transparent", [], null), null);
      References.Add(name, function);
      Add(function);
    }
    Expression Call(string name, params Expression[] arguments) => Expression.CreateResolvedCall(Token.NoToken,
      new StaticReceiverExpr(Token.NoToken, new UserDefinedType(Token.NoToken, Name, this, []), false) {
        Type = new UserDefinedType(Token.NoToken, Name, this, [])
      },
      References[name], arguments.ToList(), [], builtins);
    Expression Num(int value) => Expression.CreateIntLiteral(Token.NoToken, value);
    Expression Op(BinaryExpr.Opcode op, Expression a, Expression b) => new BinaryExpr(Token.NoToken, op, a, b) {
      ResolvedOp = op switch {
        BinaryExpr.Opcode.Add => BinaryExpr.ResolvedOpcode.Add,
        BinaryExpr.Opcode.Sub => BinaryExpr.ResolvedOpcode.Sub,
        BinaryExpr.Opcode.Mul => BinaryExpr.ResolvedOpcode.Mul,
        BinaryExpr.Opcode.Div => BinaryExpr.ResolvedOpcode.Div,
        BinaryExpr.Opcode.Mod => BinaryExpr.ResolvedOpcode.Mod,
        BinaryExpr.Opcode.Lt => BinaryExpr.ResolvedOpcode.Lt,
        BinaryExpr.Opcode.Le => BinaryExpr.ResolvedOpcode.Le,
        BinaryExpr.Opcode.And => BinaryExpr.ResolvedOpcode.And,
        BinaryExpr.Opcode.Or => BinaryExpr.ResolvedOpcode.Or,
        _ => throw new System.ArgumentOutOfRangeException(nameof(op))
      },
      Type = op is BinaryExpr.Opcode.Lt or BinaryExpr.Opcode.Le or BinaryExpr.Opcode.And or BinaryExpr.Opcode.Or
        ? Type.Bool : Type.Int
    };
    Expression Eq(Expression a, Expression b) => Expression.CreateEq(a, b, Type.Int);
    Expression If(Expression test, Expression yes, Expression no) => Expression.CreateITE(test, yes, no);
    Expression AddE(Expression a, Expression b) => Op(BinaryExpr.Opcode.Add, a, b);
    Expression Sub(Expression a, Expression b) => Op(BinaryExpr.Opcode.Sub, a, b);
    Expression Mul(Expression a, Expression b) => Op(BinaryExpr.Opcode.Mul, a, b);
    Expression Div(Expression a, Expression b) => Op(BinaryExpr.Opcode.Div, a, b);
    Expression Mod(Expression a, Expression b) => Op(BinaryExpr.Opcode.Mod, a, b);
    Expression Lt(Expression a, Expression b) => Op(BinaryExpr.Opcode.Lt, a, b);
    Expression And(Expression a, Expression b) => Op(BinaryExpr.Opcode.And, a, b);
    Expression Or(Expression a, Expression b) => Op(BinaryExpr.Opcode.Or, a, b);

    foreach (var (name, arguments) in shapes) {
      var formals = Formals(arguments);
      var variables = formals.ToDictionary(f => f.Name, f => Expression.CreateIdentExpr(f));
      var n = variables["n"];
      var a = variables.GetValueOrDefault("a");
      var b = variables.GetValueOrDefault("b");
      var k = variables.GetValueOrDefault("k");
      var p = Call("P", n);
      var previous = Sub(n, Num(1));
      Expression Norm(Expression x) => Call("norm", n, x);
      Expression Not(Expression x) => Call("notNat", n, x);
      Expression Both(Expression x, Expression y) => Call("andNat", n, x, y);
      var r = k == null ? null : Mod(k, n);
      var u = a == null ? null : Norm(a);
      var outside = k == null ? null : Or(Lt(k, Num(0)), Op(BinaryExpr.Opcode.Le, n, k));
      var body = name switch {
        "P" => If(Eq(n, Num(0)), Num(1), Mul(Num(2), Call("P", previous))),
        "norm" => Mod(a, p),
        "andNat" => If(Eq(n, Num(0)), Num(0), AddE(
          If(And(Eq(Mod(a, Num(2)), Num(1)), Eq(Mod(b, Num(2)), Num(1))), Num(1), Num(0)),
          Mul(Num(2), Call("andNat", previous, Div(a, Num(2)), Div(b, Num(2)))))),
        "notNat" => Sub(Sub(p, Num(1)), u),
        "orNat" => Not(Both(Not(a), Not(b))),
        "xorNat" => Both(Call("orNat", n, a, b), Not(Both(a, b))),
        "andNotNat" => Both(a, Not(b)),
        "shlNat" => If(outside, Num(0), Mod(Mul(u, Call("P", k)), p)),
        "shrNat" => If(outside, Num(0), Div(u, Call("P", k))),
        "rolNat" => If(Eq(n, Num(0)), Num(0), AddE(Mod(Mul(u, Call("P", r)), p),
          Div(u, Call("P", Sub(n, r))))),
        "rorNat" => If(Eq(n, Num(0)), Num(0), AddE(Div(u, Call("P", r)),
          Mul(Mod(u, Call("P", r)), Call("P", Sub(n, r))))),
        "popNat" => If(Eq(n, Num(0)), Num(0), AddE(Mod(a, Num(2)), Call("popNat", previous, Div(a, Num(2))))),
        _ => throw new System.ArgumentOutOfRangeException(nameof(name))
      };
      // Retain source-definedness of every division/modulus. Positivity is proved
      // from the raw P interface before any of these lemmas may be called.
      Expression positive = name switch {
        "norm" => Lt(Num(0), p),
        "shlNat" => Or(outside, Lt(Num(0), p)),
        "shrNat" => Or(outside, Lt(Num(0), Call("P", k))),
        "rolNat" => Or(Eq(n, Num(0)), And(Lt(Num(0), p), Lt(Num(0), Call("P", Sub(n, r))))),
        "rorNat" => Or(Eq(n, Num(0)), Lt(Num(0), Call("P", r))),
        _ => null
      };
      var lemma = new Lemma(SourceOrigin.NoToken, new Name("unfold" + char.ToUpperInvariant(name[0]) + name[1..].Replace("Nat", "")), true, [], formals, [],
        positive == null ? [] : [new AttributedExpression(positive)],
        new Specification<FrameExpression>([], null), new Specification<FrameExpression>([], null),
        [new AttributedExpression(Eq(Call(name, formals.Select(Expression.CreateIdentExpr).ToArray()), body))],
        new Specification<Expression>([], null), null, SystemModuleManager.AxiomAttribute(), null);
      Add(lemma);
    }
  }
}
