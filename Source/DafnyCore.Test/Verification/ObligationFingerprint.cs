using Microsoft.Boogie;
using Microsoft.Dafny;
using Bpl = Microsoft.Boogie;

namespace DafnyCore.Test.Verification;

// Strict typed structural comparison. Only lexical binder alpha-renaming is
// normalized. Ground identifiers, witnesses, Lit/boxing, layers and guards remain.
internal static class ObligationFingerprint {
  public static string Emit(IEnumerable<Bpl.Program> programs) {
    var result = new StringWriter();
    foreach (var program in programs) {
      using var writer = new TokenTextWriter(result, DafnyOptions.Default);
      program.Emit(writer);
    }
    return result.ToString();
  }

  public static string Content(BoogieGenerator.PropositionLowering package) =>
    string.Join("\n", package.Pieces.Select(p => $"{p.Kind}:{Expression(p.E)}")) +
    "\nsummary:" + Expression(package.Summary);

  public static string Expression(Bpl.Expr expression) => Term(expression, new BindingScope());

  // Only recorded fresh preparation arguments may be renamed by this overload.
  // Source variables, functions, heaps, triggers and fuel remain literal.
  public static string PreparationExpression(Bpl.Expr expression, IReadOnlyDictionary<string, string> arguments) =>
    Term(expression, new BindingScope(arguments));

  // Resolved occurrences are bound by declaration identity; unresolved ones use
  // the innermost lexical name. Depth counts declarations, never distinct names.
  private sealed class BindingScope {
    private readonly Dictionary<Bpl.Variable, string> identities;
    private readonly Dictionary<string, string> names;
    private readonly IReadOnlyDictionary<string, string> preparationArguments;
    public int Depth { get; private set; }
    public BindingScope(IReadOnlyDictionary<string, string>? arguments = null) {
      identities = new(); names = new();
      preparationArguments = arguments ?? new Dictionary<string, string>();
    }
    public BindingScope(BindingScope parent) {
      identities = new(parent.identities); names = new(parent.names);
      Depth = parent.Depth;
      preparationArguments = parent.preparationArguments;
    }
    public void Bind(Bpl.Variable variable, string canonical) {
      identities[variable] = canonical; names[variable.Name] = canonical; Depth++;
    }
    public string Identifier(Bpl.IdentifierExpr identifier) {
      var name = identifier.Decl != null
        ? identities.GetValueOrDefault(identifier.Decl, identifier.Name)
        : names.GetValueOrDefault(identifier.Name, identifier.Name);
      return preparationArguments.GetValueOrDefault(name, name);
    }
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

  private static string Attributes(QKeyValue? attribute, BindingScope bound) {
    var result=new List<string>();
    for (var a=attribute;a!=null;a=a.Next) {
      result.Add($"{a.Key}:[{string.Join(",",a.Params.Select(p => p is Bpl.Expr e ? Term(e,bound) : p.ToString()))}]");
    }
    return string.Join(";",result);
  }
}
