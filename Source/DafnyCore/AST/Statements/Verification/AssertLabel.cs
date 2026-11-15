using System.Diagnostics.Contracts;

namespace Microsoft.Dafny;

public class AssertLabel : Label {

  [FilledInDuringTranslation] private Boogie.Expr expression;

  public Boogie.Expr E {
    get => ModuleNamingScope.Current is { } scope
      ? scope.FindValue<Boogie.Expr>(this, "assert-label") : expression;
    set {
      if (ModuleNamingScope.Current is { } scope) {
        scope.SetValue(this, "assert-label", value);
      } else {
        expression = value;
      }
    }
  }

  public AssertLabel(IOrigin tok, string name)
    : base(tok, name) {
    Contract.Requires(tok != null);
    Contract.Requires(name != null);
  }
}
