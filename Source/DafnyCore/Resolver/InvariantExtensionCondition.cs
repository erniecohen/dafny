#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// Issue 105 (--member-invariants): the extension condition of section 3.3 of
/// docs/dev/issue105-member-invariants-plan.md, in its conservative first form.  The meaning of a type T with
/// invariants is that its values are the raw values that satisfy them.  If the invariants refer to the set of values
/// of T, that sentence is a recursive definition that may have no solution, and then the axioms of T are
/// inconsistent.  The condition keeps every such reference out of the cone of T's invariants.
///
/// The cone is the invariants of T, the types of the components of raw T, and everything they reach: the
/// specifications and bodies of the functions they call or mention, the initializers of the constants they read,
/// and the constraints of the subset types, newtypes and types with invariants whose values they range over, test,
/// convert to or take as arguments.  A type is T-dependent if its set of values is defined with T's: T, raw T, the
/// traits T extends and the types that extend T, 'object' if T is a reference type, and every type built from one
/// of these (as a component of a datatype, a type argument, the base of a subset type or newtype, or a parameter or
/// result of an arrow).  Within the cone:
///  1. a bound variable of a T-dependent type must range over the elements of a collection (or be equal to a given
///     value), rather than over the set of values of its type;
///  2. no generic function is instantiated with a T-dependent type argument (the first-stage approximation of the
///     rule that such an argument may only go to a type parameter the callee does not quantify over);
///  3. no function value is applied other than a lambda expression or a named function, applied on the spot;
///  4. no dynamically bound function is called or mentioned, since its overrides are not all known;
///  5. no raw value is tested against, or converted to, a T-dependent type, explicitly or implicitly.
/// The constraint of a generic type in the cone (a subset type, a newtype, or a type with invariants, whose
/// constraint is its invariants) is analysed with each type parameter T-dependent where its type argument is.
/// Statements inside expressions are proof hints and do not contribute to the meaning, so they are not in the cone.
///
/// The plan notes that, where T may involve references, the existing restriction on function bodies discharges
/// clauses 1 to 4 and accepts dynamically bound calls.  Clause 5 is not discharged that way: an override, or a
/// stored function value, may test a constructor application, which is raw, against T.  So this first form applies
/// all five clauses to every type with invariants.
/// </summary>
public class InvariantExtensionCondition {
  private readonly TopLevelDeclWithMembers owner;
  private readonly ErrorReporter reporter;
  private readonly Dictionary<TopLevelDecl, bool> declDependence = new();
  private readonly HashSet<string> visitedTypeNodes = [];
  private readonly HashSet<(MemberDecl, string)> visitedMembers = [];
  private readonly Queue<Node> worklist = new();

  /// <summary>
  /// A part of the cone: a member (an invariant, a function or a constant), or a type declaration, instantiated with
  /// type arguments of which those at "DependentParameters" are T-dependent.  Only an invariant reached through the
  /// type it constrains has dependent parameters as a member: a generic function given a dependent type argument
  /// is rejected (clause 2).
  /// </summary>
  private record Node(MemberDecl? Member, TopLevelDecl? Type, ISet<TypeParameter> DependentParameters);

  private Node current = new(null, null, new HashSet<TypeParameter>());

  /// <summary>
  /// The places reported so far: an expression in the cones of two types is reported once.
  /// </summary>
  private readonly HashSet<(string?, int)> reported;

  private InvariantExtensionCondition(TopLevelDeclWithMembers owner, ErrorReporter reporter, HashSet<(string?, int)> reported) {
    this.owner = owner;
    this.reporter = reporter;
    this.reported = reported;
  }

  /// <summary>
  /// Check the extension condition for each declaration in "declarations" that has invariants, declared or inherited
  /// from the traits it extends.
  /// </summary>
  public static void Check(IEnumerable<TopLevelDecl> declarations, ErrorReporter reporter) {
    var reported = new HashSet<(string?, int)>();
    foreach (var decl in declarations.OfType<TopLevelDeclWithMembers>()) {
      if (decl.Members.OfType<MemberInvariant>().Any() || decl.InvariantTypeDecl != null) {
        new InvariantExtensionCondition(decl, reporter, reported).Run();
      }
    }
  }

  private void Run() {
    var none = new HashSet<TypeParameter>();
    foreach (var invariant in decl.Members.OfType<MemberInvariant>()) {
      Enqueue(invariant);
    }
    EnqueueInheritedInvariants(decl, none);
    if (owner is DatatypeDecl datatype) {
      // The values of raw T are built from values of the types of its components.
      EnqueueType(datatype, datatype.TypeArgs.ConvertAll(tp => (Type)new UserDefinedType(tp)), none);
    }
    while (worklist.Count != 0) {
      current = worklist.Dequeue();
      if (current.Member is Function function) {
        foreach (var formal in function.Ins) {
          ReachType(formal.Type, formal.Origin);
        }
        ReachType(function.ResultType, function.Result?.Origin ?? function.NameNode.Origin);
        function.Req.ForEach(req => Visit(req.E));
        function.Ens.ForEach(ens => Visit(ens.E));
        if (function.Body != null) {
          CheckSlot(function.Body, function.ResultType);
          Visit(function.Body);
        }
      } else if (current.Member is ConstantField { Rhs: { } rhs } constant) {
        ReachType(constant.Type, constant.NameNode.Origin);
        CheckSlot(rhs, constant.Type);
        Visit(rhs);
      } else {
        switch (current.Type) {
          case InvariantTypeDecl invariantTypeDecl:
            // The constraint of the type is the conjunction of the invariants of its owner, which are analysed as the
            // constraint of a generic subset type is, with the dependence of each type parameter, rather than as
            // calls of generic functions (clause 2).
            ReachType(invariantTypeDecl.Var.Type, invariantTypeDecl.Var.Origin);
            var dependentOwnerParameters = invariantTypeDecl.TypeArgs.Zip(invariantTypeDecl.Owner.TypeArgs)
              .Where(pair => current.DependentParameters.Contains(pair.First)).Select(pair => pair.Second).ToHashSet();
            foreach (var invariant in InvariantTypeDecl.Invariants(invariantTypeDecl.Owner)) {
              Enqueue(invariant, dependentOwnerParameters);
            }
            EnqueueInheritedInvariants(invariantTypeDecl.Owner, dependentOwnerParameters);
            break;
          case SubsetTypeDecl subsetTypeDecl: // including NonNullTypeDecl
            if (IsConstrainedArrowTypeDecl(subsetTypeDecl)) {
              // A built-in arrow type, with its base types, is checked where the program reaches it (see ReachType),
              // since its declaration has no place in the program to report.
              break;
            }
            ReachType(subsetTypeDecl.Var.Type, subsetTypeDecl.Var.Origin);
            Visit(subsetTypeDecl.Constraint);
            break;
          case NewtypeDecl newtypeDecl:
            ReachType(newtypeDecl.BaseType, newtypeDecl.NameNode.Origin);
            if (newtypeDecl.Constraint != null) {
              Visit(newtypeDecl.Constraint);
            }
            break;
          case DatatypeDecl datatypeDecl:
            foreach (var formal in datatypeDecl.Ctors.SelectMany(ctor => ctor.Formals)) {
              ReachType(formal.Type, formal.Origin);
            }
            break;
        }
      }
    }
  }

  private TopLevelDeclWithMembers decl => owner;

  /// <summary>
  /// Put into the cone the invariants that "withInvariants" inherits from the traits it extends, where the type
  /// parameters "dependentParameters" of "withInvariants" are T-dependent. A type parameter of the trait that declares
  /// an inherited invariant is T-dependent where its type argument in the extends clauses is.
  /// </summary>
  private void EnqueueInheritedInvariants(TopLevelDeclWithMembers withInvariants, ISet<TypeParameter> dependentParameters) {
    foreach (var (invariant, receiver) in withInvariants.InvariantTypeDecl?.InheritedInvariants ?? []) {
      var trait = invariant.EnclosingClass;
      var dependent = trait.TypeArgs
        .Where((_, i) => i < receiver.TypeArgs.Count && DependsOnOwner(receiver.TypeArgs[i], dependentParameters))
        .ToHashSet();
      Enqueue(invariant, dependent);
    }
  }

  private void Enqueue(MemberDecl member) => Enqueue(member, new HashSet<TypeParameter>());

  private void Enqueue(MemberDecl member, ISet<TypeParameter> dependentParameters) {
    var key = string.Join(",", member.EnclosingClass.TypeArgs.Select(dependentParameters.Contains));
    if (visitedMembers.Add((member, key))) {
      worklist.Enqueue(new Node(member, null, dependentParameters));
    }
  }

  private void EnqueueType(TopLevelDecl typeDecl, List<Type> typeArgs, ISet<TypeParameter> dependentParameters) {
    var dependent = new HashSet<TypeParameter>();
    for (var i = 0; i < typeDecl.TypeArgs.Count && i < typeArgs.Count; i++) {
      if (DependsOnOwner(typeArgs[i], dependentParameters)) {
        dependent.Add(typeDecl.TypeArgs[i]);
      }
    }
    var key = typeDecl.FullName + "<" + string.Join(",", typeDecl.TypeArgs.Select(dependent.Contains)) + ">" +
              typeDecl.GetHashCode();
    if (visitedTypeNodes.Add(key)) {
      worklist.Enqueue(new Node(null, typeDecl, dependent));
    }
  }

  /// <summary>
  /// Put into the cone the declarations whose constraints decide which values "type" has.  "origin" is where the
  /// cone reaches the type.
  ///
  /// The total (->) and partial (-->) arrow types are subset types of the arrow type (~>), with a constraint that
  /// quantifies over their parameter types: a function value's precondition holds, or its reads clause is empty, at
  /// every value of them.  So an arrow type with a T-dependent parameter type refers to the set of values of that
  /// type, as a bound variable of it would (clause 1).  This is reported at "origin".
  /// </summary>
  private void ReachType(Type type, IOrigin origin) {
    type = type.NormalizeExpandKeepConstraints();
    if (type is UserDefinedType { ResolvedClass: SubsetTypeDecl arrowTypeDecl } && IsConstrainedArrowTypeDecl(arrowTypeDecl) &&
        type.TypeArgs.SkipLast(1).FirstOrDefault(DependsOnOwner) is { } parameterType) {
      Error(origin, $"refers to the set of values of '{parameterType}'{DependsOnOwnerNote(parameterType)}, " +
                    $"in the constraint of the arrow type '{type}'");
    }
    foreach (var typeArg in type.TypeArgs) {
      ReachType(typeArg, origin);
    }
    if (type is UserDefinedType { ResolvedClass: SubsetTypeDecl or NewtypeDecl or DatatypeDecl } udt) {
      EnqueueType(udt.ResolvedClass, udt.TypeArgs, current.DependentParameters);
    }
  }

  private static bool IsConstrainedArrowTypeDecl(SubsetTypeDecl decl) {
    return ArrowType.IsPartialArrowTypeName(decl.Name) || ArrowType.IsTotalArrowTypeName(decl.Name);
  }

  // ----- T-dependent types ------------------------------------------------------------------------------------

  private bool DependsOnOwner(Type type) => DependsOnOwner(type, current.DependentParameters);

  private bool DependsOnOwner(Type type, ISet<TypeParameter> dependentParameters) {
    type = type.NormalizeExpandKeepConstraints();
    if (type is UserDefinedType { ResolvedClass: TypeParameter typeParameter }) {
      return dependentParameters.Contains(typeParameter);
    }
    if (type.TypeArgs.Any(typeArg => DependsOnOwner(typeArg, dependentParameters))) {
      return true;
    }
    return type is UserDefinedType { ResolvedClass: { } typeDecl } && DeclDependsOnOwner(typeDecl);
  }

  /// <summary>
  /// Whether the values of "typeDecl", instantiated with type arguments that are not T-dependent, are defined with
  /// the values of T.  A declaration whose dependence is being computed counts as independent, which gives the
  /// least solution for recursive datatypes.
  /// </summary>
  private bool DeclDependsOnOwner(TopLevelDecl typeDecl) {
    if (declDependence.TryGetValue(typeDecl, out var result)) {
      return result;
    }
    declDependence[typeDecl] = false;
    var none = new HashSet<TypeParameter>();
    result = typeDecl switch {
      InvariantTypeDecl invariantTypeDecl => invariantTypeDecl.Owner == owner || DeclDependsOnOwner(invariantTypeDecl.Owner),
      _ when typeDecl == owner => true,
      NonNullTypeDecl nonNullTypeDecl => DeclDependsOnOwner(nonNullTypeDecl.Class),
      ClassLikeDecl { IsObjectTrait: true } => owner is ClassLikeDecl { IsReferenceTypeDecl: true },
      TopLevelDeclWithMembers withMembers when owner.HeadDerivesFrom(withMembers) || withMembers.HeadDerivesFrom(owner) => true,
      SubsetTypeDecl subsetTypeDecl => DependsOnOwner(subsetTypeDecl.Var.Type, none),
      TypeSynonymDecl synonym => DependsOnOwner(synonym.Rhs, none),
      NewtypeDecl newtypeDecl => DependsOnOwner(newtypeDecl.BaseType, none),
      DatatypeDecl datatypeDecl => datatypeDecl.Ctors.Any(ctor => ctor.Formals.Any(formal => DependsOnOwner(formal.Type, none))),
      _ => false
    };
    declDependence[typeDecl] = result;
    return result;
  }

  // ----- raw values ---------------------------------------------------------------------------------------------

  /// <summary>
  /// If "type" is a raw type, that is, a type with invariants or a subset type of it that does not say the
  /// invariants hold, return the declaration of that type.
  /// </summary>
  private static TopLevelDeclWithMembers? RawOwner(Type type) {
    return type.NormalizeExpand() is UserDefinedType { ResolvedClass: TopLevelDeclWithMembers { InvariantTypeDecl: not null } cl } &&
           RawMemberInference.IsRaw(type, cl)
      ? cl
      : null;
  }

  /// <summary>
  /// Whether converting a value of type "source" to "target" tests a raw value against a T-dependent type: either
  /// "source" is raw and "target" is a T-dependent type that is not one of its supertypes, or the two are
  /// collections, arrows or instances of one generic type whose arguments are so converted.
  /// </summary>
  private bool ConvertsRawValue(Type source, Type target) {
    if (source.Equals(target, true)) {
      return false;
    }
    if (RawOwner(source) != null) {
      return !Type.GetTowerOfSubsetTypes(source).Exists(supertype => supertype.Equals(target, true)) && DependsOnOwner(target);
    }
    var s = source.NormalizeExpand();
    var t = target.NormalizeExpand();
    return s.TypeArgs.Count != 0 && s.TypeArgs.Count == t.TypeArgs.Count && Type.SameHead(s, t) &&
           s.TypeArgs.Zip(t.TypeArgs).Any(pair => ConvertsRawValue(pair.First, pair.Second));
  }

  // ----- the clauses --------------------------------------------------------------------------------------------

  private string Subject {
    get {
      if ((current.Member is MemberInvariant own && own.EnclosingClass == owner) ||
          (current.Type is InvariantTypeDecl ownType && ownType.Owner == owner)) {
        return $"the invariant of '{owner.Name}'";
      }
      var what = current.Member switch {
        MemberInvariant invariant => $"the invariant of '{invariant.EnclosingClass.Name}'",
        { } member => $"'{member.Name}'",
        _ => current.Type is InvariantTypeDecl invariantTypeDecl ? $"the invariant of '{invariantTypeDecl.Owner.Name}'" : $"'{current.Type!.Name}'"
      };
      return $"{what} is used by the invariant of '{owner.Name}' and";
    }
  }

  private void Error(IOrigin origin, string message) {
    if (!reported.Add((origin.Uri?.ToString(), origin.Center.pos))) {
      return;
    }
    reporter.Error(MessageSource.Resolver, origin, $"{Subject} {message}");
  }

  private string DependsOnOwnerNote(Type type) {
    var name = type.ToString();
    return name == owner.Name ? "" : $", which depends on '{owner.Name}'";
  }

  /// <summary>
  /// Clauses 1 and 5 for a bound variable of a quantifier, a comprehension or a let-such-that expression.
  /// </summary>
  private void CheckBoundVariable(BoundVar boundVar, BoundedPool? bound) {
    if (!DependsOnOwner(boundVar.Type)) {
      ReachType(boundVar.Type, boundVar.Origin);
      return;
    }
    var elementType = bound switch {
      CollectionBoundedPool collection => collection.CollectionElementType,
      ExactBoundedPool exact => exact.E.Type,
      SubSetBoundedPool subset => subset.UpperBound.Type,
      _ => null
    };
    if (elementType == null) {
      Error(boundVar.Origin, $"refers to the set of values of '{boundVar.Type}'{DependsOnOwnerNote(boundVar.Type)}");
    } else if (ConvertsRawValue(elementType, boundVar.Type)) {
      Error(boundVar.Origin, $"tests a raw value against '{boundVar.Type}'");
    }
    ReachType(boundVar.Type, boundVar.Origin);
  }

  /// <summary>
  /// Clause 5 for an implicit conversion of "expr" to "target": an argument, a component, an element, a bound
  /// value or a result.
  /// </summary>
  private void CheckSlot(Expression expr, Type target) {
    ReachType(target, expr.Origin);
    foreach (var terminal in expr.TerminalExpressions) {
      if (terminal.Type != null && ConvertsRawValue(terminal.Type, target)) {
        Error(terminal.Origin, $"converts a raw value to '{target}'");
      }
    }
  }

  /// <summary>
  /// Clause 5 for a receiver: a member that is not a raw member of a type with invariants (a member of the receiver's
  /// type or of a trait it extends, that is not raw, or a member of a trait without invariants) is applied to a value
  /// of the receiver's type, so a raw receiver is converted to it. A field whose value is stored needs no conversion.
  /// </summary>
  private void CheckReceiver(Expression? receiver, MemberDecl member) {
    if (receiver?.Type == null || member is { IsStatic: true } or SpecialField || RawMemberInference.IsStoredField(member) ||
        (RawMemberInference.HasRawCounterpart(member) && RawMemberInference.IsRawMember(member)) ||
        RawOwner(receiver.Type) is not { InvariantTypeDecl: { } invariantTypeDecl }) {
      return;
    }
    var target = new UserDefinedType(receiver.Origin, invariantTypeDecl.Name, invariantTypeDecl, receiver.Type.NormalizeExpand().TypeArgs);
    ReachType(target, receiver.Origin);
    if (DependsOnOwner(target)) {
      Error(receiver.Origin, $"converts a raw value to '{target}', by applying '{member.Name}', which is not a raw member, to it");
    }
  }

  /// <summary>
  /// Clauses 2 and 4 for a use of "function", called or as a value, and the function's place in the cone.
  /// </summary>
  private void CheckFunctionUse(Function function, IEnumerable<Type>? typeArgs, IOrigin origin) {
    foreach (var typeArg in typeArgs ?? []) {
      if (DependsOnOwner(typeArg)) {
        Error(origin, $"instantiates '{function.Name}' with '{typeArg}', a type that depends on '{owner.Name}', which is not yet supported");
        break;
      }
    }
    if (function is { IsStatic: false, Body: null, EnclosingClass: TraitDecl }) {
      Error(origin, $"uses '{function.Name}', which is dynamically bound, so the bodies it stands for are not all known");
    }
    Enqueue(function);
  }

  /// <summary>
  /// Clause 3 for "fn", which is applied: only a lambda expression or a named function, applied on the spot, has a
  /// body that the cone can see.
  /// </summary>
  private void CheckApplied(Expression fn) {
    switch (fn.Resolved) {
      case LambdaExpr:
      case MemberSelectExpr { Member: Function { EnclosingClass: not ArrowTypeDecl } }:
        return;
      default:
        Error(fn.Origin, "applies a function value that is not a lambda expression or a named function, so the body it applies is not known");
        return;
    }
  }

  private void Visit(Expression expr) {
    switch (expr) {
      case LambdaExpr lambda:
        foreach (var boundVar in lambda.BoundVars) {
          ReachType(boundVar.Type, boundVar.Origin);
        }
        if (lambda.Type.AsArrowType is { } lambdaType) {
          CheckSlot(lambda.Body, lambdaType.Result);
        }
        break;
      case ComprehensionExpr comprehension:
        for (var i = 0; i < comprehension.BoundVars.Count; i++) {
          CheckBoundVariable(comprehension.BoundVars[i], comprehension.Bounds?.ElementAtOrDefault(i));
        }
        if (comprehension is SetComprehension setComprehension && setComprehension.Type.AsSetType is { } setType) {
          CheckSlot(setComprehension.Term, setType.Arg);
        } else if (comprehension is MapComprehension mapComprehension && mapComprehension.Type.AsMapType is { } mapType) {
          if (mapComprehension.TermLeft != null) {
            CheckSlot(mapComprehension.TermLeft, mapType.Domain);
          }
          CheckSlot(mapComprehension.Term, mapType.Range);
        }
        break;
      case LetExpr { Exact: false } letSuchThat:
        var boundVars = letSuchThat.BoundVars.ToList();
        for (var i = 0; i < boundVars.Count; i++) {
          CheckBoundVariable(boundVars[i], letSuchThat.Constraint_Bounds?.ElementAtOrDefault(i));
        }
        break;
      case LetExpr let:
        for (var i = 0; i < let.LHSs.Count && i < let.RHSs.Count; i++) {
          if ((let.LHSs[i].Var?.Type ?? let.LHSs[i].Expr?.Type) is { } lhsType) {
            CheckSlot(let.RHSs[i], lhsType);
          }
        }
        break;
      case FunctionCallExpr call when call.Function != null:
        if (call.Function.EnclosingClass is ArrowTypeDecl) {
          // f.requires(x) or f.reads(x): the specification of a function value
          CheckApplied(call.Receiver);
        } else {
          CheckFunctionUse(call.Function, (call.TypeApplication_AtEnclosingClass ?? []).Concat(call.TypeApplication_JustFunction ?? []), call.Origin);
          CheckReceiver(call.Receiver, call.Function);
        }
        var typeMap = call.GetTypeArgumentSubstitutions();
        for (var i = 0; i < call.Args.Count && i < call.Function.Ins.Count; i++) {
          CheckSlot(call.Args[i], call.Function.Ins[i].Type.Subst(typeMap));
        }
        break;
      case MemberSelectExpr select:
        if (select.Member is Function function) {
          CheckFunctionUse(function, (select.TypeApplicationAtEnclosingClass ?? []).Concat(select.TypeApplicationJustMember ?? []), select.Origin);
        } else if (select.Member is ConstantField { Rhs: not null } constant) {
          Enqueue(constant);
        }
        CheckReceiver(select.Obj, select.Member);
        break;
      case ApplyExpr apply:
        CheckApplied(apply.Function);
        if (apply.Function.Type.AsArrowType is { } arrowType) {
          for (var i = 0; i < apply.Args.Count && i < arrowType.Args.Count; i++) {
            CheckSlot(apply.Args[i], arrowType.Args[i]);
          }
        }
        break;
      case SeqConstructionExpr construction:
        CheckApplied(construction.Initializer);
        break;
      case DatatypeValue datatypeValue when datatypeValue.Ctor != null:
        var datatypeMap = TypeParameter.SubstitutionMap(datatypeValue.Ctor.EnclosingDatatype.TypeArgs, datatypeValue.InferredTypeArgs);
        for (var i = 0; i < datatypeValue.Arguments.Count && i < datatypeValue.Ctor.Formals.Count; i++) {
          CheckSlot(datatypeValue.Arguments[i], datatypeValue.Ctor.Formals[i].Type.Subst(datatypeMap));
        }
        break;
      case DisplayExpression display when display.Type.NormalizeExpand() is CollectionType collectionType:
        display.Elements.ForEach(element => CheckSlot(element, collectionType.Arg));
        break;
      case MapDisplayExpr mapDisplay when mapDisplay.Type.AsMapType is { } displayMapType:
        foreach (var entry in mapDisplay.Elements) {
          CheckSlot(entry.A, displayMapType.Domain);
          CheckSlot(entry.B, displayMapType.Range);
        }
        break;
      case SeqUpdateExpr update:
        switch (update.Seq.Type.NormalizeExpand()) {
          case SeqType seqType:
            CheckSlot(update.Value, seqType.Arg);
            break;
          case MapType updatedMapType:
            CheckSlot(update.Index, updatedMapType.Domain);
            CheckSlot(update.Value, updatedMapType.Range);
            break;
          case MultiSetType multiSetType:
            CheckSlot(update.Index, multiSetType.Arg);
            break;
        }
        break;
      case TypeTestExpr test:
        ReachType(test.ToType, test.Origin);
        if (ConvertsRawValue(test.E.Type, test.ToType)) {
          Error(test.Origin, $"tests a raw value against '{test.ToType}'");
        }
        break;
      case ConversionExpr conversion:
        ReachType(conversion.ToType, conversion.Origin);
        if (ConvertsRawValue(conversion.E.Type, conversion.ToType)) {
          Error(conversion.Origin, $"converts a raw value to '{conversion.ToType}'");
        }
        break;
    }
    foreach (var subExpression in expr.SubExpressions) {
      Visit(subExpression);
    }
  }
}
