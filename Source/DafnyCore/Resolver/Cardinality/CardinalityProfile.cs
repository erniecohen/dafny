// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny;

internal enum CardinalityReasonKind {
  ConstructorFormal,
  NewtypeBase,
  SynonymRhs,
  InstanceField,
  GeneralTraitInclusion,
  ParentContract
}

/// <summary>A persistent trail avoids quadratic copying when a type is deeply nested.</summary>
internal sealed record CardinalityPosition(CardinalityPosition? Parent, string Step) {
  public override string ToString() {
    var steps = new Stack<string>();
    for (CardinalityPosition? position = this; position != null; position = position.Parent) {
      steps.Push(position.Step);
    }
    return string.Join(" -> ", steps);
  }
}

internal sealed record CardinalityReason(IOrigin Origin, CardinalityReasonKind Kind,
  string Description, CardinalityPosition? Position = null) {
  internal CardinalityReason Through(string step) => this with {
    Position = new CardinalityPosition(Position, step)
  };
  internal string Display => Position == null ? Description : $"{Description} -> {Position}";

  internal static int Compare(CardinalityReason left, CardinalityReason right) {
    var order = CardinalityOrder.CompareOrigins(left.Origin, right.Origin);
    if (order != 0) { return order; }
    order = left.Kind.CompareTo(right.Kind);
    if (order != 0) { return order; }
    order = StringComparer.Ordinal.Compare(left.Description, right.Description);
    return order != 0 ? order : StringComparer.Ordinal.Compare(left.Position?.ToString(), right.Position?.ToString());
  }
}

internal sealed record CardinalityDependency(CardinalityWeight Weight, CardinalityReason Reason);

/// <summary>The map is private to a validation run and joining never loses an expansive occurrence.</summary>
internal sealed class CardinalityProfile {
  private readonly Dictionary<CardinalityAtom, CardinalityDependency> dependencies = new();
  internal IEnumerable<KeyValuePair<CardinalityAtom, CardinalityDependency>> Entries => dependencies;
  internal bool TryGet(CardinalityAtom atom, out CardinalityDependency dependency) =>
    dependencies.TryGetValue(atom, out dependency!);

  internal void Add(CardinalityAtom atom, CardinalityWeight weight, CardinalityReason reason) {
    if (!dependencies.TryGetValue(atom, out var previous) || (byte)weight > (byte)previous.Weight ||
        weight == previous.Weight && CardinalityReason.Compare(reason, previous.Reason) < 0) {
      dependencies[atom] = new CardinalityDependency(weight, reason);
    }
  }
}

internal static class CardinalityOrder {
  internal static int CompareOrigins(IOrigin left, IOrigin right) {
    var order = StringComparer.Ordinal.Compare(left.Uri?.ToString(), right.Uri?.ToString());
    if (order != 0) { return order; }
    order = left.pos.CompareTo(right.pos);
    if (order != 0) { return order; }
    order = left.line.CompareTo(right.line);
    return order != 0 ? order : left.col.CompareTo(right.col);
  }

  internal static int CompareDeclarations(TopLevelDecl left, TopLevelDecl right) {
    var order = CompareOrigins(left.Origin, right.Origin);
    return order != 0 ? order : StringComparer.Ordinal.Compare(left.FullName, right.FullName);
  }

  internal static readonly IComparer<TopLevelDecl> Declarations =
    Comparer<TopLevelDecl>.Create(CompareDeclarations);
}
