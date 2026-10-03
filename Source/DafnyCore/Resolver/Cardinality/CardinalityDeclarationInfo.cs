// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Microsoft.Dafny;

/// <summary>A substituted type cursor. Substitution never rewrites the resolved type AST.</summary>
internal sealed record CardinalityTypeUse(Type Type, CardinalitySubstitution? Substitution = null);

internal sealed class CardinalitySubstitution {
  private readonly Dictionary<TypeParameter, CardinalityTypeUse> bindings;
  private readonly CardinalitySubstitution? enclosing;

  internal CardinalitySubstitution(Dictionary<TypeParameter, CardinalityTypeUse> bindings,
    CardinalitySubstitution? enclosing = null) {
    this.bindings = bindings;
    this.enclosing = enclosing;
  }

  internal bool TryGet(TypeParameter parameter, out CardinalityTypeUse value) {
    for (var environment = this; environment != null; environment = environment.enclosing) {
      if (environment.bindings.TryGetValue(parameter, out value!)) {
        return true;
      }
    }
    value = null!;
    return false;
  }

  internal static CardinalitySubstitution Bind(TopLevelDecl declaration, IReadOnlyList<CardinalityTypeUse> actuals,
    CardinalitySubstitution? enclosing = null) {
    if (declaration.TypeArgs.Count != actuals.Count) {
      throw new CardinalityTypeException(declaration.Origin, "inconsistent type-argument arity in a resolved declaration");
    }
    var bindings = new Dictionary<TypeParameter, CardinalityTypeUse>();
    for (var index = 0; index < actuals.Count; index++) {
      bindings.Add(declaration.TypeArgs[index], actuals[index]);
    }
    return new CardinalitySubstitution(bindings, enclosing);
  }
}

internal sealed record CardinalityRepresentation(CardinalityTypeUse Type, CardinalityReason Reason, bool EmitsEdges);
internal sealed record CardinalityParentInstance(TopLevelDecl Parent, ImmutableArray<CardinalityTypeUse> Actuals,
  CardinalityReason Reason);

internal sealed record CardinalityDeclarationInfo(TopLevelDecl Declaration, ImmutableArray<CardinalityWeight> Modes,
  ImmutableArray<CardinalityRepresentation> Representations, ImmutableArray<CardinalityParentInstance> Parents);

internal sealed class CardinalityTypeException(IOrigin origin, string message) : Exception(message) {
  internal IOrigin Origin { get; } = origin;
}

/// <summary>Views share identity. Refinements share carriers only in the selected replacement environment.</summary>
internal sealed class CardinalityCanonicalizer {
  private readonly CancellationToken cancellationToken;
  private readonly Dictionary<TopLevelDecl, TopLevelDecl> canonical = new();
  private readonly Dictionary<TopLevelDecl, TopLevelDecl> views = new();
  private readonly Dictionary<TopLevelDecl, TopLevelDecl> selections = new();
  private readonly Dictionary<TopLevelDecl, ImmutableArray<CardinalityWeight>> advertisedModes = new();
  private readonly Dictionary<TypeParameter, CardinalityAtom> formals = new();

  internal CardinalityCanonicalizer(CancellationToken cancellationToken,
    IReadOnlyDictionary<ModuleDefinition, ModuleDefinition>? replacements = null) {
    this.cancellationToken = cancellationToken;
    if (replacements == null) { return; }
    var pending = new Queue<(ModuleDefinition Target, ModuleDefinition Selected, bool Contextual)>();
    var processed = new HashSet<(ModuleDefinition, ModuleDefinition, bool)>();
    foreach (var replacement in replacements.OrderBy(entry => entry.Key.FullName, StringComparer.Ordinal)) {
      pending.Enqueue((replacement.Key, replacement.Value, false));
    }
    while (pending.TryDequeue(out var pair)) {
      cancellationToken.ThrowIfCancellationRequested();
      if (!processed.Add(pair) || ReferenceEquals(pair.Target, pair.Selected)) { continue; }
      var originalTypes = pair.Target.TopLevelDecls.Where(CardinalityValidator.IsTypeDeclaration)
        .Select(raw => pair.Contextual ? ContextDeclaration(raw) : ViewDeclaration(raw))
        .Where(original => original is not DefaultClassDecl).Distinct().ToArray();
      var sources = new Dictionary<TopLevelDecl, List<TopLevelDecl>>();
      foreach (var original in originalTypes) {
        cancellationToken.ThrowIfCancellationRequested();
        var source = ViewDeclaration(original);
        if (!sources.TryGetValue(source, out var interfaces)) { sources[source] = interfaces = []; }
        interfaces.Add(original);
      }
      var correspondences = new HashSet<TopLevelDecl>();
      foreach (var raw in pair.Selected.TopLevelDecls.Where(CardinalityValidator.IsTypeDeclaration)) {
        cancellationToken.ThrowIfCancellationRequested();
        var selected = ContextDeclaration(raw);
        var current = ViewDeclaration(selected);
        if (current is DefaultClassDecl) { continue; }
        var seen = new HashSet<TopLevelDecl>();
        while (true) {
          cancellationToken.ThrowIfCancellationRequested();
          if (!seen.Add(current)) {
            throw new CardinalityTypeException(selected.Origin, "cyclic refinement correspondence in a selected replacement");
          }
          if (sources.TryGetValue(current, out var interfaces)) {
            foreach (var original in interfaces) {
              AddSelection(original, selected);
              correspondences.Add(original);
            }
            // Stop at this exact interface. Ordinary template ancestors beyond it stay independent.
            break;
          }
          if (current.CardinalityRefinementBase is not { } predecessor) { break; }
          current = ViewDeclaration(predecessor);
        }
      }
      foreach (var original in originalTypes) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!correspondences.Contains(original)) {
          throw new CardinalityTypeException(original.Origin,
            $"selected replacement of '{pair.Target.FullName}' has no recorded correspondence for type '{original.FullName}'");
        }
      }

      // A selected outer replacement can replace an abstract import with a concrete
      // refining module. Its synthetic facade has its own raw declarations; select
      // those before their visibility links, rather than selecting the global template.
      foreach (var original in pair.Target.TopLevelDecls.OfType<AbstractModuleDecl>()) {
        cancellationToken.ThrowIfCancellationRequested();
        var source = ViewDeclaration(original);
        var counterparts = new List<ModuleDecl>();
        foreach (var raw in pair.Selected.TopLevelDecls.OfType<ModuleDecl>()) {
          var current = ViewDeclaration(raw);
          var seen = new HashSet<TopLevelDecl>();
          while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!seen.Add(current)) {
              throw new CardinalityTypeException(raw.Origin, "cyclic abstract-import correspondence in a selected replacement");
            }
            if (ReferenceEquals(current, source)) { counterparts.Add(raw); break; }
            if (current.CardinalityRefinementBase is not { } predecessor) { break; }
            current = ViewDeclaration(predecessor);
          }
        }
        if (counterparts.Count != 1 || original.Signature?.ModuleDef is not { } facade ||
            counterparts[0].Signature?.ModuleDef is not { } implementation) {
          throw new CardinalityTypeException(original.Origin,
            "a selected replacement has no unique resolved correspondence for an abstract import");
        }
        pending.Enqueue((facade, implementation, true));
      }
    }
    // Join every exposed interface's contract before profiling anything. The selected body
    // still validates its own (possibly stronger) contract; no source variance modes change.
    foreach (var original in selections.Keys) {
      cancellationToken.ThrowIfCancellationRequested();
      var owner = Declaration(original);
      if (!advertisedModes.TryGetValue(owner, out var modes)) {
        modes = owner.TypeArgs.Select(CardinalityWeights.Mode).ToImmutableArray();
      }
      advertisedModes[owner] = modes.Select((mode, index) =>
        CardinalityWeights.Join(mode, CardinalityWeights.Mode(original.TypeArgs[index]))).ToImmutableArray();
    }
  }

  private static TopLevelDecl ContextDeclaration(TopLevelDecl declaration) =>
    declaration is NonNullTypeDecl wrapper ? wrapper.Class : declaration;

  private void AddSelection(TopLevelDecl original, TopLevelDecl selected) {
    CheckLink(original, selected, true);
    if (ReferenceEquals(original, selected)) { return; }
    if (selections.TryGetValue(original, out var other) && !ReferenceEquals(other, selected)) {
      throw new CardinalityTypeException(original.Origin, "inconsistent selected type replacement");
    }
    selections[original] = selected;
  }

  internal TopLevelDecl Declaration(TopLevelDecl declaration) {
    var path = new List<TopLevelDecl>();
    var seen = new HashSet<TopLevelDecl>();
    var current = declaration;
    while (!canonical.TryGetValue(current, out _)) {
      cancellationToken.ThrowIfCancellationRequested();
      if (!seen.Add(current)) {
        throw new CardinalityTypeException(declaration.Origin, "cyclic semantic type-view or replacement metadata");
      }
      path.Add(current);
      // Contextual facade selections precede pure-view links to the global template.
      TopLevelDecl? next;
      var selection = selections.TryGetValue(current, out next);
      if (!selection) { next = ViewTarget(current); }
      if (next == null) {
        canonical[current] = current;
        break;
      }
      CheckLink(current, next, selection);
      current = next;
    }
    var owner = canonical[current];
    foreach (var view in path) {
      cancellationToken.ThrowIfCancellationRequested();
      canonical[view] = owner;
      // Internal synonyms can share their formal objects with their target.
      for (var index = 0; index < view.TypeArgs.Count; index++) {
        formals[view.TypeArgs[index]] = CardinalityAtom.Formal(owner, index);
      }
    }
    return owner;
  }

  internal ImmutableArray<CardinalityWeight> AdvertisedModes(TopLevelDecl declaration) {
    var owner = Declaration(declaration);
    return advertisedModes.TryGetValue(owner, out var modes)
      ? modes : owner.TypeArgs.Select(CardinalityWeights.Mode).ToImmutableArray();
  }

  private TopLevelDecl ViewDeclaration(TopLevelDecl declaration) {
    var path = new List<TopLevelDecl>();
    var seen = new HashSet<TopLevelDecl>();
    var current = declaration;
    while (!views.TryGetValue(current, out _)) {
      cancellationToken.ThrowIfCancellationRequested();
      if (!seen.Add(current)) {
        throw new CardinalityTypeException(declaration.Origin, "cyclic semantic type-view metadata");
      }
      path.Add(current);
      var next = ViewTarget(current);
      if (next == null) {
        views[current] = current;
        break;
      }
      CheckLink(current, next, false);
      current = next;
    }
    var owner = views[current];
    foreach (var view in path) { views[view] = owner; }
    return owner;
  }

  private TopLevelDecl? ViewTarget(TopLevelDecl declaration) {
    TopLevelDecl? internalTarget = null;
    if (declaration is InternalTypeSynonymDecl synonym) {
      if (synonym.Rhs is not UserDefinedType { ResolvedClass: { } target } nominal) {
        throw new CardinalityTypeException(declaration.Origin,
          "an internal type view has no resolved nominal target");
      }
      // Generated self-synonyms are exactly D<formals>. Other shapes can create fresh
      // substitution environments indefinitely and are not semantic identity views.
      if (nominal.TypeArgs.Count != synonym.TypeArgs.Count) {
        throw new CardinalityTypeException(declaration.Origin, "an internal type view changes type-argument arity");
      }
      for (var index = 0; index < nominal.TypeArgs.Count; index++) {
        cancellationToken.ThrowIfCancellationRequested();
        if (nominal.TypeArgs[index] is not UserDefinedType { ResolvedClass: TypeParameter parameter } argument ||
            argument.TypeArgs.Count != 0 || !ReferenceEquals(parameter, synonym.TypeArgs[index])) {
          throw new CardinalityTypeException(declaration.Origin,
            "an internal type view is not a positional identity of its nominal target");
        }
      }
      internalTarget = target;
    }
    return declaration switch {
      NonNullTypeDecl wrapper => wrapper.Class,
      { CardinalityViewOf: { } view } => view,
      _ => internalTarget
    };
  }

  private void CheckLink(TopLevelDecl source, TopLevelDecl target, bool selection) {
    if (source.TypeArgs.Count != target.TypeArgs.Count) {
      throw new CardinalityTypeException(source.Origin,
        selection ? "a selected replacement changes type-parameter arity" : "a semantic type view changes type-parameter arity");
    }
    for (var index = 0; index < source.TypeArgs.Count; index++) {
      cancellationToken.ThrowIfCancellationRequested();
      var original = CardinalityWeights.Mode(source.TypeArgs[index]);
      var replacement = CardinalityWeights.Mode(target.TypeArgs[index]);
      if (selection ? (byte)replacement > (byte)original : replacement != original) {
        throw new CardinalityTypeException(source.Origin, selection
          ? "a selected replacement weakens an exposed strict cardinality contract"
          : "a semantic type view changes a cardinality contract");
      }
    }
  }

  internal CardinalityAtom Formal(TypeParameter parameter) {
    if (formals.TryGetValue(parameter, out var atom)) {
      return atom;
    }
    if (parameter.Parent is not TopLevelDecl owner) {
      throw new CardinalityTypeException(parameter.Origin, "a representation contains an unbound type parameter");
    }
    var index = owner.TypeArgs.IndexOf(parameter);
    if (index < 0) {
      throw new CardinalityTypeException(parameter.Origin, "a type parameter has no resolved positional owner");
    }
    return CardinalityAtom.Formal(Declaration(owner), index);
  }
}
