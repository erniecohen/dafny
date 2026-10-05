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

  public static string Expression(Bpl.Expr expression) => Term(expression, new Dictionary<string,string>());

  private static string Term(Bpl.Expr e, Dictionary<string,string> bound) {
    var type = e.Type?.ToString() ?? "unresolved";
    string Child(Bpl.Expr x) => Term(x, bound);
    switch (e) {
      case Bpl.IdentifierExpr id:
        return $"id:{type}:{bound.GetValueOrDefault(id.Name, id.Name)}";
      case Bpl.LiteralExpr literal:
        return $"literal:{type}:{literal}";
      case Bpl.OldExpr old:
        return $"old:{type}({Child(old.Expr)})";
      case Bpl.NAryExpr application:
        return $"apply:{type}:{application.Fun}({string.Join(",", application.Args.Select(Child))})";
      case Bpl.QuantifierExpr quantifier: {
        var scoped = new Dictionary<string,string>(bound);
        var variables = quantifier.Dummies.Select((v,i) => {
          var name = $"bound{bound.Count+i}";
          scoped[v.Name] = name;
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
      case Bpl.LetExpr let: {
        var scoped = new Dictionary<string,string>(bound);
        var variables = let.Dummies.Select((v,i) => {
          var name=$"bound{bound.Count+i}";scoped[v.Name]=name;return $"{name}:{v.TypedIdent.Type}";
        });
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

  private static string Attributes(QKeyValue? attribute, Dictionary<string,string> bound) {
    var result=new List<string>();
    for (var a=attribute;a!=null;a=a.Next) {
      result.Add($"{a.Key}:[{string.Join(",",a.Params.Select(p => p is Bpl.Expr e ? Term(e,bound) : p.ToString()))}]");
    }
    return string.Join(";",result);
  }
}
