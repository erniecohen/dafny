// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Reflection;
using Bpl = Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace Microsoft.Dafny;

#nullable disable

/// <summary>Bounded typed field view; it detects mutation, never licenses a source root.</summary>
internal sealed class B3IfGuardSnapshot {
  private sealed record Atom(object Value, bool ByReference);
  private readonly Atom[] fields;
  private B3IfGuardSnapshot(Atom[] fields) { this.fields = fields; }

  internal static B3IfGuardSnapshot Capture(Bpl.Expr guard, ref int nodes, ref long bytes) {
    var reader = new Reader(null, nodes, bytes, guard.tok);
    reader.Expression(guard, 0);
    nodes = reader.Nodes; bytes = reader.Bytes;
    return new B3IfGuardSnapshot(reader.Fields.ToArray());
  }
  internal void Recheck(Bpl.Expr guard, ref int nodes, ref long bytes) {
    var reader = new Reader(fields, nodes, bytes, guard.tok);
    reader.Expression(guard, 0);
    Require(reader.Position == fields.Length, "Guard field inventory changed after capture", guard.tok);
    nodes = reader.Nodes; bytes = reader.Bytes;
  }
  private sealed class Reader(Atom[] expected, int nodes, long bytes, Bpl.IToken token) {
    internal readonly List<Atom> Fields = new();
    internal int Nodes = nodes;
    internal long Bytes = bytes;
    internal int Position;
    // Pinned TypeProxy.ProxyFor compresses paths. Read only this one audited
    // backing field, as in B3UnsignedWrappers.Width; never use reflective equality.
    private static readonly FieldInfo ProxyTarget = typeof(Bpl.TypeProxy).GetField("proxyFor", BindingFlags.Instance | BindingFlags.NonPublic);
    private void Enter(int depth) => Require(depth < Ir.Protocol.MaximumDepth && ++Nodes <= Ir.Protocol.MaximumNodes,
      "Guard field traversal exceeds its node/depth bound", token);
    private void Add(object value, bool byReference) {
      Bytes += 64 + (value is string text ? 4L * text.Length : 0);
      Require(++Position <= Ir.Protocol.MaximumNodes && Bytes <= Ir.Protocol.MaximumMessageBytes,
        "Guard field inventory exceeds its byte/field bound", token);
      if (expected == null) { Fields.Add(new Atom(value, byReference)); return; }
      Require(Position <= expected.Length && expected[Position - 1].ByReference == byReference &&
        (byReference ? ReferenceEquals(expected[Position - 1].Value, value) : Equals(expected[Position - 1].Value, value)),
        "Guard semantic field changed after capture", token);
    }
    private void Ref(object value) => Add(value, true);
    private void Val(object value) => Add(value, false);
    private void Type(Bpl.Type type, int depth) {
      Enter(depth); Ref(type);
      if (type == null) { return; }
      Ref(type.GetType());
      switch (type) {
        case Bpl.TypeProxy proxy:
          Require(ProxyTarget != null, "Pinned type proxy field is unavailable", token);
          Type((Bpl.Type)ProxyTarget.GetValue(proxy), depth + 1); break;
        case Bpl.BasicType basic: Val(basic.T); break;
        case Bpl.BvType word: Val(word.Bits); break;
        case Bpl.TypeVariable: break;
        case Bpl.CtorType ctor:
          Ref(ctor.Decl); Types(ctor.Arguments, depth + 1); break;
        case Bpl.TypeSynonymAnnotation synonym:
          Ref(synonym.Decl); Types(synonym.Arguments, depth + 1); Type(synonym.ExpandedType, depth + 1); break;
        case Bpl.MapType map:
          Types(map.TypeParameters, depth + 1); Types(map.Arguments, depth + 1); Type(map.Result, depth + 1); break;
        default: throw new B3UnsignedWrappers.Rejection("Unsupported type constructor in guard field view", token);
      }
    }
    private void Types<T>(IList<T> types, int depth) where T : Bpl.Type {
      Require(types != null && types.Count <= Ir.Protocol.MaximumNodes, "Guard type list exceeds its bound", token);
      Val(types.Count); foreach (var type in types) { Type(type, depth); }
    }
    private void Variable(Bpl.Variable variable, int depth) {
      Require(variable != null && variable.TypedIdent != null, "Guard variable is unresolved", token);
      Enter(depth); Ref(variable); Ref(variable.GetType()); Ref(variable.TypedIdent);
      Type(variable.TypedIdent.Type, depth + 1); Expression(variable.TypedIdent.WhereExpr, depth + 1);
      Attributes(variable.Attributes, depth + 1);
    }
    private void Variables(IList<Bpl.Variable> variables, int depth) {
      Require(variables != null && variables.Count <= Ir.Protocol.MaximumNodes, "Guard variable list exceeds its bound", token);
      Val(variables.Count); foreach (var variable in variables) { Variable(variable, depth); }
    }
    private void Attributes(Bpl.QKeyValue attributes, int depth) {
      var count = 0;
      for (var current = attributes; current != null; current = current.Next) {
        Enter(depth); Require(++count <= Ir.Protocol.MaximumNodes, "Guard attributes exceed their bound", token);
        Ref(current); Val(current.Key); Ref(current.Next); Val(current.Params.Count);
        Require(current.Params.Count <= Ir.Protocol.MaximumNodes, "Guard attribute parameters exceed their bound", token);
        foreach (var parameter in current.Params) {
          if (parameter is Bpl.Expr expression) { Val(true); Expression(expression, depth + 1); }
          else if (parameter is string text) { Val(false); Val(text); }
          else { throw new B3UnsignedWrappers.Rejection("Unsupported guard attribute parameter", token); }
        }
      }
      Ref(null);
    }
    private void Function(Bpl.Function function, int depth) {
      Enter(depth); Ref(function); Types(function.TypeParameters, depth + 1);
      Variables(function.InParams, depth + 1); Variables(function.OutParams, depth + 1);
      Attributes(function.Attributes, depth + 1);
      // This only detects mutations of referenced Bodies. Expression source-root
      // indexing remains unchanged and cannot reach a wrapper through a Body.
      Expression(function.Body, depth + 1); Expression(function.DefinitionBody, depth + 1); Ref(function.DefinitionAxiom);
    }
    private void Appliable(Bpl.IAppliable fun, int depth) {
      Enter(depth); Ref(fun); var constructor = fun.GetType(); Ref(constructor);
      Require(constructor == typeof(Bpl.UnaryOperator) || constructor == typeof(Bpl.BinaryOperator) ||
        constructor == typeof(Bpl.FunctionCall) || constructor == typeof(Bpl.TypeCoercion) ||
        constructor == typeof(Bpl.ArithmeticCoercion) || constructor == typeof(Bpl.MapSelect) ||
        constructor == typeof(Bpl.MapStore) || constructor == typeof(Bpl.IfThenElse),
        "Unsupported exact operator constructor in guard field view", token);
      switch (fun) {
        case Bpl.UnaryOperator unary: Val(unary.Op); break;
        case Bpl.BinaryOperator binary: Val(binary.Op); break;
        case Bpl.FunctionCall call: Function(call.Func, depth + 1); break;
        case Bpl.TypeCoercion coercion: Type(coercion.Type, depth + 1); break;
        case Bpl.ArithmeticCoercion coercion: Val(coercion.Coercion); break;
        case Bpl.MapSelect select: Val(select.Arity); break;
        case Bpl.MapStore store: Val(store.Arity); break;
        case Bpl.IfThenElse: break;
        default: throw new B3UnsignedWrappers.Rejection("Unsupported operator constructor in guard field view", token);
      }
    }
    private void Expressions(IList<Bpl.Expr> expressions, int depth) {
      Require(expressions != null && expressions.Count <= Ir.Protocol.MaximumNodes, "Guard expression list exceeds its bound", token);
      Val(expressions.Count); foreach (var expression in expressions) { Expression(expression, depth); }
    }
    internal void Expression(Bpl.Expr expression, int depth) {
      Enter(depth); Ref(expression);
      if (expression == null) { return; }
      var constructor = expression.GetType(); Ref(constructor);
      Require(constructor == typeof(Bpl.LiteralExpr) || constructor == typeof(Bpl.IdentifierExpr) ||
        constructor == typeof(Bpl.NAryExpr) || constructor == typeof(Bpl.OldExpr) ||
        constructor == typeof(Bpl.BvExtractExpr) || constructor == typeof(Bpl.BvConcatExpr) ||
        constructor == typeof(Bpl.ForallExpr) || constructor == typeof(Bpl.ExistsExpr) ||
        constructor == typeof(Bpl.LambdaExpr) || constructor == typeof(Bpl.LetExpr),
        "Unsupported exact expression constructor in guard field view", token);
      Type(expression.Type, depth + 1);
      switch (expression) {
        case Bpl.LiteralExpr literal:
          // Val and BvConst.Value/Bits are readonly. Their existing boxed value
          // identity suffices; no unbounded numeric rendering is allocated.
          Ref(literal.Val); break;
        case Bpl.IdentifierExpr identifier:
          Variable(identifier.Decl, depth + 1); break;
        case Bpl.NAryExpr application:
          Appliable(application.Fun, depth + 1); Ref(application.TypeParameters);
          if (application.TypeParameters != null) {
            var parameters = application.TypeParameters.FormalTypeParams;
            Types(parameters, depth + 1);
            foreach (var parameter in parameters) { Type(application.TypeParameters[parameter], depth + 1); }
          }
          Expressions(application.Args, depth + 1); break;
        case Bpl.OldExpr old: Expression(old.Expr, depth + 1); break;
        case Bpl.BvExtractExpr extract:
          Val(extract.Start); Val(extract.End); Expression(extract.Bitvector, depth + 1); break;
        case Bpl.BvConcatExpr concat:
          Expression(concat.E0, depth + 1); Expression(concat.E1, depth + 1); break;
        case Bpl.BinderExpr binder:
          Types(binder.TypeParameters, depth + 1); Variables(binder.Dummies, depth + 1);
          Attributes(binder.Attributes, depth + 1);
          if (binder is Bpl.QuantifierExpr quantifier) {
            var count = 0;
            for (var trigger = quantifier.Triggers; trigger != null; trigger = trigger.Next) {
              Enter(depth + 1); Require(++count <= Ir.Protocol.MaximumNodes, "Guard triggers exceed their bound", token);
              Ref(trigger); Val(trigger.Pos); Ref(trigger.Next); Expressions(trigger.Tr, depth + 2);
            }
            Ref(null);
          }
          Expression(binder.Body, depth + 1); break;
        case Bpl.LetExpr let:
          Variables(let.Dummies, depth + 1); Expressions(let.Rhss, depth + 1);
          Attributes(let.Attributes, depth + 1); Expression(let.Body, depth + 1); break;
        default: throw new B3UnsignedWrappers.Rejection("Unsupported expression constructor in guard field view", token);
      }
    }
  }
  private static void Require(bool condition, string message, Bpl.IToken token) {
    if (!condition) { throw new B3UnsignedWrappers.Rejection(message, token); }
  }
}
