#nullable enable

namespace Microsoft.Dafny;

public class ConversionExpr : TypeUnaryExpr, ICloneable<ConversionExpr> {
  public string messagePrefix;

  // Internal elaboration marker; ordinary source conversions keep their explicit target.
  public bool IsBaseOperation;

  public ConversionExpr(Cloner cloner, ConversionExpr original) : base(cloner, original) {
    messagePrefix = original.messagePrefix;
    IsBaseOperation = original.IsBaseOperation;
  }

  [SyntaxConstructor]
  public ConversionExpr(IOrigin origin, Expression e, Type toType, string messagePrefix = "")
    : base(origin, e, toType) {
    this.messagePrefix = messagePrefix;
  }

  public ConversionExpr Clone(Cloner cloner) {
    return new ConversionExpr(cloner, this);
  }
}