namespace Microsoft.Dafny;

/// <summary>
/// Structural fuel policy for one proposition occurrence. The expression translator
/// carries the heaps, layers, receiver, SCC limit, frames and visibility inputs.
/// Siblings inherit the same policy; consuming it only affects descendants.
/// </summary>
public record VerificationExpressionContext(
  VerificationExpressionUse Use, bool Positive = true, bool MayAdjustFuel = true) {
  public VerificationExpressionContext Negated() => this with { Positive = !Positive };
  public VerificationExpressionContext FuelSelected() => this with { MayAdjustFuel = false };
}

public enum VerificationExpressionUse { Value, Check, Summary }
