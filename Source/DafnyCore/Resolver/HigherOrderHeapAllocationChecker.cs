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
/// by storing functions in memory (aka Landin's knots)
/// Thanks to frame information, we need not reject all assignments of
/// functions to memory, but only the ones that are know to have a 
/// read frame.
/// </summary>
class HigherOrderHeapAllocationChecker : ASTVisitor<IASTVisitorContext> {
  private readonly ErrorReporter reporter;
  private readonly bool legacy;

  public HigherOrderHeapAllocationChecker(ErrorReporter reporter) {
    this.reporter = reporter;
    legacy = reporter.Options.Get(CommonOptionBag.LegacyReadingFunctionsInDatatypes);
  }

  public override IASTVisitorContext GetContext(IASTVisitorContext astVisitorContext, bool inFunctionPostcondition) {
    return astVisitorContext;
  }

  /// <summary>
  /// ContainsRead is a pure function that visits a type to test
  /// for the presence of a memory-reading arrow type ( ~> ).
  /// </summary>
  private bool ContainsRead(Type rhs) {
    return ContainsRead(rhs, legacy ? null : []);
  }

  /// <summary>
  /// The search of ContainsRead, which also looks inside the declarations of the types it meets
  /// (https://github.com/erniecohen/dafny/issues/479): the arguments of the constructors of an
  /// inductive or coinductive datatype, the base type of a newtype or a subset type, each with the
  /// type arguments substituted.  A value of such a type can carry a function that reads the heap,
  /// and a field that holds the value lets the function read itself.  "visited" holds the
  /// declarations already searched, so that a recursive datatype ends the search; the type
  /// arguments of each occurrence are searched there, so a declaration needs to be searched once.
  /// It is null under the hidden --legacy-reading-functions-in-datatypes, which restores the search
  /// of the type and its type arguments alone.
  /// </summary>
  private static bool ContainsRead(Type rhs, HashSet<TopLevelDecl> visited) {

    Type type = rhs.NormalizeExpandKeepConstraints();

    if (type is BasicType) {
      return false;
    } else if (type is MapType) {
      var t = (MapType)type;
      return ContainsRead(t.Domain, visited) || ContainsRead(t.Range, visited);
    } else if (type is CollectionType) {
      var t = (CollectionType)type;
      return ContainsRead(t.Arg, visited);
    } else {
      var t = (UserDefinedType)type;
      if (t.IsArrowType && !t.IsArrowTypeWithoutReadEffects) {
        return true;
      }
      if (BoundedCallables.IsBoundedDecl(t.ResolvedClass)) {
        // Under --modular-termination, the function of a bounded value runs under its bound in every later
        // state: its formation checks a lambda's body in every successor heap, against the bound evaluated when
        // it is formed, and each application, from whatever field or element it is read, checks the bound it
        // reads to be below that of the code applying it.  A cycle through the heap would apply a value under
        // its own bound or a smaller one (issue 266, M3).  Only the top-level arrow of the function type is
        // exempt: the order type and the function's parameter and result types may hold raw heap-reading
        // functions, which the bounded function could hand out to code that applies them without a bound, as in
        // "bounded<nat, () -> (() ~> bool)>".  They are searched as any other type is, inside datatypes, newtypes
        // and subset types too (issue 479).
        var function = t.TypeArgs[1].NormalizeExpandKeepConstraints();
        var components = function.IsArrowType ? function.AsArrowType.TypeArgs : [function];
        return ContainsRead(t.TypeArgs[0], visited) || components.Exists(component => ContainsRead(component, visited));
      }
      if (t.TypeArgs.Exists(typeArg => ContainsRead(typeArg, visited))) {
        return true;
      }
      // The arrow types without read effects are subset types of the arrow type that reads, so the
      // search does not follow their base types.
      if (visited == null || t.IsArrowType || t.ResolvedClass is not { } decl || !visited.Add(decl)) {
        return false;
      }
      return Components(t, decl).Any(component => ContainsRead(component, visited));
    }

  }

  /// <summary>
  /// The types of the values that a value of type "t", whose declaration is "decl", holds: the
  /// arguments of the constructors of a datatype, or the base type of a newtype or a subset type.
  /// </summary>
  private static IEnumerable<Type> Components(UserDefinedType t, TopLevelDecl decl) {
    switch (decl) {
      case DatatypeDecl datatype: {
          var subst = TypeParameter.SubstitutionMap(datatype.TypeArgs, t.TypeArgs);
          return datatype.Ctors.SelectMany(ctor => ctor.Formals).Select(formal => formal.Type.Subst(subst));
        }
      case NewtypeDecl { BaseType: not null } newtype:
        return [newtype.RhsWithArgument(t.TypeArgs)];
      case SubsetTypeDecl subset:
        return [subset.RhsWithArgumentIgnoringScope(t.TypeArgs)];
      default:
        return [];
    }
  }

  /// <summary>
  /// VisitOneStatement checks that we do not store in memory a function of
  /// type . ~> .
  /// </summary>
  protected override bool VisitOneStatement(Statement stmt, IASTVisitorContext context) {

    // Since all updates and variable declarations are eventually broken down into
    // assignments, we need only consider an AssignStmt.
    if (stmt is SingleAssignStmt { Lhs: { } lhs, Rhs: { } rhs }) {

      // Memory can be updated either by writing to a sequence-like collection
      // or by writing to an object's field.
      if (lhs is MemberSelectExpr or SeqSelectExpr) {

        // The only case of interest is when the right hand side of the assignment 
        // is an expression. The case where it is a type expression (new) is handled
        // by a different check.
        if (rhs is ExprRhs { Expr: { Type: { } type } }) {

          // If the assignment contains a function with read effects, it must be rejected.
          // It would not be enough to check that the RHS is of type . ~> . because
          // one can hide such a function deep into another type.
          // It is plausible that using write frame information, we could relax this check
          // by ensuring that the read frame of the function and the write frame of the context
          // are disjoint, but this would require an inter-procedural analysis.
          if (ContainsRead(type)) {
            reporter.Error(MessageSource.Resolver, stmt,
              "To prevent the creation of non-terminating functions, storing functions with read effects into memory is disallowed");
          }
        }
      }
    }

    return base.VisitOneStatement(stmt, context);
  }
}