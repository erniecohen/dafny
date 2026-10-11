module SameModule {
  function Identity(x: int): int { x }
  // The literal call occurs only in this other function's body.
  function OnlyInOtherBody(): int { Identity(777) }
  lemma Unrelated(x: int) ensures Identity(x) == x { }
  lemma ThroughOtherBody() ensures OnlyInOtherBody() == 777 { }
  lemma Branches(b: bool) {
    if b { assert Identity(17) == 17; }
    else { assert Identity(17) == 17; }
  }
  lemma Repeated() ensures Identity(42) + Identity(7) + Identity(42) == 91 { }
}
module SeparateModule {
  function Identity(x: int): int { x }
  lemma Unrelated(x: int) ensures Identity(x) == x { }
}
