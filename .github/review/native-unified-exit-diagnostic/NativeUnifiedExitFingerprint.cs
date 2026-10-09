using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Boogie;
using Bpl = Microsoft.Boogie;
namespace Microsoft.Dafny;
internal static class NativeUnifiedExitFingerprint {
  public static string Expression(Bpl.Expr expression) => Term(expression, new BindingScope());

  // Resolved occurrences are bound by declaration identity; unresolved ones use
  // the innermost lexical name. Depth counts declarations, never distinct names.
  public sealed class BindingScope {
    private readonly Dictionary<Bpl.Variable, string> identities;
    private readonly Dictionary<string, string> names;
    public int Depth { get; private set; }
    public BindingScope() {
      identities = new(); names = new();
    }
    public BindingScope(BindingScope parent) {
      identities = new(parent.identities); names = new(parent.names);
      Depth = parent.Depth;
    }
    public void Bind(Bpl.Variable variable, string canonical) {
      identities[variable] = canonical; names[variable.Name] = canonical; Depth++;
    }
    public string Identifier(Bpl.IdentifierExpr identifier) => identifier.Decl != null
      ? identities.GetValueOrDefault(identifier.Decl, identifier.Name)
      : names.GetValueOrDefault(identifier.Name, identifier.Name);
  }

  private static string Term(Bpl.Expr e, BindingScope bound) {
    var type = e.Type?.ToString() ?? "unresolved";
    string Child(Bpl.Expr x) => Term(x, bound);
    switch (e) {
      case Bpl.IdentifierExpr id:
        return $"id:{type}:{bound.Identifier(id)}";
      case Bpl.LiteralExpr literal:
        return $"literal:{type}:{literal}";
      case Bpl.OldExpr old:
        return $"old:{type}({Child(old.Expr)})";
      case Bpl.NAryExpr application: {
        // Boogie's operator ToString() can return only the CLR class name.
        // Keep opcode identity rather than conflating, for example, => and &&.
        var operation = application.Fun switch {
          Bpl.BinaryOperator binary => $"binary:{binary.Op}",
          Bpl.UnaryOperator unary => $"unary:{unary.Op}",
          Bpl.FunctionCall function => $"function:{function}",
          Bpl.TypeCoercion coercion => $"coercion:{coercion.Type}",
          _ => $"{application.Fun.GetType().FullName}:{application}"
        };
        return $"apply:{type}:{operation}({string.Join(",", application.Args.Select(Child))})";
      }
      case Bpl.QuantifierExpr quantifier: {
        var scoped = new BindingScope(bound);
        var variables = quantifier.Dummies.Select((v,i) => {
          var name = $"bound{bound.Depth+i}";
          scoped.Bind(v, name);
          return $"{name}:{v.TypedIdent.Type}";
        }).ToArray();
        var patterns = new List<string>();
        for (var t = quantifier.Triggers; t != null; t = t.Next) {
          patterns.Add($"{t.Pos}:[{string.Join(",", t.Tr.Select(x => Term(x,scoped)))}]");
        }
        return $"{e.GetType().Name}:{type}:types[{string.Join(",",quantifier.TypeParameters)}]" +
          $"[{string.Join(",",variables)}]:attributes[{Attributes(quantifier.Attributes,scoped)}]" +
          $"patterns[{string.Join(";",patterns)}]({Term(quantifier.Body,scoped)})";
      }
      case Bpl.LambdaExpr lambda: {
        var scoped = new BindingScope(bound);
        var variables = lambda.Dummies.Select((v,i) => {
          var name = $"bound{bound.Depth+i}";
          scoped.Bind(v, name);
          return $"{name}:{v.TypedIdent.Type}";
        }).ToArray();
        return $"lambda:{type}:types[{string.Join(",",lambda.TypeParameters)}]" +
          $"[{string.Join(",",variables)}]:attributes[{Attributes(lambda.Attributes,scoped)}]" +
          $"({Term(lambda.Body,scoped)})";
      }
      case Bpl.LetExpr let: {
        var scoped = new BindingScope(bound);
        var variables = let.Dummies.Select((v,i) => {
          var name=$"bound{bound.Depth+i}";scoped.Bind(v, name);return $"{name}:{v.TypedIdent.Type}";
        }).ToArray();
        var bindings=string.Join(",",variables);
        return $"let:{type}[{bindings}]=[{string.Join(",",let.Rhss.Select(Child))}]" +
          $"({Term(let.Body,scoped)})";
      }
      default:
        // Keep unsupported encodings verbatim; failing equality is safer than
        // inventing a bridge certificate or conflating different witnesses.
        return $"{e.GetType().Name}:{type}:{e}";
    }
  }

  public static string Attributes(QKeyValue? attribute, BindingScope bound) {
    var result=new List<string>();
    for (var a=attribute;a!=null;a=a.Next) {
      result.Add($"{a.Key}:[{string.Join(",",a.Params.Select(p => p is Bpl.Expr e ? Term(e,bound) : p.ToString()))}]");
    }
    return string.Join(";",result);
  }
}
