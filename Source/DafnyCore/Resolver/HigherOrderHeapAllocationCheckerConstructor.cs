//-----------------------------------------------------------------------------
//
// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
//
//-----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// This checker prevents the definition of non-terminating functions
/// by storing functions in memory (aka Landin's knots) during the 
/// creation of an object. This could also be prevented by doing 
/// a cardinality check on the type of class (similar to what is done
/// for algebraic datatypes) but at a fundamental level, the issue is
/// the creation of a Landin's knot, and it is easier and safer to catch
/// this way because of type parameters and traits.
/// Thanks to frame information, we need not reject all assignments of
/// functions to memory, but only the ones that are know to have a 
/// read frame.
/// To understand this checker, it is recommended to first look at
/// HigherOrderHeapAllocationChecker.
/// </summary>
class HigherOrderHeapAllocationCheckerConstructor : ASTVisitor<IASTVisitorContext> {
  private readonly ErrorReporter reporter;
  private readonly bool legacy;

  public HigherOrderHeapAllocationCheckerConstructor(ErrorReporter reporter, DafnyOptions options) {
    this.reporter = reporter;
    legacy = options.Get(CommonOptionBag.LegacyStoredFunctionCallbacks);
  }

  public override IASTVisitorContext GetContext(IASTVisitorContext astVisitorContext, bool inFunctionPostcondition) {
    return astVisitorContext;
  }

  /// <summary>
  /// Occurs is a pure function that visits a type (rhs) to test
  /// for the presence of an heap-allocated type (Obj) left of
  /// an arrow. 
  /// This check could be relaxed but we keep it simple until
  /// we encounter a good use case for the more general check.
  /// Recall that a cycle can only be created using a function,
  /// which is why the test is different than a traditional
  /// cardinality test.
  /// </summary>
  private bool Occurs(Type obj, Type rhs, bool left) {
    Type type = rhs.NormalizeExpand();
    if (type is BasicType) {
      return false;
    } else if (type is MapType) {
      var t = (MapType)type;
      return Occurs(obj, t.Domain, left) || Occurs(obj, t.Range, left);
    } else if (type is SetType) {
      var t = (SetType)type;
      return Occurs(obj, t.Arg, left);
    } else if (type is CollectionType) {
      var t = (CollectionType)type;
      return Occurs(obj, t.Arg, left);
    } else {
      var t = (UserDefinedType)type;
      if (BoundedCallables.IsBoundedDecl(t.ResolvedClass)) {
        // A bounded value (--modular-termination, issue 266) cannot close a knot by being applied: each
        // application is checked to run under a bound strictly below that of the code applying it, and the
        // function it applies is checked to run under the value's bound.  So only the top-level arrow of the
        // function type is exempt.  The order type, and the function's parameter and result types, may still hold
        // raw functions, which the bounded function can hand out to code that applies them without a bound: a
        // value of type "bounded<nat, () -> (Y -> int)>" can return a function on "Y" that applies it again.
        // They are checked as if they appeared where the bounded type does.
        var function = t.TypeArgs[1].NormalizeExpand();
        var components = function.IsArrowType ? function.TypeArgs : [function];
        return Occurs(obj, t.TypeArgs[0], left) || components.Exists(typeArg => Occurs(obj, typeArg, left));
      }
      if (left && Type.Equal_Improved(obj, t)) {
        return true;
      }
      if (left && t.IsTraitType) {
        return true;
      }
      var b = false;
      if (t.IsArrowType) {
        var arrow = type.AsArrowType;
        b = b || arrow.TypeArgs.Exists(typeArg => Occurs(obj, typeArg, true));
      }
      return b || t.TypeArgs.Exists(typeArg => Occurs(obj, typeArg, left));
    }
  }

  /// <summary>
  /// The search that Occurs makes, extended to the declarations of the types it meets
  /// (https://github.com/erniecohen/dafny/issues/453).  A function value that a constructor stores
  /// in a field can be applied, by a function of the object, to a value from which the function
  /// value reaches the object again without reading the heap: through the arguments of a datatype
  /// constructor or the components of a tuple, the constants of a class or trait, or the base type
  /// of a newtype.  Then neither call has a termination check, and the two functions are a cycle.
  /// So, to the left of an arrow, the search follows those types, and it fails at the class of the
  /// object, with any type arguments, and at any trait type or abstract type, which may stand for
  /// such a type.  Elsewhere in the type of the stored value it looks for function values the same
  /// way, through the same declarations; there the type arguments of a trait type or an abstract
  /// type are taken to be left of an arrow, because an implementation or a definition may store a
  /// function of them.  A type parameter of the enclosing declaration is not followed: the class
  /// that is instantiated with the object's type is checked where an instance is stored.  The
  /// hidden --legacy-stored-function-callbacks restores the search that Occurs makes alone.
  /// </summary>
  private sealed class Reach {
    public readonly TopLevelDecl ObjDecl;
    private readonly HashSet<(string, bool)> visited = [];
    // The size of the instantiation of each declaration, in each position, that is being expanded
    private readonly Dictionary<(TopLevelDecl, bool), int> expanding = new();

    public Reach(Type obj) {
      ObjDecl = Declaration(obj as UserDefinedType);
    }

    private static TopLevelDecl Declaration(UserDefinedType type) {
      return type?.ResolvedClass is NonNullTypeDecl nonNull ? nonNull.Class : type?.ResolvedClass;
    }

    /// <summary>
    /// The declaration of the type left of an arrow at which the search fails, or null.
    /// </summary>
    public TopLevelDecl Occurs(Type rhs, bool left) {
      var type = rhs.NormalizeExpand();
      if (type is BasicType) {
        return null;
      }
      if (type is MapType map) {
        return Occurs(map.Domain, left) ?? Occurs(map.Range, left);
      }
      if (type is CollectionType collection) {
        return Occurs(collection.Arg, left);
      }
      if (type is not UserDefinedType udt) {
        return null;
      }
      if (!visited.Add((udt.ToString(), left))) {
        return null;
      }
      var decl = Declaration(udt);
      if (BoundedCallables.IsBoundedDecl(decl)) {
        // A bounded value (--modular-termination, https://github.com/erniecohen/dafny/issues/266) is exempt as in
        // the Occurs of the checker: applying it is checked to run under a bound strictly below that of the code
        // that applies it, and its function is checked to run under the value's bound, so applications through
        // bounded values cannot form a cycle, and its top-level arrow is not searched.  Its order type and the
        // parameter and result types of its function may hold raw functions, which the bounded function can hand
        // to code that applies them without a bound, so they are searched where the bounded type is.
        var function = udt.TypeArgs[1].NormalizeExpand();
        var components = function.IsArrowType ? function.TypeArgs : [function];
        return Occurs(udt.TypeArgs[0], left) ?? components.Select(component => Occurs(component, left)).FirstOrDefault(t => t != null);
      }
      if (left && ((ObjDecl != null && decl == ObjDecl) || decl is TraitDecl || decl is AbstractTypeDecl)) {
        return decl;
      }
      if (udt.IsArrowType) {
        return udt.TypeArgs.Select(typeArg => Occurs(typeArg, true)).FirstOrDefault(t => t != null);
      }
      var argumentsLeft = left || decl is TraitDecl || decl is AbstractTypeDecl;
      var found = udt.TypeArgs.Select(typeArg => Occurs(typeArg, argumentsLeft)).FirstOrDefault(t => t != null);
      if (found != null || decl is not TopLevelDeclWithMembers withMembers || decl is TraitDecl || decl is AbstractTypeDecl) {
        return found;
      }
      // A declaration can recur at a larger instantiation, as in a nested datatype
      // N<A> = Leaf(a: A) | Node(n: N<seq<A>>), whose expansion makes a larger type at every step.  So a
      // declaration that is being expanded, in the same position, is not expanded again at a larger
      // instantiation: the parts of its components that do not come from its type parameters are the
      // same at every instantiation, and they are searched by the expansion in progress.  Only the type
      // arguments differ, and they are searched as if left of an arrow, the position in which the
      // search fails the most.  An instantiation that is not larger is expanded, as for Box<Box<C>>.
      // On each path of the search, then, the instantiations of a declaration in a position do not
      // grow; there are finitely many types of a given size, and "visited" stops a repeated one, so
      // the search ends.
      var key = (withMembers, left);
      var size = Size(udt);
      var nested = expanding.TryGetValue(key, out var outer);
      if (nested && size > outer) {
        return udt.TypeArgs.Select(typeArg => Occurs(typeArg, true)).FirstOrDefault(t => t != null);
      }
      expanding[key] = size;
      try {
        var subst = TypeParameter.SubstitutionMap(withMembers.TypeArgs, udt.TypeArgs);
        return Components(withMembers, subst).Select(component => Occurs(component, left)).FirstOrDefault(t => t != null);
      } finally {
        if (nested) {
          expanding[key] = outer;
        } else {
          expanding.Remove(key);
        }
      }
    }

    /// <summary>
    /// The number of type constructors in a type, after its synonyms are expanded.
    /// </summary>
    private static int Size(Type type) {
      return 1 + type.NormalizeExpand().TypeArgs.Sum(Size);
    }

    /// <summary>
    /// The types of the values that a function can get, without reading the heap, from a value of
    /// the type that "decl" declares: the arguments of its constructors, its base type, and its
    /// constants, with those of its parent traits.
    /// </summary>
    private static IEnumerable<Type> Components(TopLevelDeclWithMembers decl, Dictionary<TypeParameter, Type> subst) {
      var types = new List<Type>();
      if (decl is DatatypeDecl datatype) {
        types.AddRange(datatype.Ctors.SelectMany(ctor => ctor.Formals).Select(formal => formal.Type.Subst(subst)));
      }
      if (decl is NewtypeDecl { BaseType: { } baseType }) {
        types.Add(baseType.Subst(subst));
      }
      types.AddRange(decl.Members.OfType<Field>().Where(field => !field.IsStatic && !field.IsMutable)
        .Select(field => field.Type.Subst(subst)));
      foreach (var parent in decl.ParentTypes(decl.TypeArgs.ConvertAll(tp => (Type)new UserDefinedType(tp)), false)) {
        if (parent.NormalizeExpand() is UserDefinedType { ResolvedClass: TopLevelDeclWithMembers parentDecl } parentType) {
          var parentSubst = TypeParameter.SubstitutionMap(parentDecl.TypeArgs, parentType.TypeArgs.ConvertAll(t => t.Subst(subst)));
          types.AddRange(Components(parentDecl, parentSubst));
        }
      }
      return types;
    }
  }

  protected override bool VisitOneStatement(Statement stmt, IASTVisitorContext context) {

    // Assigments to constant fields in constructors boil down to an assignment.
    if (stmt is SingleAssignStmt assign) {

      var lhs = assign.Lhs;
      Type lhsType;

      // We need to make sure that if a function is written to a field
      // it does not refer to the type of the object being assigned to.
      // Note that the function may not be of type . ~> . because
      // functions of type . ~> . can be assigned to constant fields
      // of type . -> . during the object's construction.
      if (lhs is MemberSelectExpr mseLhs) {

        lhsType = mseLhs.Obj.Type.NormalizeExpand();

        var rhs = assign.Rhs;
        if (rhs is ExprRhs eRhs) {
          var exp = eRhs.Expr;
          var type = exp.Type;

          if (Occurs(lhsType, type, false)) {
            reporter.Error(MessageSource.Resolver, stmt,
              "To prevent the creation of non-terminating functions, storing functions into an object's fields that reads the object is disallowed");
          } else if (!legacy && new Reach(lhsType) is var reach && reach.Occurs(type, false) is { } via) {
            reporter.Error(MessageSource.Resolver, stmt,
              "To prevent the creation of non-terminating functions, storing functions into an object's fields that reads the object is disallowed: " +
              $"a function in the stored value takes an argument from which a function can reach a value of type '{via.Name}' without reading the heap" +
              (via == reach.ObjDecl ? "" : ", and such a value may be the object"));
          }
        }
      }
    }

    return base.VisitOneStatement(stmt, context);
  }
}