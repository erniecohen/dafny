#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// The type "bounded&lt;O, F&gt;" of a bounded callable (issue 266, milestone 1): a function value F that carries
/// persistent evidence that every call of it terminates below a ghost bound of the order O.  The type exists only
/// under --modular-termination, as an abstract type of the system module:
///   - O is the order: nat or ORDINAL, each with its built-in order, or an order type "type {:order O} L = ..." (issue
///     211), whose values are compared by O.  O may also be a total arrow "A1, ..., An -> R", with R one of those and
///     A1, ..., An the parameter types of F: the bound then depends on the arguments of each call (an
///     argument-dependent bound, as in VeriFast's function-pointer contracts).  O is non-variant.
///   - F is the payload, an arrow type of any flavor (~>, --> or ->).  F is strictly covariant, so a bounded type
///     has exactly the subtyping of its arrow type.
///   - The type has no equality, no default value and no nonemptiness: it has no values that the program did not
///     form (milestone 2), and a value is not determined by its payload.
///   - The compilers erase it to F, with no wrapper.
/// Milestone 2 adds the members of the type:
///   - "bounded.certify(a, f)" forms a value with bound a from f: a lambda expression, whose body, precondition and
///     reads clause are checked to run under a; the function value of a bounded callable c, with c.bound &lt;= a; or a
///     function that reaches no dispatched call.  It is a static special function whose parameter f has the callback
///     bound a (milestone M3b), so the checks of a callback argument apply.
///   - "c.bound" is the ghost bound of c, fixed when c is formed.  For an argument-dependent bound it is a ghost
///     function, closed (rule 8): a lambda expression or a function that reaches no dispatch or unknown application.
///     "bounded.certifyAt(b, f)" is another name of "bounded.certify", for such a bound b, and "c.boundAt(args)" is
///     "c.bound(args)".  A lambda f is checked to run under b at its own arguments, and an application "c(args)" to
///     run under "c.bound(args)".  "certify(a, f)" is "certifyAt(x => a, f)" with the bound kept as a value.
///   - "c.erase" is the function value of c.  Applying it, or calling its "requires" or "reads", needs
///     c.bound to be below the caller's bound; passing it for a parameter with a callback bound q needs c.bound &lt;= q.
/// </summary>
public static class BoundedCallables {
  public const string TypeName = "bounded";

  public static AbstractTypeDecl CreateDecl(ModuleDefinition systemModule, Attributes dontCompile) {
    var order = new TypeParameter(SourceOrigin.NoToken, new Name("O"), TPVarianceSyntax.NonVariant_Strict);
    var payload = new TypeParameter(SourceOrigin.NoToken, new Name("F"), TPVarianceSyntax.Covariant_Strict);
    var characteristics = new TypeParameterCharacteristics(TypeParameter.EqualitySupportValue.Unspecified,
      Type.AutoInitInfo.MaybeEmpty, false);
    var decl = new AbstractTypeDecl(SourceOrigin.NoToken, new Name(TypeName), systemModule, characteristics,
      [order, payload], [], [], dontCompile, false);
    var orderType = new UserDefinedType(order);
    var payloadType = new UserDefinedType(payload);
    var bound = new SpecialField(SourceOrigin.NoToken, BoundName, SpecialField.ID.BoundedBound, null, true, false, false, orderType, null);
    var erase = new SpecialField(SourceOrigin.NoToken, EraseName, SpecialField.ID.BoundedErase, null, false, false, false, payloadType, null);
    var resultType = new UserDefinedType(SourceOrigin.NoToken, TypeName, decl, [orderType, payloadType]);
    // "certify" and its other name "certifyAt", each with formals of its own
    SpecialFunction Certify(string name) {
      var a = new Formal(Token.NoToken, "a", orderType, true, true, null);
      var f = new Formal(Token.NoToken, "f", payloadType, true, false, null);
      var aExpr = new IdentifierExpr(Token.NoToken, a.Name) { Var = a, Type = a.Type };
      var fExpr = new IdentifierExpr(Token.NoToken, f.Name) { Var = f, Type = f.Type };
      var callback = new Attributes(ModularTermination.CallbackAttributeName, [fExpr, aExpr], null);
      return new SpecialFunction(SourceOrigin.NoToken, name, systemModule, true, false,
        [], [a, f], resultType,
        [], new Specification<FrameExpression>([], null), [],
        new Specification<Expression>([], callback), null, null, null);
    }
    var certify = Certify(CertifyName);
    var certifyAt = Certify(CertifyAtName);
    foreach (var member in new MemberDecl[] { bound, erase, certify, certifyAt }) {
      member.EnclosingClass = decl;
      member.AddVisibilityScope(systemModule.VisibilityScope, false);
      decl.Members.Add(member);
    }
    return decl;
  }

  public const string BoundName = "bound";
  public const string EraseName = "erase";
  public const string CertifyName = "certify";
  public const string CertifyAtName = "certifyAt";
  public const string BoundAtName = "boundAt";

  /// <summary>
  /// Whether "function" is "bounded.certify".
  /// </summary>
  public static bool IsCertify(MemberDecl? function) {
    return function is SpecialFunction { Name: CertifyName or CertifyAtName } && IsBoundedDecl(function.EnclosingClass);
  }

  /// <summary>
  /// If "value" is "c.erase" for a bounded callable c, then c; otherwise null.
  /// </summary>
  public static Expression? ErasedCallable(Expression value) {
    return value.Resolved is MemberSelectExpr { Member: SpecialField { SpecialId: SpecialField.ID.BoundedErase } } select
      ? select.Obj : null;
  }

  /// <summary>
  /// Whether "value" is "X.bound" for a bounded value X with an argument-dependent bound: a ghost function that was
  /// checked to be closed when X was formed, so applying it runs no dispatch and no unknown code.
  /// </summary>
  public static bool IsArgumentBound(Expression value) {
    return value.Resolved is MemberSelectExpr { Member: SpecialField { SpecialId: SpecialField.ID.BoundedBound } } select &&
           ArgumentOrderResult(select.Type) != null;
  }

  /// <summary>
  /// Whether "decl" is the system module's declaration of "bounded".
  /// </summary>
  public static bool IsBoundedDecl(TopLevelDecl? decl) {
    return decl is AbstractTypeDecl { Name: TypeName, EnclosingModuleDefinition.Name: "_System" };
  }

  /// <summary>
  /// The type arguments of the (normalized) type "type" that may not be ORDINAL: all of them, except the order of a
  /// bounded type.  The order is ghost, and the value compiles to its payload, so an ORDINAL order is never compiled.
  /// </summary>
  public static IEnumerable<Type> OrdinalCheckedTypeArgs(Type type) {
    return type is UserDefinedType { ResolvedClass: var decl } && IsBoundedDecl(decl) ? type.TypeArgs.Skip(1) : type.TypeArgs;
  }

  /// <summary>
  /// Like OrdinalCheckedTypeArgs, for a pre-type.
  /// </summary>
  public static IEnumerable<PreType> OrdinalCheckedArguments(DPreType preType) {
    return IsBoundedDecl(preType.Decl) ? preType.Arguments.Skip(1) : preType.Arguments;
  }

  /// <summary>
  /// If "type" is a bounded type (after expanding synonyms), its payload type F; otherwise null.
  /// </summary>
  public static Type? Payload(Type type) {
    return type.NormalizeExpand() is UserDefinedType { ResolvedClass: var decl, TypeArgs: { Count: 2 } args } &&
           IsBoundedDecl(decl) ? args[1] : null;
  }

  /// <summary>
  /// The member "name" ("bound" or "erase") of "bounded", if the type "type" of "bounded" is a bounded type; null
  /// otherwise.  This is how "c(args)", "c.requires(args)" and "c.reads(args)" are read as "c.erase(args)", and so on.
  /// </summary>
  public static MemberSelectExpr? Select(Expression bounded, Type type, string name) {
    if (type.NormalizeExpand() is not UserDefinedType { ResolvedClass: TopLevelDeclWithMembers decl, TypeArgs: { Count: 2 } args } ||
        !IsBoundedDecl(decl)) {
      return null;
    }
    var field = decl.Members.OfType<SpecialField>().Single(f => f.Name == name);
    return new MemberSelectExpr(bounded.Origin, bounded, field.NameNode) {
      Member = field,
      TypeApplicationAtEnclosingClass = args,
      TypeApplicationJustMember = [],
      Type = name == BoundName ? args[0] : args[1]
    };
  }

  /// <summary>
  /// Like Select, for the pre-type resolver: "preType" is the pre-type of "bounded".
  /// </summary>
  public static MemberSelectExpr? SelectByPreType(Expression bounded, PreType preType, string name) {
    if (preType.Normalize() is not DPreType { Decl: TopLevelDeclWithMembers decl, Arguments: { Count: 2 } args } || !IsBoundedDecl(decl)) {
      return null;
    }
    var field = decl.Members.OfType<SpecialField>().Single(f => f.Name == name);
    return new MemberSelectExpr(bounded.Origin, bounded, field.NameNode) {
      Member = field,
      PreTypeApplicationAtEnclosingClass = args,
      PreTypeApplicationJustMember = [],
      PreType = name == BoundName ? args[0] : args[1]
    };
  }

  /// <summary>
  /// The legacy resolver infers "int" for the order of "bounded.certify(5, f)", since the literal has that type; but
  /// nat is the only int-based order that a bounded type admits.  So an inferred order of "int" becomes "nat", for
  /// every type that shares the inferred proxy.  A written order is left alone (and "int" is rejected).
  /// </summary>
  public static void DefaultOrderToNat(Type order, SystemModuleManager systemModuleManager) {
    TypeProxy? last = null;
    for (var t = order; t is TypeProxy { T: { } next } proxy; t = next) {
      last = proxy;
    }
    if (last != null && last.T.NormalizeExpand() is IntType) {
      last.T = systemModuleManager.Nat();
    }
  }

  /// <summary>
  /// Whether "order" names an order that a bounded type may use: nat or ORDINAL, with its built-in order, or an
  /// order type "type {:order O} L = ...", whose bounds are compared by O (issue 211).  The order is part of the
  /// type, and O is non-variant, so a value certified under one order is never compared under another.
  /// </summary>
  public static bool IsAdmittedOrder(Type order) {
    if (ArgumentOrderResult(order) is { } result) {
      return result.NormalizeExpand() is not ArrowType && IsAdmittedOrder(result);
    }
    var t = order.NormalizeExpandKeepConstraints();
    return t is BigOrdinalType || t is UserDefinedType { ResolvedClass: SubsetTypeDecl { Name: "nat", EnclosingModuleDefinition.Name: "_System" } } ||
           DecreasesOrders.TypeOrder(order) != null;
  }

  /// <summary>
  /// If "order" is the order of an argument-dependent bound, a total arrow "A1, ..., An -> R", then R; otherwise null.
  /// </summary>
  public static Type? ArgumentOrderResult(Type order) {
    return order.IsArrowTypeWithoutPreconditions ? order.AsArrowType.Result : null;
  }

  /// <summary>
  /// The bound of the bounded value "bounded" for a call with the arguments "args": "bounded.bound", or for an
  /// argument-dependent bound "bounded.bound(args)".  Its order is that of its type.
  /// </summary>
  public static Expression BoundAt(Expression bounded, List<Expression>? args) {
    var bound = Select(bounded, bounded.Type, BoundName)!;
    if (ArgumentOrderResult(bound.Type) is not { } result) {
      return DecreasesOrders.WithTypeOrder(bound, bound.Type);
    }
    var apply = new ApplyExpr(bounded.Origin, bound, args ?? [], Token.NoToken) { Type = result };
    return DecreasesOrders.WithTypeOrder(apply, result);
  }

  private static bool SameParameters(List<Type> xs, List<Type> ys) {
    return xs.Count == ys.Count && xs.Zip(ys).All(p => p.First.Equals(p.Second, true));
  }

  /// <summary>
  /// Report an error at "tok" if the bounded type "udt" has an order other than nat, ORDINAL or an order type, or a payload that is
  /// not an arrow type.  A type parameter is neither.
  /// </summary>
  public static void CheckInstantiation(ErrorReporter reporter, IOrigin tok, UserDefinedType udt, ISet<IOrigin> reported) {
    if (udt.TypeArgs.Count != 2 || !reported.Add(udt.Origin.Uri == null ? tok : udt.Origin)) {
      return;
    }
    var (order, payload) = (udt.TypeArgs[0], udt.TypeArgs[1]);
    if (order.Normalize() is not TypeProxy && !IsAdmittedOrder(order)) {
      reporter.Error(MessageSource.Resolver, tok,
        $"the order of a bounded type must be nat, ORDINAL, an order type (a subset type declared with {{:order O}}), or a total arrow to one of these (got {order})");
    } else if (order.Normalize() is not TypeProxy && payload.Normalize() is not TypeProxy && ArgumentOrderResult(order) != null &&
               payload.NormalizeExpand().AsArrowType is { } payloadArrow &&
               !SameParameters(order.AsArrowType.Args, payloadArrow.Args)) {
      reporter.Error(MessageSource.Resolver, tok,
        $"an argument-dependent order of a bounded type must take the parameters of its payload (got {order} for payload {payload})");
    }
    if (payload.Normalize() is not TypeProxy && !payload.NormalizeExpand().IsArrowType) {
      reporter.Error(MessageSource.Resolver, tok,
        $"the payload of a bounded type must be an arrow type (got {payload})");
    }
  }
}
