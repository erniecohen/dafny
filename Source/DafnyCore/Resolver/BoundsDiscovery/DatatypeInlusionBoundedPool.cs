//-----------------------------------------------------------------------------
//
// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
//
//-----------------------------------------------------------------------------

namespace Microsoft.Dafny;

public class DatatypeInclusionBoundedPool : BoundedPool {
  public readonly bool IsIndDatatype;

  public DatatypeInclusionBoundedPool(bool isIndDatatype) : base() {
    IsIndDatatype = isIndDatatype;
  }

  // A strict rank inequality is an allocation-independent bound, but it does
  // not bound the number of datatype values. In particular, an iset-valued
  // constructor argument can force infinitely many values below one rank.
  public override PoolVirtues Virtues =>
    PoolVirtues.IndependentOfAlloc | PoolVirtues.IndependentOfAlloc_or_ExplicitAlloc;
  public override int Preference() => 2;
  public override BoundedPool Clone(Cloner cloner) {
    return this;
  }
}
