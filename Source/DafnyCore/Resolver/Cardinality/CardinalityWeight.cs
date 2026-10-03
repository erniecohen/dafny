// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT

namespace Microsoft.Dafny;

/// <summary>Potential expansion, rather than arithmetic on actual cardinalities.</summary>
internal enum CardinalityWeight : byte {
  Preserving,
  Expanding
}

internal static class CardinalityWeights {
  internal static CardinalityWeight Join(CardinalityWeight left, CardinalityWeight right) =>
    left == CardinalityWeight.Expanding || right == CardinalityWeight.Expanding
      ? CardinalityWeight.Expanding : CardinalityWeight.Preserving;

  internal static CardinalityWeight Mode(TypeParameter parameter) =>
    parameter.StrictVariance ? CardinalityWeight.Preserving : CardinalityWeight.Expanding;
}
