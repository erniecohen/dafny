#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// Program-wide checks for member invariants (issue 105, --member-invariants), made once before the
/// modules are resolved:
///  * the option requires --type-system-refresh;
///  * a codatatype may not declare an invariant or a witness member;
///  * a datatype declares at most one witness member, and only if it has an invariant; a class or an abstract type
///    declares none (witness members of traits and newtypes are not yet supported);
///  * the declaration rule: a program that declares a member invariant may not declare a codatatype.
///    Libraries are part of the program, so the rule covers them too.
/// The checks are syntactic, so they see every declaration, including those of abstract modules and of
/// modules that are refined.  None of them runs without the option.
/// </summary>
public static class MemberInvariantChecks {

  /// <summary>
  /// Returns false, after reporting errors, if the program cannot be resolved.
  /// </summary>
  public static bool Check(Program program, ErrorReporter reporter) {
    var options = program.Options;
    if (!options.Get(CommonOptionBag.MemberInvariants)) {
      return true;
    }
    if (!options.Get(CommonOptionBag.TypeSystemRefresh)) {
      reporter.Error(MessageSource.Resolver, Token.NoToken, "use of --member-invariants requires --type-system-refresh");
      return false;
    }

    var errorCount = reporter.ErrorCount;
    var invariants = new List<(MemberInvariant Invariant, TopLevelDeclWithMembers Type)>();
    var codatatypes = new List<CoDatatypeDecl>();
    foreach (var decl in AllTopLevelDecls(program.DefaultModuleDef)) {
      if (decl is CoDatatypeDecl codatatype) {
        codatatypes.Add(codatatype);
      }
      if (decl is not TopLevelDeclWithMembers withMembers) {
        continue;
      }
      foreach (var member in withMembers.Members) {
        if (member is MemberInvariant invariant) {
          if (decl is CoDatatypeDecl) {
            reporter.Error(MessageSource.Resolver, invariant.NameNode.Origin,
              "a codatatype may not declare an invariant: its values are not built up from finitely many constructor applications, so an invariant cannot be checked when they are constructed");
          } else {
            invariants.Add((invariant, withMembers));
          }
        } else if (member is MemberWitness witness) {
          if (decl is CoDatatypeDecl) {
            reporter.Error(MessageSource.Resolver, witness.NameNode.Origin,
              "a codatatype may not declare a witness: a witness is only needed by a type with an invariant");
          } else if (decl is ClassDecl) {
            reporter.Error(MessageSource.Resolver, witness.NameNode.Origin,
              "a class may not declare a witness: its objects are the ones its constructors return, so it needs none");
          } else if (decl is AbstractTypeDecl) {
            reporter.Error(MessageSource.Resolver, witness.NameNode.Origin,
              "an abstract type may not declare a witness: its characteristics, '(0)' or '(00)', promise one, and each refinement that defines the type supplies it");
          } else if (decl is not IndDatatypeDecl datatype) {
            reporter.Error(MessageSource.Resolver, witness.NameNode.Origin,
              $"a witness member of a {decl.WhatKind} is not yet supported");
          } else if (withMembers.Members.OfType<MemberWitness>().First() != witness) {
            reporter.Error(MessageSource.Resolver, witness.NameNode.Origin,
              $"'{decl.Name}' declares more than one witness");
          } else if (!withMembers.Members.OfType<MemberInvariant>().Any() && datatype.Traits.Count == 0 &&
                     datatype.EnclosingModuleDefinition.Implements == null) {
            // A refining declaration may add a witness for the invariants of the declaration it refines, and a
            // datatype may inherit invariants from its parent traits.
            reporter.Error(MessageSource.Resolver, witness.NameNode.Origin,
              $"'{decl.Name}' declares no invariant, so it needs no witness: its values are its constructors' values");
          }
        }
      }
    }

    if (invariants.Count != 0 && codatatypes.Count != 0) {
      // Report the violation once, in the program's own source where possible, rather than in a library.
      var compilation = program.Compilation;
      var (invariant, type) = invariants.FirstOrDefault(i => i.Invariant.ShouldVerify(compilation), invariants[0]);
      var codatatype = codatatypes.FirstOrDefault(c => c.ShouldVerify(compilation)) ?? codatatypes[0];
      const string rule = "a program that declares a member invariant may not declare a codatatype";
      if (invariant.ShouldVerify(compilation) || !codatatype.ShouldVerify(compilation)) {
        reporter.Error(MessageSource.Resolver, invariant.NameNode.Origin,
          $"{rule}, but it declares codatatype '{codatatype.Name}'{InModule(codatatype.EnclosingModuleDefinition)}");
      } else {
        reporter.Error(MessageSource.Resolver, codatatype.NameNode.Origin,
          $"{rule}, but '{type.Name}'{InModule(type.EnclosingModuleDefinition)} declares an invariant");
      }
    }
    return reporter.ErrorCount == errorCount;
  }

  /// <summary>
  /// The reads restrictions of section 3.2 of the plan, checked syntactically on the resolved body of every
  /// invariant declared in "declarations": an invariant depends on no mutable state, so that it can be
  /// translated to a function with no heap argument.  It reads no mutable field and no array element, uses no
  /// old, fresh, allocated or unchanged, calls no two-state function or lemma, uses no function that has a
  /// reads clause, and applies no function value that may read the heap.  Of these, the checks of every
  /// function body already reject old, fresh, unchanged, allocated and two-state functions and lemmas (an
  /// invariant is not a two-state context, and has no reads clause), so they are not repeated here.
  /// </summary>
  public static void CheckReadsRestrictions(IEnumerable<TopLevelDecl> declarations, ErrorReporter reporter) {
    var visitor = new ReadsRestrictionVisitor(reporter);
    foreach (var decl in declarations.OfType<TopLevelDeclWithMembers>()) {
      foreach (var invariant in decl.Members.OfType<MemberInvariant>()) {
        if (invariant.Body != null) {
          visitor.Visit(invariant.Body);
        }
      }
    }
  }

  private class ReadsRestrictionVisitor(ErrorReporter reporter) : BottomUpVisitor {
    private void Error(Expression expr, string what) {
      reporter.Error(MessageSource.Resolver, expr.Origin, $"an invariant may not {what}: it depends on no mutable state");
    }

    protected override void VisitOneExpr(Expression expr) {
      switch (expr) {
        case MemberSelectExpr { Member: Field { IsMutable: true } field }:
          Error(expr, $"read the mutable field '{field.Name}'");
          break;
        case MemberSelectExpr { Member: Function function } select when MayReadHeap(function, select.Obj):
          Error(expr, HeapReadingUse("use", function));
          break;
        case FunctionCallExpr { Function: { } function } call when MayReadHeap(function, call.Receiver):
          Error(expr, HeapReadingUse("call", function));
          break;
        case SeqSelectExpr select when select.Seq.Type.IsArrayType:
          Error(expr, "read an array element");
          break;
        case MultiSelectExpr:
          Error(expr, "read an array element");
          break;
        case ApplyExpr apply when !apply.Function.Type.IsArrowTypeWithoutReadEffects:
          Error(expr, "apply a function value that may read the heap");
          break;
      }
    }

    private static string HeapReadingUse(string verb, Function function) {
      return function.EnclosingClass is ArrowTypeDecl
        ? $"use '{function.Name}' of a function value that may read the heap"
        : $"{verb} the function '{function.Name}', which has a reads clause";
    }

    /// <summary>
    /// Whether a use of "function" on "receiver" may read the heap.  The built-in members "requires" and "reads"
    /// of a function value read what the function value may read, which is nothing for "->" and "-->".
    /// </summary>
    private static bool MayReadHeap(Function function, Expression receiver) {
      if (function.EnclosingClass is ArrowTypeDecl) {
        return !receiver.Type.IsArrowTypeWithoutReadEffects;
      }
      return function.Reads.Expressions.Count != 0;
    }
  }

  private static string InModule(ModuleDefinition module) {
    return module.IsDefaultModule ? "" : $" in module '{module.Name}'";
  }

  private static IEnumerable<TopLevelDecl> AllTopLevelDecls(ModuleDefinition module) {
    foreach (var decl in module.TopLevelDecls) {
      yield return decl;
      if (decl is LiteralModuleDecl literal) {
        foreach (var inner in AllTopLevelDecls(literal.ModuleDef)) {
          yield return inner;
        }
      }
    }
  }
}
