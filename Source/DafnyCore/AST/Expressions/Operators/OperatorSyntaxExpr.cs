using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>Source notation only. Semantic visitors see exactly the ordinary resolved child.</summary>
public class OperatorSyntaxExpr : ConcreteSyntaxExpression, ICloneable<OperatorSyntaxExpr>, ICanFormat {
  public Expression Syntax { get; }
  public OperatorSelection Selection { get; set; }
  public bool ResolutionStarted { get; set; }
  public bool ReplayRequired { get; private set; }
  public ImmutableList<OperatorCloneContext> CloneContexts { get; private set; } = ImmutableList<OperatorCloneContext>.Empty;
  public IEnumerable<Expression> Operands => Syntax is BinaryExpr b ? new[] { b.E0, b.E1 } : new[] { ((NegationExpression)Syntax).E };
  public string Symbol => Syntax is BinaryExpr b ? BinaryExpr.OpcodeString(b.Op) : "-";

  public OperatorSyntaxExpr(IOrigin origin, Expression syntax) : base(origin) {
    Syntax = syntax;
  }

  public OperatorSyntaxExpr(Cloner cloner, OperatorSyntaxExpr original) : base(cloner, original) {
    if ((original.ResolutionStarted || original.ResolvedExpression != null) && original.Selection == null) {
      throw new System.InvalidOperationException("resolved operator selection is missing; lookup cannot be restarted");
    }
    cloner.OperatorClones.HasSelections = true;
    // The root is printable source notation, not a semantic opcode. Rebuild it
    // directly so specialized semantic cloners never inspect its unresolved opcode.
    Syntax = original.Syntax is BinaryExpr binary
      ? new BinaryExpr(cloner.Origin(binary.Origin), binary.Op, cloner.CloneExpr(binary.E0), cloner.CloneExpr(binary.E1))
      : new NegationExpression(cloner.Origin(original.Syntax.Origin), cloner.CloneExpr(((NegationExpression)original.Syntax).E));
    Selection = original.Selection;
    ResolutionStarted = original.ResolutionStarted;
    ReplayRequired = original.ReplayRequired || original.ResolutionStarted || original.ResolvedExpression != null;
    CloneContexts = original.CloneContexts.Add(cloner.OperatorClones);
  }

  public OperatorSyntaxExpr Clone(Cloner cloner) => new(cloner, this);
  public override IEnumerable<Expression> PreResolveSubExpressions => Operands;
  public override IEnumerable<Expression> SubExpressions => ResolvedExpression == null ? Operands : base.SubExpressions;

  public OperatorSelection ReplaySelection() {
    if (ReplayRequired && Selection == null) {
      throw new System.InvalidOperationException("resolved operator selection is missing; lookup cannot be restarted");
    }
    var result = Selection;
    if (result != null) {
      foreach (var context in CloneContexts) {
        result = result.Transport(context);
      }
    }
    return result;
  }

  public void FreezeSelection(OperatorSelection selection) {
    Selection = selection;
    ReplayRequired = false;
    CloneContexts = ImmutableList<OperatorCloneContext>.Empty;
  }

  public static Expression Binary(bool enabled, IOrigin origin, BinaryExpr.Opcode op, Expression left, Expression right) {
    var intrinsic = new BinaryExpr(origin, op, left, right);
    return enabled && op is BinaryExpr.Opcode.Add or BinaryExpr.Opcode.Sub or BinaryExpr.Opcode.Mul or
      BinaryExpr.Opcode.Div or BinaryExpr.Opcode.Mod or BinaryExpr.Opcode.Lt or BinaryExpr.Opcode.Le or
      BinaryExpr.Opcode.Gt or BinaryExpr.Opcode.Ge ? new OperatorSyntaxExpr(origin, intrinsic) : intrinsic;
  }

  public bool SetIndent(int indentBefore, TokenNewIndentCollector formatter) =>
    Syntax is not ICanFormat formattable || formattable.SetIndent(indentBefore, formatter);
}
