module Chain {
  import opened Std.CallPermissions

  datatype Maybe<T> = None | Some(value: T)

  trait {:termination_closed} Producer extends ProducesRanked {
    ghost predicate Valid()
      decreases {:global} allowance
      ensures Valid() ==> producedAllowanceCeiling <= allowance

    method Next() returns (p: Maybe<Producer>)
      requires Valid()
      decreases {:global} allowance + 2
      ensures p.Some? ==> p.value.allowance < producedAllowanceCeiling
      ensures p.Some? ==> fresh(p.value) && p.value.Valid()
  }

  class {:termination_closed} Link extends Producer {
    const {:termination_closed} depth: nat

    constructor (depth: nat)
      ensures this.depth == depth
      ensures allowance == (2 * depth + 2) as ORDINAL
      ensures Valid()
    {
      this.depth := depth;
      allowance := (2 * depth + 2) as ORDINAL;
      producedAllowanceCeiling := (2 * depth + 1) as ORDINAL;
    }

    ghost predicate Valid()
      ensures Valid() ==> producedAllowanceCeiling <= allowance
    {
      allowance == (2 * depth + 2) as ORDINAL &&
      producedAllowanceCeiling == (2 * depth + 1) as ORDINAL
    }

    method Next() returns (p: Maybe<Producer>)
      requires Valid()
      ensures p.Some? <==> depth > 0
      ensures p.Some? ==> p.value.allowance < producedAllowanceCeiling
      ensures p.Some? ==> fresh(p.value) && p.value.Valid()
    {
      if depth == 0 {
        p := None;
      } else {
        var child := new Link(depth - 1);
        p := Some(child);
      }
    }
  }

  method Use()
    decreases {:global} 20 as ORDINAL
  {
    var first := new Link(3);
    var second := first.Next();
    assert second.Some?;
    var third := second.value.Next();
    // CONTROL
  }
}
