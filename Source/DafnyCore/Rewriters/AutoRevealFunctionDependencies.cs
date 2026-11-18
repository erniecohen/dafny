using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Numerics;
using Microsoft.Boogie;

namespace Microsoft.Dafny;

/// <summary>
/// When Dafny is called with `--default-function-opacity autoRevealDependencies`, this rewriter computes
/// all transitive functional dependencies for each callable, and inserts (in-memory, on the AST) reveal stmts for each such dependency
/// at the top of the callable body, and also in a precondition.
///
/// For example:
///   function f()
///     ensures true
///   { g(h()) }
///
/// would get transformed to:
///   function f()
///     requires reveal g(); reveal h(); true
///     ensures true
///   {
///     reveal g(); reveal h();
///     g(h())
///   }
///
/// assuming that g() and h() don't have the `{:transparent}` attribute.
/// </summary>
public class AutoRevealFunctionDependencies : IRewriter {
  public AutoRevealFunctionDependencies(ErrorReporter reporter) : base(reporter) {
  }

  internal override void PostResolveIntermediate(ModuleDefinition moduleDefinition) {
    Contract.Requires(moduleDefinition != null);

    foreach (var decl in moduleDefinition.TopLevelDecls) {
      if (decl is ICanAutoRevealDependencies m) {
        m.AutoRevealDependencies(this, Options, Reporter);
      }

      if (decl is TopLevelDeclWithMembers cl) {
        foreach (var member in cl.Members) {
          if (member is ICanAutoRevealDependencies mem) {
            mem.AutoRevealDependencies(this, Options, Reporter);
          }
        }

        if (cl.InvariantTypeDecl is { Witness: { } witness } invariantTypeDecl && !cl.Members.OfType<MemberWitness>().Any()) {
          // Issue 105: the witness clause of the header of a newtype with member invariants is sugar for a witness
          // member (section 3.5 of the plan), so it needs what a witness member needs (see ConstantField)
          invariantTypeDecl.Witness = AddRevealStmtsToExpression(witness,
            InvariantFunctionalDependencies(invariantTypeDecl, witness.Origin, moduleDefinition).Distinct());
        }
      }
    }
  }

  /// <summary>
  /// Wrapper class created for traversing call graphs of modules. It stores the actual call graph vertex and
  /// a boolean flag `local` indicating whether the callable is in the same module as its predecessor in the traversal.
  /// </summary>
  private class GraphTraversalVertex {
    public readonly Graph<ICallable>.Vertex Vertex;
    public readonly bool Local;
    public readonly int Depth;

    public GraphTraversalVertex(Graph<ICallable>.Vertex vertex, bool local, int depth) {
      Vertex = vertex;
      Local = local;
      Depth = depth;
    }

    public override bool Equals(object obj) {
      var other = obj as GraphTraversalVertex;
      return other?.Vertex.N.FullSanitizedName == Vertex.N.FullSanitizedName;
    }

    public override int GetHashCode() {
      return HashCode.Combine(Vertex.N.FullSanitizedName);
    }
  }

  private static string RenderRevealStmts(List<RevealStmtWithDepth> addedRevealStmtList, int depth = 1) {
    Contract.Requires(addedRevealStmtList.Any());

    var currentDepth = depth - 1;

    string result = "";

    foreach (var revealStmt in addedRevealStmtList.Where(stmt => stmt.Depth >= depth)) {
      if (revealStmt.Depth > currentDepth) {
        currentDepth = revealStmt.Depth;
        var stmtCount = addedRevealStmtList.Count(stmt => stmt.Depth == currentDepth);
        result += $"\n\n// depth {currentDepth}: {stmtCount} {(stmtCount == 1 ? "stmt" : "stmts")}\n";
      }

      result += $"{revealStmt.RevealStmt} ";


    }
    // Removing the initial `\n\n`
    return result[2..];
  }

  public static string GenerateMessage(List<RevealStmtWithDepth> addedReveals, int autoRevealDepth = Int32.MaxValue) {
    var numInsertedReveals = addedReveals.Count(stmt => stmt.Depth <= autoRevealDepth);
    var message = "";

    message +=
      $"Found {addedReveals.Count} function {(addedReveals.Count == 1 ? "dependency" : "dependencies")}.";

    message +=
      $" {numInsertedReveals} reveal {(numInsertedReveals == 1 ? "statement" : "statements")} inserted implicitly.";

    // if (numInsertedReveals < addedReveals.Count) {
    //   message += $" Remaining:\n{RenderRevealStmts(addedReveals, 1 + autoRevealDepth)}";
    // } else {
    message += $" Reveal statements:\n{RenderRevealStmts(addedReveals)}";
    // }

    return message;
  }

  public class RevealStmtWithDepth {
    public RevealStmtWithDepth(HideRevealStmt RevealStmt, int Depth) {
      this.RevealStmt = RevealStmt;
      this.Depth = Depth;
    }

    public override bool Equals(object obj) {
      var item = obj as RevealStmtWithDepth;
      return item != null && this.RevealStmt.ToString().Equals(item.RevealStmt.ToString());
    }

    public override int GetHashCode() {
      return HashCode.Combine(RevealStmt.ToString());
    }

    public HideRevealStmt RevealStmt { get; }
    public int Depth { get; }
  };

  public record FunctionWithDepth(Function Function, int Depth);

  public IEnumerable<FunctionWithDepth> GetEnumerator(ICallable m, TopLevelDecl currentClass, IEnumerable<Expression> subexpressions, ModuleDefinition rootModule = null) {
    var origVertex = currentClass.EnclosingModuleDefinition.CallGraph.FindVertex(m);
    var interModuleVertex = currentClass.EnclosingModuleDefinition.InterModuleCallGraph.FindVertex(m);

    if (origVertex is null) {
      yield break;
    }  //vertex can be null if m is a Test method.

    var queue = new Queue<GraphTraversalVertex>();

    var defaultRootModule = rootModule is null ? currentClass.EnclosingModuleDefinition : rootModule;

    // The rootModule parameter is used in the case when GetEnumerator is called, not on a function from a class, but on subset type expressions.
    // Here this function may be called with a callable that is in a different module than the original one.

    foreach (var callable in origVertex.Successors) {
      queue.Enqueue(new GraphTraversalVertex(callable, true, 1));
    }

    var typeList = ExprListToTypeList(subexpressions.ToList()).ToList();

    if (m is Function f) {
      typeList.Add(f.ResultType);
    }

    typeList.ForEach(newType => {
      if (newType is Type { AsSubsetType: not null }) {
        foreach (var subexpression in newType.AsSubsetType.Constraint.SubExpressions) {
          if (subexpression is FunctionCallExpr funcExpr) {
            var func = funcExpr.Function;

            if (IsRevealable(defaultRootModule.AccessibleMembers, func)) {
              var newVertex = func.EnclosingClass.EnclosingModuleDefinition.CallGraph.FindVertex(func);

              if (newVertex is not null) {
                queue.Enqueue(new GraphTraversalVertex(newVertex, false, 1));
              }
            }
          }
        }
        foreach (var dependency in InvariantDependencies(newType.AsSubsetType, defaultRootModule, 1)) {
          queue.Enqueue(dependency);
        }
      }
    });

    if (m is Constructor) {
      // Issue 105: a constructor forms a value of the type of its class, as a function forms one of its result type
      foreach (var dependency in InvariantDependencies(currentClass, defaultRootModule, 1)) {
        queue.Enqueue(dependency);
      }
    }

    if (interModuleVertex is not null) {
      foreach (var callable in interModuleVertex.Successors) {

        if (IsRevealable(defaultRootModule.AccessibleMembers, (Declaration)callable.N)) {

          queue.Enqueue(new GraphTraversalVertex(callable, false, 1));
        }
      }
    }

    foreach (var functionWithDepth in Reachable(queue, defaultRootModule)) {
      yield return functionWithDepth;
    }
  }

  /// <summary>
  /// The functions made implicitly opaque that the traversal of the call graphs reaches from the vertices in "queue",
  /// breadth first, each with its depth.
  /// </summary>
  private IEnumerable<FunctionWithDepth> Reachable(Queue<GraphTraversalVertex> queue, ModuleDefinition defaultRootModule) {
    var visited = new HashSet<GraphTraversalVertex>();

    while (queue.Any()) {
      var vertex = queue.Dequeue();
      if (!visited.Contains(vertex)) {
        visited.Add(vertex);

        Graph<ICallable>.Vertex graphVertex;
        Graph<ICallable>.Vertex interModuleGraphVertex;

        if (vertex.Local) {
          graphVertex = vertex.Vertex;

        } else {
          graphVertex = vertex.Vertex.N.EnclosingModule.CallGraph.FindVertex(vertex.Vertex.N);

          if (graphVertex is null) {
            continue;
          }
        }

        interModuleGraphVertex = graphVertex.N.EnclosingModule.InterModuleCallGraph.FindVertex(graphVertex.N);

        foreach (var vertex0 in graphVertex.Successors) {
          if (IsRevealable(defaultRootModule.AccessibleMembers, (Declaration)vertex0.N)) {
            var newGraphTraversalVertex =
              new GraphTraversalVertex(vertex0, true, 1 + vertex.Depth);

            if (!visited.Contains(newGraphTraversalVertex)) {
              queue.Enqueue(newGraphTraversalVertex);
            }
          }
        }

        if (interModuleGraphVertex is not null) {
          foreach (var vertex0 in interModuleGraphVertex.Successors) {
            if (IsRevealable(defaultRootModule.AccessibleMembers, (Declaration)vertex0.N)) {
              queue.Enqueue(new GraphTraversalVertex(vertex0, false, 1 + vertex.Depth));
            }
          }
        }

        foreach (var dependency in InvariantDependencies(graphVertex.N, defaultRootModule, 1 + vertex.Depth)) {
          if (!visited.Contains(dependency)) {
            queue.Enqueue(dependency);
          }
        }

        var callable = graphVertex.N;

        if (callable is Function func && func.IsMadeImplicitlyOpaque(Options)) {
          yield return new FunctionWithDepth(func, vertex.Depth);
        }
      }
    }
  }

  /// <summary>
  /// Issue 105 (--member-invariants): the type with member invariants that "decl" stands for, or null. That is the
  /// InvariantTypeDecl itself; that of the declaration that owns one, since the call graph records a use of the type
  /// with the invariants as a use of its owner (see CallGraphBuilder.AddTypeDependencyEdges); or that of the class or
  /// trait whose non-null type "decl" is. The call graph records a use of "raw D" as a use of D's declaration too, so
  /// a use of "raw D" or "raw C" reveals what the invariants call as well: a reveal only adds what holds.
  /// </summary>
  private static InvariantTypeDecl InvariantTypeOf(object decl) {
    return decl switch {
      InvariantTypeDecl invariantTypeDecl => invariantTypeDecl,
      NonNullTypeDecl nonNullTypeDecl => nonNullTypeDecl.Class.InvariantTypeDecl,
      TopLevelDeclWithMembers owner => owner.InvariantTypeDecl,
      _ => null
    };
  }

  /// <summary>
  /// Issue 105 (--member-invariants): a callable that depends on a type with member invariants depends on what its
  /// invariants call, as one that depends on a subset type depends on what the constraint calls. These are the callees
  /// of the invariants, own and inherited, in the call graphs, and the functions that the constraint of a newtype's
  /// header calls, which is a conjunct of the invariants (see InvariantTypeDecl.HeaderConjunct). An invariant is never
  /// opaque, so it is not revealed itself, and its callees have the depth that the type has.
  /// </summary>
  private IEnumerable<GraphTraversalVertex> InvariantDependencies(object decl, ModuleDefinition rootModule, int depth) {
    var invariantTypeDecl = InvariantTypeOf(decl);
    if (invariantTypeDecl is null) {
      yield break;
    }

    var invariants = invariantTypeDecl.InheritedInvariants.Select(inherited => inherited.Invariant)
      .Concat(InvariantTypeDecl.Invariants(invariantTypeDecl.Owner));
    foreach (var invariant in invariants) {
      var module = invariant.EnclosingClass.EnclosingModuleDefinition;
      var vertex = module.CallGraph.FindVertex(invariant);
      if (vertex is not null) {
        foreach (var callee in vertex.Successors) {
          if (IsRevealable(rootModule.AccessibleMembers, (Declaration)callee.N)) {
            yield return new GraphTraversalVertex(callee, true, depth);
          }
        }
      }

      var interModuleVertex = module.InterModuleCallGraph.FindVertex(invariant);
      if (interModuleVertex is not null) {
        foreach (var callee in interModuleVertex.Successors) {
          if (IsRevealable(rootModule.AccessibleMembers, (Declaration)callee.N)) {
            yield return new GraphTraversalVertex(callee, false, depth);
          }
        }
      }
    }

    if (invariantTypeDecl.HeaderConjunct is not null) {
      foreach (var expr in Triggers.ExprExtensions.AllSubExpressions(invariantTypeDecl.HeaderConjunct, false, false)) {
        if (expr is FunctionCallExpr { Function: var func } && IsRevealable(rootModule.AccessibleMembers, func)) {
          var vertex = func.EnclosingClass.EnclosingModuleDefinition.CallGraph.FindVertex(func);
          if (vertex is not null) {
            yield return new GraphTraversalVertex(vertex, false, depth);
          }
        }
      }
    }
  }

  /// <summary>
  /// Issue 105 (--member-invariants): the reveal statements that the witness of the type with member invariants
  /// "invariantTypeDecl" needs, as the witness clause of a subset type needs those of the functions its constraint
  /// calls (see SubsetTypeDecl.AutoRevealDependencies).
  /// </summary>
  public IEnumerable<RevealStmtWithDepth> InvariantFunctionalDependencies(InvariantTypeDecl invariantTypeDecl, IOrigin tok,
    ModuleDefinition rootModule) {
    var queue = new Queue<GraphTraversalVertex>(InvariantDependencies(invariantTypeDecl, rootModule, 1));
    foreach (var functionWithDepth in Reachable(queue, rootModule)) {
      var revealStmt = BuildRevealStmt(functionWithDepth.Function, tok, rootModule);
      if (revealStmt is not null) {
        yield return new RevealStmtWithDepth(revealStmt, functionWithDepth.Depth);
      }
    }
  }

  private static IEnumerable<Type> ExprListToTypeList(IEnumerable<Expression> exprList) {
    if (exprList is null || !exprList.Any()) {
      return new List<Type>();
    }

    var subExprList = exprList.SelectMany(expr => Triggers.ExprExtensions.AllSubExpressions(expr, false, false));

    var typeList = subExprList.Select(expr => expr.Type);

    return typeList;
  }

  public IEnumerable<RevealStmtWithDepth> ExprToFunctionalDependencies(Expression expr, ModuleDefinition enclosingModule) {
    var expressionList = Triggers.ExprExtensions.AllSubExpressions(expr, false, false);
    var revealStmtList = new List<RevealStmtWithDepth>();

    foreach (var expression in expressionList) {
      if (expression is FunctionCallExpr funcExpr) {
        var func = funcExpr.Function;

        if (IsRevealable(enclosingModule.AccessibleMembers, func)) {
          if (func.IsMadeImplicitlyOpaque(Options)) {

            var revealStmt0 = BuildRevealStmt(func,
              expr.Origin, enclosingModule);

            if (revealStmt0 is not null) {
              revealStmtList.Add(new RevealStmtWithDepth(revealStmt0, 1));
            }
          }

          foreach (var newFunc in GetEnumerator(func, func.EnclosingClass, new List<Expression> { expr },
                     enclosingModule)) {

            var revealStmt1 = BuildRevealStmt(newFunc.Function, expr.Origin, enclosingModule);

            if (revealStmt1 is not null) {
              revealStmtList.Add(new RevealStmtWithDepth(revealStmt1, newFunc.Depth));
            }
          }
        }
      }
    }

    revealStmtList = revealStmtList.Distinct().ToList();
    return revealStmtList;
  }

  public Expression AddRevealStmtsToExpression(Expression expr, IEnumerable<RevealStmtWithDepth> revealStmtList) {
    var finalExpr = expr;

    foreach (var revealStmt in revealStmtList) {
      var oldExpr = finalExpr;

      finalExpr = new StmtExpr(expr.Origin, revealStmt.RevealStmt, oldExpr) {
        Type = oldExpr.Type
      };
    }

    return finalExpr;
  }

  public static HideRevealStmt BuildRevealStmt(Function func, IOrigin tok, ModuleDefinition rootModule) {
    List<Type> args = [];
    foreach (var _ in func.EnclosingClass.TypeArgs) {
      args.Add(new IntType());
    }

    AccessibleMember accessibleMember = rootModule.AccessibleMembers[func];

    var resolveExpr = ConstructExpressionFromPath(func, accessibleMember);

    var callableClass = ((TopLevelDeclWithMembers)func.EnclosingClass);

    var callableName = HideRevealStmt.RevealLemmaPrefix + func.Name;
    var member = callableClass.Members.Find(decl => decl.Name == callableName);

    Type.PushScope(callableClass.EnclosingModuleDefinition.VisibilityScope);
    var receiver = new StaticReceiverExpr(tok, new UserDefinedType(tok, callableName, callableClass, callableClass.TypeArgs.ConvertAll(obj => (Type)Type.Int)), callableClass, true);
    Type.PopScope(callableClass.EnclosingModuleDefinition.VisibilityScope);

    if (member is null) {
      return null;
    }

    callableName = ((ICallable)member).NameRelativeToModule;
    var rr = new MemberSelectExpr(func.Origin, receiver, new Name(callableName));
    rr.Type = new InferredTypeProxy();
    rr.Member = member;
    rr.TypeApplicationJustMember = [];
    rr.TypeApplicationAtEnclosingClass = args;

    var call = new CallStmt(func.Origin, [], rr, (List<Expression>)[],
      func.ReportingRange);
    call.IsGhost = true;
    call.Bindings.AcceptArgumentExpressionsAsExactParameterList([]);

    resolveExpr.Type = new InferredTypeProxy();
    ((ConcreteSyntaxExpression)resolveExpr).ResolvedExpression = rr;

    List<Expression> expressionList = [
      new ApplySuffix(tok, null,
        resolveExpr,
        [], Token.NoToken)
    ];

    var revealStmt = new HideRevealStmt(func.Origin, expressionList, HideRevealCmd.Modes.Reveal);
    revealStmt.ResolvedStatements.Add(call);
    revealStmt.IsGhost = true;

    return revealStmt;
  }

  private static Expression ConstructExpressionFromPath(Function func, AccessibleMember accessibleMember) {

    var topLevelDeclsList = accessibleMember.AccessPath;
    var nameList = topLevelDeclsList.Where(decl => decl.Name != "_default").ToList();

    nameList.Add(new NameSegment(func.Origin, func.Name, []));

    Expression nameSeed = nameList[0];
    var resolveExpr = nameList.Skip(1)
    .Aggregate(nameSeed, (acc, name) => new ExprDotName(func.Origin, acc, name.NameNode, name.OptTypeArguments));

    return resolveExpr;
  }

  public static bool IsRevealable(Dictionary<Declaration, AccessibleMember> accessibleMembers,
    Declaration decl) {
    if (accessibleMembers.TryGetValue(decl, out var member)) {
      return member.IsRevealed;
    }

    return false;
  }
}