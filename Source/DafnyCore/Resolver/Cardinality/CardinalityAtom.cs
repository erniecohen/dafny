// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable
using System;
using System.Runtime.CompilerServices;

namespace Microsoft.Dafny;

/// <summary>A semantic declaration identity; names and source locations are not identities.</summary>
internal readonly struct CardinalityAtom : IEquatable<CardinalityAtom> {
  internal TopLevelDecl Owner { get; }
  // -1 denotes a nominal head; otherwise this is the formal's positional index.
  internal int Index { get; }
  internal bool IsFormal => Index >= 0;

  private CardinalityAtom(TopLevelDecl owner, int index) {
    Owner = owner;
    Index = index;
  }

  internal static CardinalityAtom Head(TopLevelDecl declaration) => new(declaration, -1);
  internal static CardinalityAtom Formal(TopLevelDecl owner, int index) => new(owner, index);
  public bool Equals(CardinalityAtom other) => ReferenceEquals(Owner, other.Owner) && Index == other.Index;
  public override bool Equals(object? other) => other is CardinalityAtom atom && Equals(atom);
  public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(Owner), Index);
}
