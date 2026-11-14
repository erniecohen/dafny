#nullable enable
using System.Collections.Generic;

namespace Microsoft.Dafny;

/// <summary>
/// The type "bounded&lt;O, F&gt;" of a bounded callable (issue 266, milestone 1): a function value F that carries
/// persistent evidence that every call of it terminates below a ghost bound of the order O.  The type exists only
/// under --modular-termination, as an abstract type of the system module:
///   - O is the order: nat or ORDINAL, each with its built-in order.  O is non-variant.
///   - F is the payload, an arrow type of any flavor (~>, --> or ->).  F is strictly covariant, so a bounded type
///     has exactly the subtyping of its arrow type.
///   - The type has no equality, no default value and no nonemptiness: it has no values that the program did not
///     form (milestone 2), and a value is not determined by its payload.
///   - The compilers erase it to F, with no wrapper.
/// Milestone 1 has no way to form a value, apply it, or project its bound or payload; such uses do not resolve.
/// </summary>
public static class BoundedCallables {
  public const string TypeName = "bounded";

  public static AbstractTypeDecl CreateDecl(ModuleDefinition systemModule, Attributes dontCompile) {
    var order = new TypeParameter(SourceOrigin.NoToken, new Name("O"), TPVarianceSyntax.NonVariant_Strict);
    var payload = new TypeParameter(SourceOrigin.NoToken, new Name("F"), TPVarianceSyntax.Covariant_Strict);
    var characteristics = new TypeParameterCharacteristics(TypeParameter.EqualitySupportValue.Unspecified,
      Type.AutoInitInfo.MaybeEmpty, false);
    return new AbstractTypeDecl(SourceOrigin.NoToken, new Name(TypeName), systemModule, characteristics,
      [order, payload], [], [], dontCompile, false);
  }

  /// <summary>
  /// Whether "decl" is the system module's declaration of "bounded".
  /// </summary>
  public static bool IsBoundedDecl(TopLevelDecl? decl) {
    return decl is AbstractTypeDecl { Name: TypeName, EnclosingModuleDefinition.Name: "_System" };
  }

  /// <summary>
  /// If "type" is a bounded type (after expanding synonyms), its payload type F; otherwise null.
  /// </summary>
  public static Type? Payload(Type type) {
    return type.NormalizeExpandKeepConstraints() is UserDefinedType { ResolvedClass: var decl, TypeArgs: { Count: 2 } args } &&
           IsBoundedDecl(decl) ? args[1] : null;
  }

  /// <summary>
  /// Whether "order" names an order that a bounded type may use: exactly nat or ORDINAL.
  /// </summary>
  public static bool IsAdmittedOrder(Type order) {
    var t = order.NormalizeExpandKeepConstraints();
    return t is BigOrdinalType || t is UserDefinedType { ResolvedClass: SubsetTypeDecl { Name: "nat", EnclosingModuleDefinition.Name: "_System" } };
  }

  /// <summary>
  /// Report an error at "tok" if the bounded type "udt" has an order other than nat or ORDINAL, or a payload that is
  /// not an arrow type.  A type parameter is neither.
  /// </summary>
  public static void CheckInstantiation(ErrorReporter reporter, IOrigin tok, UserDefinedType udt, ISet<IOrigin> reported) {
    if (udt.TypeArgs.Count != 2 || !reported.Add(udt.Origin)) {
      return;
    }
    var (order, payload) = (udt.TypeArgs[0], udt.TypeArgs[1]);
    if (order.Normalize() is not TypeProxy && !IsAdmittedOrder(order)) {
      reporter.Error(MessageSource.Resolver, tok,
        $"the order of a bounded type must be nat or ORDINAL (got {order})");
    }
    if (payload.Normalize() is not TypeProxy && !payload.NormalizeExpand().IsArrowType) {
      reporter.Error(MessageSource.Resolver, tok,
        $"the payload of a bounded type must be an arrow type (got {payload})");
    }
  }
}
