//-----------------------------------------------------------------------------
//
// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
//
//-----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;

namespace Microsoft.Dafny;

/// <summary>
/// The purpose of the TypeRefinementVisitor is to incorporate subset types into the types that were inferred during
/// pre-type inference. The visitor collects constraints, which are solved by the Solve() method.
/// </summary>
public class TypeRefinementVisitor : ASTVisitor<IASTVisitorContext> {
  private string moduleDescription;
  public override IASTVisitorContext GetContext(IASTVisitorContext astVisitorContext, bool inFunctionPostcondition) {
    return astVisitorContext;
  }

  private readonly SystemModuleManager systemModuleManager;

  /// <summary>
  /// Issue 105: non-null when the declarations include a type with member invariants.
  /// </summary>
  private readonly RawMemberInference rawMemberInference;

  public TypeRefinementVisitor(string moduleDescription, SystemModuleManager systemModuleManager,
    RawMemberInference rawMemberInference = null) {
    this.moduleDescription = moduleDescription;
    this.systemModuleManager = systemModuleManager;
    this.rawMemberInference = rawMemberInference;
  }

  private readonly List<Flow> flows = [];

  /// <summary>
  /// Issue 105: the application of "member" to "receiver" in "context" may make "member" a raw member.
  /// </summary>
  private void AddRawMemberFlow(IASTVisitorContext context, Expression receiver, MemberDecl member, IOrigin tok) {
    if (rawMemberInference?.ApplicationFlow(context, receiver, member, tok) is { } flow) {
      flows.Add(flow);
    }
  }

  public void DebugPrint() {
    var sw = new StringWriter();
    sw.WriteLine($"--------------------------- type-refinement flows, {moduleDescription}:");
    foreach (var flow in flows) {
      flow.DebugPrint(sw);
    }
    sw.WriteLine($"------------------- (end of type-refinement flows, {moduleDescription})");
    systemModuleManager.Options.OutputWriter.Debug(sw.ToString());
  }

  public override void VisitField(Field field) {
    base.VisitField(field);
    if (field is ConstantField { Rhs: { } rhs } constantField) {
      flows.Add(new FlowIntoVariable(constantField, rhs, field.Origin, ":="));
    }
  }

  protected override bool VisitOneExpression(Expression expr, IASTVisitorContext context) {
    if (expr is DatatypeUpdateExpr datatypeUpdateExpr) {
      // How a DatatypeUpdateExpr desugars depends on whether or not the expression is ghost, which hasn't been determined
      // yet. So, if there is a difference between the two, then pre-type resolution prepares two different resolved expressions.
      // The choice between these two is done in a later phase during resolution. For now, if there are two, we visit them both.
      // ASTVisitor arranges to visit ResolvedExpression, but we consider ResolvedCompiledExpression here.
      if (datatypeUpdateExpr.ResolvedCompiledExpression != datatypeUpdateExpr.ResolvedExpression) {
        VisitExpression(datatypeUpdateExpr.ResolvedCompiledExpression, context);
      }
    }
    return base.VisitOneExpression(expr, context);
  }

  /// <summary>
  /// Issue 105: the flows of an arithmetic expression of a newtype N with member invariants, a tree of operations and
  /// literals of N (see PreTypeToTypeVisitor.IsNewtypeArithmetic). The type of an operation is the join of the types
  /// of its operands of N, as for any operation (below), and that type also flows down into the operands that are
  /// literals or operations, never into a variable. So every node of the tree has the join of its other leaves: "raw N"
  /// if one of them is raw, and N otherwise, which the verifier checks at each node, as it checks the constraint of
  /// a newtype in the old syntax. A comparison has no result of N, so each of its operands flows into the other, if
  /// that is a literal or an operation: in "r < 3", where "r" is raw, so is "3".
  /// </summary>
  private void AddNewtypeArithmeticFlows(Expression expr) {
    void FlowDown(Expression source, Expression sink, string description) {
      if (PreTypeToTypeVisitor.IsNewtypeArithmetic(sink.Resolved) && SamePreTypeDecl(source, sink.Resolved)) {
        flows.Add(new FlowBetweenExpressions(sink.Resolved, source, description));
      }
    }
    switch (expr) {
      case BinaryExpr binaryExpr when PreTypeToTypeVisitor.IsNewtypeArithmetic(expr):
        FlowDown(expr, binaryExpr.E0, BinaryExpr.OpcodeString(binaryExpr.Op));
        if (binaryExpr.ResolvedOp is not (BinaryExpr.ResolvedOpcode.LeftShift or BinaryExpr.ResolvedOpcode.RightShift)) {
          FlowDown(expr, binaryExpr.E1, BinaryExpr.OpcodeString(binaryExpr.Op));
        }
        break;
      case UnaryOpExpr unaryOpExpr when PreTypeToTypeVisitor.IsNewtypeArithmetic(expr) && SamePreTypeDecl(expr, unaryOpExpr.E):
        flows.Add(new FlowBetweenExpressions(expr, unaryOpExpr.E, "unary " + unaryOpExpr.Op));
        FlowDown(expr, unaryOpExpr.E, "unary " + unaryOpExpr.Op);
        break;
      case BinaryExpr {
        ResolvedOp: BinaryExpr.ResolvedOpcode.EqCommon or BinaryExpr.ResolvedOpcode.NeqCommon or BinaryExpr.ResolvedOpcode.Lt or
        BinaryExpr.ResolvedOpcode.Le or BinaryExpr.ResolvedOpcode.Ge or BinaryExpr.ResolvedOpcode.Gt
      } comparison:
        FlowDown(comparison.E0, comparison.E1, BinaryExpr.OpcodeString(comparison.Op));
        FlowDown(comparison.E1, comparison.E0, BinaryExpr.OpcodeString(comparison.Op));
        break;
    }
  }

  private static bool SamePreTypeDecl(Expression a, Expression b) {
    return a.PreType?.Normalize() is DPreType { Decl: var declA } && b.PreType?.Normalize() is DPreType { Decl: var declB } &&
           declA == declB;
  }

  protected override void PostVisitOneExpression(Expression expr, IASTVisitorContext context) {
    AddNewtypeArithmeticFlows(expr);
    if (expr is IdentifierExpr identifierExpr) {
      flows.Add(new FlowFromType(expr, identifierExpr.Var.UnnormalizedType, identifierExpr.Name));

    } else if (expr is SeqSelectExpr selectExpr) {
      var unnormalizedSeqType = selectExpr.Seq.UnnormalizedType;
      var seqType = selectExpr.Seq.Type.NormalizeToAncestorType();
      if (!selectExpr.SelectOne) {
        var sinkType = selectExpr.Type.NormalizeToAncestorType().AsSeqType;
        flows.Add(new FlowFromTypeArgument(sinkType.Arg, unnormalizedSeqType, 0, expr.Origin));
      } else if (seqType.AsSeqType != null || seqType.IsArrayType) {
        flows.Add(new FlowFromTypeArgument(expr, unnormalizedSeqType, 0));
      } else if (seqType.IsMapType || seqType.IsIMapType) {
        flows.Add(new FlowFromTypeArgument(expr, unnormalizedSeqType, 1));
      } else {
        Contract.Assert(seqType.AsMultiSetType != null);
        // type is fixed, so no flow to set up
      }

    } else if (expr is MultiSelectExpr multiSelectExpr) {
      flows.Add(new FlowFromTypeArgument(expr, multiSelectExpr.Array.UnnormalizedType, 0));

    } else if (expr is ITEExpr iteExpr) {
      flows.Add(new FlowBetweenExpressions(expr, iteExpr.Thn, "if-then"));
      flows.Add(new FlowBetweenExpressions(expr, iteExpr.Els, "if-else"));

    } else if (expr is NestedMatchExpr matchExpr) {
      foreach (var kase in matchExpr.Cases) {
        VisitMatchCasePattern(kase.Pat, () => TypeRefinementWrapper.NormalizeSansBottom(matchExpr.Source));
        flows.Add(new FlowBetweenExpressions(expr, kase.Body, "case"));
      }

    } else if (expr is MemberSelectExpr memberSelectExpr) {
      AddRawMemberFlow(context, memberSelectExpr.Obj, memberSelectExpr.Member, expr.Origin);
      if (memberSelectExpr.Member is Field field) {
        var dPreType = (DPreType)memberSelectExpr.Obj.PreType.Normalize();
        if (dPreType.Decl is ValuetypeDecl valuetypeDecl) {
          flows.Add(new FlowFromComputedType(expr, () => {
            // TypeArgumentSubstitutionsWithParents doesn't work with ValuetypeDecl, so we roll our own type map
            var receiverType = memberSelectExpr.Obj.Type.NormalizeExpand();
            Contract.Assert(receiverType.TypeArgs.Count == valuetypeDecl.TypeArgs.Count);
            var typeMap = TypeParameter.SubstitutionMap(valuetypeDecl.TypeArgs, receiverType.TypeArgs);
            return field.Type.Subst(typeMap);
          }, $".{memberSelectExpr.MemberName}"));
        } else {
          flows.Add(new FlowFromComputedType(expr, () => {
            var typeMap = memberSelectExpr.TypeArgumentSubstitutionsWithParents();
            return field.Type.Subst(typeMap);
          }, $".{memberSelectExpr.MemberName}"));
        }
      } else if (memberSelectExpr.Member is Function function) {
        flows.Add(new FlowFromComputedType(expr, () => {
          var typeMap = memberSelectExpr.TypeArgumentSubstitutionsWithParents();
          return ModuleResolver.SelectAppropriateArrowTypeForFunction(function, typeMap, systemModuleManager);
        }, $".{memberSelectExpr.MemberName}"));
      }

    } else if (expr is FunctionCallExpr functionCallExpr) {
      AddRawMemberFlow(context, functionCallExpr.Receiver, functionCallExpr.Function, expr.Origin);
      Contract.Assert(functionCallExpr.Args.Count == functionCallExpr.Function.Ins.Count);
      for (var i = 0; i < functionCallExpr.Args.Count; i++) {
        var formal = functionCallExpr.Function.Ins[i];
        var actual = functionCallExpr.Args[i];
        flows.Add(new FlowBetweenComputedTypes(() => {
          var typeMap = functionCallExpr.TypeArgumentSubstitutionsWithParents();
          return (TypeRefinementWrapper.NormalizeSansBottom(formal).Subst(typeMap), TypeRefinementWrapper.NormalizeSansBottom(actual));
        }, functionCallExpr.Origin, $"{functionCallExpr.Function.Name}({formal.Name} := ...)"));
      }

      flows.Add(new FlowFromComputedType(expr, () => {
        var typeMap = functionCallExpr.TypeArgumentSubstitutionsWithParents();
        return functionCallExpr.Function.ResultType.Subst(typeMap);
      }, $"{functionCallExpr.Name}(...)"));

    } else if (expr is DatatypeValue datatypeValue) {
      var ctor = datatypeValue.Ctor;
      var datatypeDecl = ctor.EnclosingDatatype;
      for (var i = 0; i < datatypeValue.Arguments.Count; i++) {
        var formal = ctor.Formals[i];
        var actual = datatypeValue.Arguments[i];
        flows.Add(new FlowBetweenComputedTypes(() => {
          var typeMap = TypeParameter.SubstitutionMap(datatypeDecl.TypeArgs, datatypeValue.InferredTypeArgs);
          return (TypeRefinementWrapper.NormalizeSansBottom(formal).Subst(typeMap), TypeRefinementWrapper.NormalizeSansBottom(actual));
        }, datatypeValue.Origin, $"{ctor.Name}({formal.Name} := ...)", isConstructorArgument: true));
      }
      flows.Add(new FlowFromComputedType(expr,
        () => new UserDefinedType(expr.Origin, datatypeDecl.Name, datatypeDecl, datatypeValue.InferredTypeArgs),
        ctor.Name));

    } else if (expr is ConversionExpr { IsBaseOperation: true } conversionExpr) {
      Type BaseOperationType() {
        var expected = ((DPreType)conversionExpr.PreType.Normalize()).Decl;
        var view = NewtypeOperationView.Get(conversionExpr.E.Type, expected, preserveSubsetTypes: true);
        if (expected is ArrowTypeDecl &&
            (view.Status != NewtypeOperationView.ViewStatus.Resolved || view.BaseType?.AsArrowType?.ResolvedClass != expected)) {
          // A hidden or unresolved carrier cannot replace a resolved arrow signature.
          // Preserve any total/partial subset on a successful projection below.
          return TypeRefinementWrapper.NormalizeSansBottom(expr);
        }
        // Keep the current lower bound until the source view is determined.
        // Successful resolution supplies the concrete, instantiated visible base.
        return view.BaseType ?? TypeRefinementWrapper.NormalizeSansBottom(expr);
      }
      if (((DPreType)conversionExpr.PreType.Normalize()).Decl is ArrowTypeDecl) {
        // An operation view has the source's exact base signature. A subtype join
        // here can broaden a contravariant domain and create an invalid totality demand.
        flows.Add(new FlowFromComputedArrowOperationType(conversionExpr, BaseOperationType));
      } else {
        flows.Add(new FlowFromComputedType(expr, BaseOperationType, "base operation"));
      }

    } else if (expr is ApplyExpr applyExpr) {
      flows.Add(new FlowFromTypeArgument(expr, applyExpr.Function.UnnormalizedType, applyExpr.Args.Count));

    } else if (expr is SetDisplayExpr setDisplayExpr) {
      foreach (var element in setDisplayExpr.Elements) {
        flows.Add(new FlowFromComputedTypeIgnoreHeadTypes(expr, () => new SetType(setDisplayExpr.Finite, TypeRefinementWrapper.NormalizeSansBottom(element)), "set display"));
      }

    } else if (expr is MultiSetDisplayExpr multiSetDisplayExpr) {
      foreach (var element in multiSetDisplayExpr.Elements) {
        flows.Add(new FlowFromComputedTypeIgnoreHeadTypes(expr, () => new MultiSetType(TypeRefinementWrapper.NormalizeSansBottom(element)), "multiset display"));
      }

    } else if (expr is SeqDisplayExpr seqDisplayExpr) {
      foreach (var element in seqDisplayExpr.Elements) {
        flows.Add(new FlowFromComputedTypeIgnoreHeadTypes(expr, () => new SeqType(TypeRefinementWrapper.NormalizeSansBottom(element)), "sequence display"));
      }

    } else if (expr is MapDisplayExpr mapDisplayExpr) {
      foreach (var element in mapDisplayExpr.Elements) {
        flows.Add(new FlowFromComputedTypeIgnoreHeadTypes(expr, () => new MapType(mapDisplayExpr.Finite,
            TypeRefinementWrapper.NormalizeSansBottom(element.A), TypeRefinementWrapper.NormalizeSansBottom(element.B)),
          "map display"));
      }

    } else if (expr is SeqUpdateExpr seqUpdateExpr) {
      if (expr.Type is MultiSetType multiSetType) {
        flows.Add(new FlowBetweenExpressions(expr, seqUpdateExpr.Seq, "multiset update (source)"));
        flows.Add(new FlowFromComputedTypeIgnoreHeadTypes(expr,
          () => new MultiSetType(TypeRefinementWrapper.NormalizeSansBottom(seqUpdateExpr.Index)),
          "multiset update (element)"));
      } else if (expr.Type is MapType mapType) {
        flows.Add(new FlowBetweenExpressions(expr, seqUpdateExpr.Seq, "map update (source)"));
        flows.Add(new FlowFromComputedTypeIgnoreHeadTypes(expr, () => new MapType(mapType.Finite,
            TypeRefinementWrapper.NormalizeSansBottom(seqUpdateExpr.Index), TypeRefinementWrapper.NormalizeSansBottom(seqUpdateExpr.Value)),
          "map update (element)"));
      } else {
        // nothing to do for sequences
      }

    } else if (expr is SetComprehension setComprehension) {
      flows.Add(new FlowFromComputedTypeIgnoreHeadTypes(expr,
        () => new SetType(setComprehension.Finite, TypeRefinementWrapper.NormalizeSansBottom(setComprehension.Term)),
        "set comprehension"));

    } else if (expr is MapComprehension mapComprehension) {
      flows.Add(new FlowFromComputedTypeIgnoreHeadTypes(expr, () => {
        Type keyType;
        if (mapComprehension.TermLeft != null) {
          keyType = TypeRefinementWrapper.NormalizeSansBottom(mapComprehension.TermLeft);
        } else {
          Contract.Assert(mapComprehension.BoundVars.Count == 1);
          keyType = TypeRefinementWrapper.NormalizeSansBottom(mapComprehension.BoundVars[0]);
        }
        return new MapType(mapComprehension.Finite, keyType, TypeRefinementWrapper.NormalizeSansBottom(mapComprehension.Term));
      }, "map comprehension"));

    } else if (expr is SeqConstructionExpr seqConstructionExpr) {
      flows.Add(new FlowFromComputedTypeIgnoreHeadTypes(expr,
        () => {
          var arrowType = (ArrowType)seqConstructionExpr.Initializer.Type.NormalizeToAncestorType();
          Contract.Assert(arrowType.TypeArgs.Count == 2);
          return new SeqType(TypeRefinementWrapper.NormalizeSansBottom(arrowType.TypeArgs[1]));
        },
        "seq constructor"));

    } else if (expr is BinaryExpr binaryExpr) {
      switch (binaryExpr.ResolvedOp) {
        case BinaryExpr.ResolvedOpcode.Iff:
        case BinaryExpr.ResolvedOpcode.Imp:
        case BinaryExpr.ResolvedOpcode.And:
        case BinaryExpr.ResolvedOpcode.Or:
        case BinaryExpr.ResolvedOpcode.Add:
        case BinaryExpr.ResolvedOpcode.Sub:
        case BinaryExpr.ResolvedOpcode.Mul:
        case BinaryExpr.ResolvedOpcode.Div:
        case BinaryExpr.ResolvedOpcode.Mod:
        case BinaryExpr.ResolvedOpcode.BitwiseAnd:
        case BinaryExpr.ResolvedOpcode.BitwiseOr:
        case BinaryExpr.ResolvedOpcode.BitwiseXor:
        case BinaryExpr.ResolvedOpcode.Union:
        case BinaryExpr.ResolvedOpcode.Intersection:
        case BinaryExpr.ResolvedOpcode.MultiSetUnion:
        case BinaryExpr.ResolvedOpcode.MultiSetIntersection:
        case BinaryExpr.ResolvedOpcode.Concat:
        case BinaryExpr.ResolvedOpcode.MapMerge:
          // For these operators, the result type is the same as that of E0 and E1.
          //
          // Note about intersection: In general, let set<C> be the result of combining the operands set<A> and set<B>
          // of intersection. To be precise, we would need C to be a type that conjoins the constraints of A and B.
          // We don't have such a time, so we instead (approximate the other direction and) let C be the join of A and B.
          flows.Add(new FlowBetweenExpressions(expr, binaryExpr.E0, BinaryExpr.OpcodeString(binaryExpr.Op)));
          flows.Add(new FlowBetweenExpressions(expr, binaryExpr.E1, BinaryExpr.OpcodeString(binaryExpr.Op)));
          break;
        case BinaryExpr.ResolvedOpcode.LeftShift:
        case BinaryExpr.ResolvedOpcode.RightShift:
        case BinaryExpr.ResolvedOpcode.SetDifference:
        case BinaryExpr.ResolvedOpcode.MultiSetDifference:
        case BinaryExpr.ResolvedOpcode.MapSubtraction:
          // For these operators, the result type is determined by E0.
          flows.Add(new FlowBetweenExpressions(expr, binaryExpr.E0, BinaryExpr.OpcodeString(binaryExpr.Op)));
          break;
        case BinaryExpr.ResolvedOpcode.EqCommon:
        case BinaryExpr.ResolvedOpcode.NeqCommon:
        case BinaryExpr.ResolvedOpcode.Lt:
        case BinaryExpr.ResolvedOpcode.Le:
        case BinaryExpr.ResolvedOpcode.Ge:
        case BinaryExpr.ResolvedOpcode.Gt:
        case BinaryExpr.ResolvedOpcode.LtChar:
        case BinaryExpr.ResolvedOpcode.LeChar:
        case BinaryExpr.ResolvedOpcode.GeChar:
        case BinaryExpr.ResolvedOpcode.GtChar:
        case BinaryExpr.ResolvedOpcode.SetEq:
        case BinaryExpr.ResolvedOpcode.SetNeq:
        case BinaryExpr.ResolvedOpcode.ProperSubset:
        case BinaryExpr.ResolvedOpcode.Subset:
        case BinaryExpr.ResolvedOpcode.Superset:
        case BinaryExpr.ResolvedOpcode.ProperSuperset:
        case BinaryExpr.ResolvedOpcode.Disjoint:
        case BinaryExpr.ResolvedOpcode.InSet:
        case BinaryExpr.ResolvedOpcode.NotInSet:
        case BinaryExpr.ResolvedOpcode.MultiSetEq:
        case BinaryExpr.ResolvedOpcode.MultiSetNeq:
        case BinaryExpr.ResolvedOpcode.MultiSubset:
        case BinaryExpr.ResolvedOpcode.MultiSuperset:
        case BinaryExpr.ResolvedOpcode.ProperMultiSubset:
        case BinaryExpr.ResolvedOpcode.ProperMultiSuperset:
        case BinaryExpr.ResolvedOpcode.MultiSetDisjoint:
        case BinaryExpr.ResolvedOpcode.InMultiSet:
        case BinaryExpr.ResolvedOpcode.NotInMultiSet:
        case BinaryExpr.ResolvedOpcode.SeqEq:
        case BinaryExpr.ResolvedOpcode.SeqNeq:
        case BinaryExpr.ResolvedOpcode.ProperPrefix:
        case BinaryExpr.ResolvedOpcode.Prefix:
        case BinaryExpr.ResolvedOpcode.InSeq:
        case BinaryExpr.ResolvedOpcode.NotInSeq:
        case BinaryExpr.ResolvedOpcode.MapEq:
        case BinaryExpr.ResolvedOpcode.MapNeq:
        case BinaryExpr.ResolvedOpcode.InMap:
        case BinaryExpr.ResolvedOpcode.NotInMap:
        case BinaryExpr.ResolvedOpcode.RankLt:
        case BinaryExpr.ResolvedOpcode.RankGt:
          // For these operators, the result type is fixed (to be bool), so there is no flow.
          break;
        case BinaryExpr.ResolvedOpcode.YetUndetermined:
        case BinaryExpr.ResolvedOpcode.LessThanLimit:
        default:
          Contract.Assert(false); // unexpected operator
          break;
      }

    } else if (expr is LetExpr letExpr) {
      if (letExpr.Exact) {
        Contract.Assert(letExpr.LHSs.Count == letExpr.RHSs.Count);
        for (var i = 0; i < letExpr.LHSs.Count; i++) {
          var rhs = letExpr.RHSs[i];
          VisitPattern(letExpr.LHSs[i], () => TypeRefinementWrapper.NormalizeSansBottom(rhs), context);
        }
      }
      flows.Add(new FlowBetweenExpressions(expr, letExpr.Body, "let"));

    } else if (expr is LambdaExpr lambdaExpr) {
      flows.Add(new FlowFromComputedType(expr, () => {
        return ModuleResolver.SelectAppropriateArrowType(lambdaExpr.Origin,
          lambdaExpr.BoundVars.ConvertAll(v => TypeRefinementWrapper.NormalizeSansBottom(v)),
          TypeRefinementWrapper.NormalizeSansBottom(lambdaExpr.Body),
          lambdaExpr.Reads.Expressions.Count != 0, lambdaExpr.Range != null, systemModuleManager);
      }, lambdaExpr.WhatKind));

    }

    base.PostVisitOneExpression(expr, context);
  }

  private void VisitPattern<VT>(CasePattern<VT> casePattern, Func<Type> getPatternRhsType, IASTVisitorContext context) where VT : class, IVariable {
    VisitExpression(casePattern.Expr, context);

    if (casePattern.Var != null) {
      flows.Add(new FlowIntoVariableFromComputedType(casePattern.Var, getPatternRhsType, casePattern.Origin, ":="));
      Contract.Assert(casePattern.Arguments == null);
    } else if (casePattern.Arguments != null) {
      var ctor = casePattern.Ctor;
      Contract.Assert(ctor != null);

      Func<Type> GetPatternArgumentType(int argumentIndex) {
        return () => {
          var nominalSourceType = getPatternRhsType();
          var sourceType = systemModuleManager.Options.Get(CommonOptionBag.ExtendedNewtypeBases)
            ? NewtypeOperationView.Get(nominalSourceType).BaseType : nominalSourceType.NormalizeExpand();
          Contract.Assert(sourceType.IsDatatype);
          Contract.Assert(sourceType.TypeArgs.Count == ctor.EnclosingDatatype.TypeArgs.Count);
          var typeMap = TypeParameter.SubstitutionMap(ctor.EnclosingDatatype.TypeArgs, sourceType.TypeArgs);
          Contract.Assert(argumentIndex < ctor.Formals.Count);
          return ctor.Formals[argumentIndex].Type.Subst(typeMap);
        };
      }

      for (var i = 0; i < casePattern.Arguments.Count; i++) {
        VisitPattern<VT>(casePattern.Arguments[i], GetPatternArgumentType(i), context);
      }
    }

  }

  /// <summary>
  /// Sets up the flows into the variables bound by a case pattern of a "match" expression or statement. A variable
  /// bound at the top of the pattern gets the type of the match source; a variable bound to a constructor argument gets
  /// the declared type of the constructor's formal, with the type arguments of the source substituted for those of the
  /// datatype, as for a let-pattern (see VisitPattern above).
  /// </summary>
  private void VisitMatchCasePattern(ExtendedPattern pattern, Func<Type> getPatternRhsType) {
    switch (pattern) {
      case DisjunctivePattern disjunctivePattern:
        foreach (var alternative in disjunctivePattern.Alternatives) {
          VisitMatchCasePattern(alternative, getPatternRhsType);
        }
        break;
      case IdPattern idPattern:
        if (idPattern.BoundVar != null) {
          flows.Add(new FlowIntoVariableFromComputedType(idPattern.BoundVar, getPatternRhsType, idPattern.Origin, "case"));
        }
        if (idPattern.Arguments != null && idPattern.Ctor != null) {
          var ctor = idPattern.Ctor;
          Contract.Assert(idPattern.Arguments.Count == ctor.Formals.Count);
          for (var i = 0; i < idPattern.Arguments.Count; i++) {
            var argumentIndex = i;
            VisitMatchCasePattern(idPattern.Arguments[i], () => {
              // As in VisitPattern: with --extended-newtype-bases the source may be a newtype
              // whose instantiated base is the constructor's datatype.
              var nominalSourceType = getPatternRhsType();
              var sourceType = systemModuleManager.Options.Get(CommonOptionBag.ExtendedNewtypeBases)
                ? NewtypeOperationView.Get(nominalSourceType).BaseType : nominalSourceType.NormalizeExpand();
              Contract.Assert(sourceType.IsDatatype);
              Contract.Assert(sourceType.TypeArgs.Count == ctor.EnclosingDatatype.TypeArgs.Count);
              var typeMap = TypeParameter.SubstitutionMap(ctor.EnclosingDatatype.TypeArgs, sourceType.TypeArgs);
              return ctor.Formals[argumentIndex].Type.Subst(typeMap);
            });
          }
        }
        break;
      default:
        break;
    }
  }

  protected override void PostVisitOneStatement(Statement stmt, IASTVisitorContext context) {
    if (stmt is VarDeclPattern varDeclPattern) {
      VisitPattern(varDeclPattern.LHS, () => TypeRefinementWrapper.NormalizeSansBottom(varDeclPattern.RHS), context);
    } else if (stmt is NestedMatchStmt nestedMatchStmt) {
      foreach (var kase in nestedMatchStmt.Cases) {
        VisitMatchCasePattern(kase.Pat, () => TypeRefinementWrapper.NormalizeSansBottom(nestedMatchStmt.Source));
      }
    } else if (stmt is SingleAssignStmt { Lhs: IdentifierExpr lhsIdentifierExpr } assignStmt) {
      if (assignStmt is { Rhs: ExprRhs exprRhs }) {
        flows.Add(new FlowIntoVariable(lhsIdentifierExpr.Var, exprRhs.Expr, assignStmt.Origin, $"{lhsIdentifierExpr.Var.Name} :="));
      } else if (assignStmt is { Rhs: TypeRhs tRhs }) {
        flows.Add(new FlowFromType(lhsIdentifierExpr.Var.UnnormalizedType, tRhs.Type, assignStmt.Origin, ":= new"));
      }

    } else if (stmt is AssignSuchThatStmt assignSuchThatStmt) {
      foreach (var lhs in assignSuchThatStmt.Lhss) {
        VisitExpression(lhs, context);
      }

    } else if (stmt is CallStmt callStmt) {
      AddRawMemberFlow(context, callStmt.MethodSelect.Obj, callStmt.Method, callStmt.Origin);
      Contract.Assert(callStmt.Args.Count == callStmt.Method.Ins.Count);
      for (var i = 0; i < callStmt.Args.Count; i++) {
        var formal = callStmt.Method.Ins[i];
        var actual = callStmt.Args[i];
        flows.Add(new FlowBetweenComputedTypes(() => {
          var typeMap = callStmt.MethodSelect.TypeArgumentSubstitutionsWithParents();
          return (TypeRefinementWrapper.NormalizeSansBottom(formal).Subst(typeMap), TypeRefinementWrapper.NormalizeSansBottom(actual));
        }, callStmt.Origin, $"{callStmt.Method.Name}({formal.Name} := ...)"));
      }

      Contract.Assert(callStmt.Lhs.Count == callStmt.Method.Outs.Count);
      for (var i = 0; i < callStmt.Lhs.Count; i++) {
        if (callStmt.Lhs[i] is IdentifierExpr actualIdentifierExpr) {
          var formal = callStmt.Method.Outs[i];
          flows.Add(new FlowIntoVariableFromComputedType(actualIdentifierExpr.Var, () => {
            var typeMap = callStmt.MethodSelect.TypeArgumentSubstitutionsWithParents();
            return TypeRefinementWrapper.NormalizeSansBottom(formal).Subst(typeMap);
          }, callStmt.Origin, $"{actualIdentifierExpr.Var.Name} := {callStmt.Method.Name}(...)"));
        }
      }

    } else if (stmt is ProduceStmt produceStmt) {
      if (produceStmt.HiddenUpdate != null) {
        VisitStatement(produceStmt.HiddenUpdate, context);
      }

    } else if (stmt is CalcStmt calcStmt) {
      // The expression in each line has been visited, but pairs of those lines are then put together to
      // form steps. These steps (are always boolean, and) need to be visited, too. Their subexpressions
      // have already been visited, so it suffices to call PostVisitOneExpression (instead of VisitExpression)
      // here.
      foreach (var step in calcStmt.Steps) {
        PostVisitOneExpression(step, context);
      }
      PostVisitOneExpression(calcStmt.Result, context);
    }

    base.PostVisitOneStatement(stmt, context);
  }

  protected override void VisitExtendedPattern(ExtendedPattern pattern, IASTVisitorContext context) {
    switch (pattern) {
      case DisjunctivePattern disjunctivePattern:
        break;
      case LitPattern litPattern:
        PostVisitOneExpression(litPattern.OptimisticallyDesugaredLit, context);
        break;
      case IdPattern idPattern:
        if (idPattern.BoundVar != null) {
#if SOON
          UpdateIfOmitted(idPattern.BoundVar.Type, idPattern.BoundVar.PreType);
#endif
        }
        if (idPattern.ResolvedLit != null) {
          PostVisitOneExpression(idPattern.ResolvedLit, context);
        }
        break;
      default:
        Contract.Assert(false); // unexpected case
        break;
    }
    base.VisitExtendedPattern(pattern, context);
  }

  /// <summary>
  /// Returns whether any type changed.
  /// </summary>
  public bool Solve(ErrorReporter reporter, bool debugPrint) {
    var context = new FlowContext(systemModuleManager, reporter, debugPrint,
      InvariantConstructorInference: systemModuleManager.Options.Get(CommonOptionBag.InvariantConstructorInference));
    var changed = false;
    bool anythingChanged;
    do {
      anythingChanged = false;
      foreach (var flow in flows.Where(flow => !flow.HasError)) {
        anythingChanged |= flow.Update(context);
      }
      changed |= anythingChanged;
    } while (anythingChanged);

    if (debugPrint) {
      DebugPrint();
    }
    return changed;
  }

}
