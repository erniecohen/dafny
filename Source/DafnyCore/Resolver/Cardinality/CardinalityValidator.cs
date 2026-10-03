// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Microsoft.Dafny;

internal sealed record CardinalityValidationResult(bool Succeeded, int DeclarationCount, int EdgeCount, int ComponentCount);

/// <summary>
/// Declaration-level admission of a resolved compilation unit and all its loaded dependencies.
/// This phase produces no verifier assumptions, type mutations, or code-generation representations.
/// </summary>
internal static class CardinalityValidator {
  internal static CardinalityValidationResult Validate(Program program, CancellationToken cancellationToken) {
    program.CardinalityValidationReceipt = null;
    cancellationToken.ThrowIfCancellationRequested();
    if (program.Reporter.HasErrors) {
      return new CardinalityValidationResult(false, 0, 0, 0);
    }
    if (program.ModuleSigs == null || program.SystemModuleManager == null) {
      program.Reporter.Error(MessageSource.Resolver, ResolutionErrors.ErrorId.r_cardinality_unclassified_type,
        program.DefaultModule.Origin, "Cardinality validation requires a program with resolved module signatures.");
      return new CardinalityValidationResult(false, 0, 0, 0);
    }
    var previousErrors = program.Reporter.ErrorCount;
    var declarationCount = 0;
    var edgeCount = 0;
    var componentCount = 0;
    try {
      var canonicalizer = new CardinalityCanonicalizer(cancellationToken, program.Replacements);
      var visitor = new CardinalityTypeVisitor(program.SystemModuleManager, canonicalizer, cancellationToken);
      var declarations = Snapshot(program, canonicalizer, visitor, cancellationToken);
      declarationCount = declarations.Count;
      var graph = new CardinalityGraph(declarations.Select(info => info.Declaration), cancellationToken);
      var parentChecker = new CardinalityParentChecker(visitor, program.Reporter, cancellationToken);
      var success = true;
      foreach (var info in declarations) {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var representation in info.Representations) {
          var profile = visitor.Profile(representation.Type, representation.Reason);
          foreach (var entry in profile.Entries.OrderBy(entry => entry.Key.Owner, CardinalityOrder.Declarations)
                     .ThenBy(entry => entry.Key.Index)) {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.Key.IsFormal) {
              if (!ReferenceEquals(entry.Key.Owner, info.Declaration)) {
                throw new CardinalityTypeException(entry.Value.Reason.Origin,
                  "an unsubstituted enclosing type parameter reached a representation descriptor");
              }
              if (entry.Key.Index >= info.Modes.Length) {
                throw new CardinalityTypeException(entry.Value.Reason.Origin, "a formal parameter has an invalid positional index");
              }
              if ((byte)entry.Value.Weight > (byte)info.Modes[entry.Key.Index]) {
                success = false;
                var parameter = info.Declaration.TypeArgs[entry.Key.Index];
                program.Reporter.MessageCore(new DafnyDiagnostic(MessageSource.Resolver,
                  ResolutionErrors.ErrorId.r_cardinality_parameter_contract.ToString(), entry.Value.Reason.Origin.ReportingRange,
                  [$"Type parameter '{parameter.Name}' of '{info.Declaration.FullName}' has a strict cardinality contract, " +
                   $"but is used in a potentially expansive representation position ({entry.Value.Reason.Display}). " +
                   $"Declare a permissive cardinality mode such as '!{parameter.Name}' if this use is intended."],
                  ErrorLevel.Error,
                  [new DafnyRelatedInformation(parameter.Origin.ReportingRange, "", ["The strict type parameter is declared here."])]));
              }
            } else if (representation.EmitsEdges) {
              graph.AddEdge(new CardinalityEdge(info.Declaration, entry.Key.Owner, entry.Value.Weight, entry.Value.Reason));
            }
          }
        }
        foreach (var parent in info.Parents) {
          cancellationToken.ThrowIfCancellationRequested();
          success &= parentChecker.Check(info, parent);
          if (parent.Parent is TraitDecl { IsReferenceTypeDecl: false }) {
            graph.AddEdge(new CardinalityEdge(parent.Parent, info.Declaration, CardinalityWeight.Preserving,
              parent.Reason with { Kind = CardinalityReasonKind.GeneralTraitInclusion }));
          }
        }
      }
      edgeCount = graph.EdgeCount;
      var analysis = graph.FindExpansiveCycles();
      componentCount = analysis.ComponentCount;
      foreach (var cycle in analysis.Cycles) {
        cancellationToken.ThrowIfCancellationRequested();
        success = false;
        ReportCycle(program.Reporter, cycle, cancellationToken);
      }
      cancellationToken.ThrowIfCancellationRequested();
      var result = new CardinalityValidationResult(success && program.Reporter.ErrorCount == previousErrors,
        declarationCount, edgeCount, componentCount);
      if (result.Succeeded) {
        program.CardinalityValidationReceipt = result;
      }
      return result;
    } catch (CardinalityTypeException exception) {
      program.Reporter.Error(MessageSource.Resolver, ResolutionErrors.ErrorId.r_cardinality_unclassified_type,
        exception.Origin, $"Cardinality validation cannot classify this resolved type: {exception.Message}.");
      return new CardinalityValidationResult(false, declarationCount, edgeCount, componentCount);
    }
  }

  internal static bool IsTypeDeclaration(TopLevelDecl declaration) => declaration is not TypeParameter && declaration is
    DatatypeDecl or NewtypeDecl or AbstractTypeDecl or TypeSynonymDeclBase or ClassLikeDecl or ValuetypeDecl;

  private static List<CardinalityDeclarationInfo> Snapshot(Program program, CardinalityCanonicalizer canonicalizer,
    CardinalityTypeVisitor visitor, CancellationToken cancellationToken) {
    var scheduled = new HashSet<TopLevelDecl>();
    var pendingDeclarations = new Queue<TopLevelDecl>();
    var pendingNodes = new Stack<INode>();
    var seenNodes = new HashSet<INode>();
    var result = new List<CardinalityDeclarationInfo>();
    foreach (var module in program.RawModules().Append(program.SystemModuleManager.SystemModule)
               .Distinct().OrderBy(module => module.FullName, StringComparer.Ordinal)) {
      cancellationToken.ThrowIfCancellationRequested();
      foreach (var declaration in module.TopLevelDecls) {
        pendingNodes.Push(declaration);
        if (IsTypeDeclaration(declaration)) { Schedule(declaration); }
      }
    }
    while (pendingNodes.Count != 0 || pendingDeclarations.Count != 0) {
      cancellationToken.ThrowIfCancellationRequested();
      while (pendingNodes.TryPop(out var node)) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!seenNodes.Add(node)) { continue; }
        if (node is Type type) {
          if (type is TypeProxy { T: { } target }) { pendingNodes.Push(target); }
          if (type is SelfType { ResolvedType: { } resolved }) { pendingNodes.Push(resolved); }
          if (type is UserDefinedType { ResolvedClass: { } head } && head is not TypeParameter) {
            Schedule(head);
            if (head is InternalTypeSynonymDecl synonym) { pendingNodes.Push(synonym.Rhs); }
          }
        }
        if (node is Expression { UnnormalizedType: { } expressionType }) { pendingNodes.Push(expressionType); }
        if (node is IVariable { UnnormalizedType: { } variableType }) { pendingNodes.Push(variableType); }
        if (node is Field { Type: { } fieldType }) { pendingNodes.Push(fieldType); }
        if (node is ResolverIdentifierExpr identifier && IsTypeDeclaration(identifier.Decl)) { Schedule(identifier.Decl); }
        if (node is TypeParameter parameter) {
          foreach (var bound in parameter.TypeBounds) { pendingNodes.Push(bound); }
        }
        if (node is TopLevelDecl declaration) {
          if (IsTypeDeclaration(declaration)) { Schedule(declaration); }
          foreach (var formal in declaration.TypeArgs) { pendingNodes.Push(formal); }
          foreach (var obligation in declaration.CardinalityParentObligations) { pendingNodes.Push(obligation); }
        }
        foreach (var child in DiscoveryChildren(node)) {
          if (child != null) { pendingNodes.Push(child); }
        }
      }
      if (pendingDeclarations.TryDequeue(out var dequeuedDeclaration)) {
        var info = Extract(dequeuedDeclaration, canonicalizer, visitor, cancellationToken);
        result.Add(info);
        foreach (var representation in info.Representations) {
          foreach (var entry in visitor.Profile(representation.Type, representation.Reason).Entries) {
            if (!entry.Key.IsFormal) { Schedule(entry.Key.Owner); }
          }
        }
        foreach (var parent in info.Parents) {
          Schedule(parent.Parent);
          foreach (var actual in parent.Actuals) {
            foreach (var entry in visitor.Profile(actual, parent.Reason).Entries) {
              if (!entry.Key.IsFormal) { Schedule(entry.Key.Owner); }
            }
          }
        }
      }
    }
    return result.OrderBy(info => info.Declaration, CardinalityOrder.Declarations).ToList();

    // Some AST Children implementations flatten Type.Nodes recursively. Discover their types directly instead.
    static IEnumerable<INode> DiscoveryChildren(INode node) {
      return node switch {
        Field field => field.Attributes.AsEnumerable().Cast<INode>()
          .Concat(field is ConstantField { Rhs: { } rhs } ? new[] { rhs } : Enumerable.Empty<INode>()),
        TypeUnaryExpr unary => unary.SubExpressions.Cast<INode>().Append(unary.ToType),
        StaticReceiverExpr receiver => receiver.SubExpressions,
        ResolverIdentifierExpr identifier => identifier.TypeArgs,
        AllocateArray allocation => new[] { allocation.ElementType }.OfType<INode>()
          .Concat(allocation.SubExpressions).Concat(allocation.SubStatements),
        _ => node.Children
      };
    }

    void Schedule(TopLevelDecl raw) {
      var declaration = canonicalizer.Declaration(raw);
      // Discover the raw view's members and referenced types even when its carrier was already scheduled.
      if (!seenNodes.Contains(raw)) { pendingNodes.Push(raw); }
      // A default class is a static namespace, not a source carrier. Its fictitious receiver types
      // can occur directly or behind an internal/provided view; both must still expose their members.
      if (declaration is DefaultClassDecl) {
        if (!seenNodes.Contains(declaration)) { pendingNodes.Push(declaration); }
        return;
      }
      if (!IsTypeDeclaration(declaration)) {
        throw new CardinalityTypeException(raw.Origin,
          $"nominal type reference '{raw.FullName}' ({raw.GetType().Name}) does not denote a type declaration");
      }
      if (scheduled.Add(declaration)) {
        pendingDeclarations.Enqueue(declaration);
        pendingNodes.Push(declaration);
      }
    }
  }

  private static CardinalityDeclarationInfo Extract(TopLevelDecl declaration, CardinalityCanonicalizer canonicalizer,
    CardinalityTypeVisitor visitor, CancellationToken cancellationToken) {
    cancellationToken.ThrowIfCancellationRequested();
    var representations = ImmutableArray.CreateBuilder<CardinalityRepresentation>();
    var parents = ImmutableArray.CreateBuilder<CardinalityParentInstance>();
    var modes = declaration.TypeArgs.Select(CardinalityWeights.Mode).ToImmutableArray();
    if (visitor.IsBuiltin(declaration)) {
      return new CardinalityDeclarationInfo(declaration, modes, representations.ToImmutable(), parents.ToImmutable());
    }
    switch (declaration) {
      case DatatypeDecl datatype:
        foreach (var constructor in datatype.Ctors) {
          cancellationToken.ThrowIfCancellationRequested();
          foreach (var formal in constructor.Formals) {
            representations.Add(new CardinalityRepresentation(new CardinalityTypeUse(formal.Type),
              new CardinalityReason(formal.Origin, CardinalityReasonKind.ConstructorFormal,
                $"constructor '{constructor.Name}.{formal.Name}'"), true));
          }
        }
        break;
      case NewtypeDecl newtype:
        if (newtype.BaseType == null) {
          throw new CardinalityTypeException(newtype.Origin, "a resolved newtype has no base type");
        }
        representations.Add(new CardinalityRepresentation(new CardinalityTypeUse(newtype.BaseType),
          new CardinalityReason(newtype.Origin, CardinalityReasonKind.NewtypeBase,
            $"base type of '{newtype.FullName}'"), true));
        break;
      case TypeSynonymDeclBase synonym:
        if (synonym.Rhs == null) {
          throw new CardinalityTypeException(synonym.Origin, "a resolved redirecting type has no representation");
        }
        representations.Add(new CardinalityRepresentation(new CardinalityTypeUse(synonym.Rhs),
          new CardinalityReason(synonym.Origin, CardinalityReasonKind.SynonymRhs,
            $"right-hand side of '{synonym.FullName}'"), true));
        break;
      case ClassLikeDecl { IsReferenceTypeDecl: true } reference:
        // Fields validate formal contracts only. A reference object is not its field tuple.
        var fields = reference.Members.OfType<Field>().Concat(reference.InheritedMembers.OfType<Field>()).Distinct();
        var inheritedBindings = new Dictionary<TypeParameter, CardinalityTypeUse>();
        var inheritedByPosition = new Dictionary<CardinalityAtom, CardinalityTypeUse>();
        foreach (var binding in reference.ParentFormalTypeParametersToActuals) {
          cancellationToken.ThrowIfCancellationRequested();
          var actual = new CardinalityTypeUse(binding.Value);
          inheritedBindings[binding.Key] = actual;
          inheritedByPosition[canonicalizer.Formal(binding.Key)] = actual;
        }
        foreach (var field in fields.Where(field => !field.IsStatic)) {
          cancellationToken.ThrowIfCancellationRequested();
          var bindings = new Dictionary<TypeParameter, CardinalityTypeUse>(inheritedBindings);
          if (field.EnclosingClass != null) {
            var fieldOwner = canonicalizer.Declaration(field.EnclosingClass);
            for (var index = 0; index < field.EnclosingClass.TypeArgs.Count; index++) {
              var parameter = field.EnclosingClass.TypeArgs[index];
              if (ReferenceEquals(fieldOwner, declaration)) {
                bindings[parameter] = new CardinalityTypeUse(new UserDefinedType(declaration.TypeArgs[index]));
              } else if (inheritedByPosition.TryGetValue(CardinalityAtom.Formal(fieldOwner, index), out var actual)) {
                bindings[parameter] = actual;
              } else {
                throw new CardinalityTypeException(field.Origin, "an inherited field has no resolved parent-parameter substitution");
              }
            }
          }
          representations.Add(new CardinalityRepresentation(
            new CardinalityTypeUse(field.Type, new CardinalitySubstitution(bindings)),
            new CardinalityReason(field.Origin, CardinalityReasonKind.InstanceField, $"instance field '{field.Name}'"), false));
        }
        break;
      case AbstractTypeDecl:
      case TraitDecl:
        // A trait's method signatures are not arbitrary stored payloads; an abstract type has no fabricated RHS.
        break;
      default:
        throw new CardinalityTypeException(declaration.Origin, "unclassified type declaration");
    }
    var ordinaryParents = declaration is TopLevelDeclWithMembers members ? members.Traits : Enumerable.Empty<Type>();
    foreach (var type in ordinaryParents.Concat(declaration.CardinalityParentObligations)) {
      cancellationToken.ThrowIfCancellationRequested();
      var reason = new CardinalityReason(type.Origin ?? declaration.Origin, CardinalityReasonKind.ParentContract,
        $"implementation '{declaration.FullName}' extends a parent trait");
      var parent = visitor.Parent(new CardinalityTypeUse(type), reason);
      if (!parents.Any(existing => ReferenceEquals(existing.Parent, parent.Parent) &&
          existing.Actuals.Length == parent.Actuals.Length &&
          existing.Actuals.Zip(parent.Actuals, visitor.SameType).All(equal => equal))) {
        parents.Add(parent with { Reason = reason with {
          Description = $"'{declaration.FullName}' extends '{parent.Parent.FullName}'"
        } });
      }
    }
    return new CardinalityDeclarationInfo(declaration, modes, representations.ToImmutable(), parents.ToImmutable());
  }

  private static void ReportCycle(ErrorReporter reporter, CardinalityCycle cycle, CancellationToken cancellationToken) {
    var inclusion = cycle.Edges.FirstOrDefault(edge => edge.Reason.Kind == CardinalityReasonKind.GeneralTraitInclusion);
    var primary = inclusion?.Reason.Origin ?? cycle.Edges[0].Reason.Origin;
    var parts = new List<string> { "This type definition creates a potentially cardinality-expanding cycle:" };
    var related = new List<DafnyRelatedInformation>();
    foreach (var edge in cycle.Edges) {
      cancellationToken.ThrowIfCancellationRequested();
      var step = edge.Reason.Kind == CardinalityReasonKind.GeneralTraitInclusion
        ? $"'{edge.From.FullName}' contains '{edge.To.FullName}' because {edge.Reason.Description}"
        : $"'{edge.From.FullName}' depends on '{edge.To.FullName}' through {edge.Reason.Display}";
      parts.Add("  " + step + (edge.Weight == CardinalityWeight.Expanding ? " (potentially expansive)" : ""));
      related.Add(new DafnyRelatedInformation(edge.Reason.Origin.ReportingRange, "", [step]));
      related.Add(new DafnyRelatedInformation(edge.To.Origin.ReportingRange, "", [$"Type '{edge.To.FullName}' is declared here."]));
    }
    parts.Add("Such a cycle is not admissible under the cardinality discipline.");
    reporter.MessageCore(new DafnyDiagnostic(MessageSource.Resolver, ResolutionErrors.ErrorId.r_cardinality_expansive_cycle.ToString(),
      primary.ReportingRange, [string.Join(Environment.NewLine, parts)], ErrorLevel.Error, related));
  }
}
