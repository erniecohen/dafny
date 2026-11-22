// Run by git-issue-105-newtype-base-values-controls.dfy with --unchecked-invariant-extension: the programs that
// git-issue-105-newtype-base-values.dfy rejects, each with a proof of 'false', which verifies once the extension
// condition is off.  So the rejected programs are real holes, not merely unusual ones.

module TypeTest {
  newtype N = x: int | true {
    invariant !((this as int) is N)
  }
  lemma False() ensures false {
    if (0 as int) is N { var y: N := 0; } else { var y: N := 0; }
  }
}

module Literal {
  newtype N = x: int | true {
    invariant Inv(this)
  }
  ghost predicate Inv(x: raw N) ensures !Inv(x) { var y: N := 0; true }
  lemma False() ensures false { var y: N := 0; assert Inv(y); }
}

module Conversion {
  newtype N = x: int | true {
    invariant Inv(this)
  }
  ghost predicate Inv(x: raw N) ensures !Inv(x) { var i: int := 0; var y := i as N; true }
  lemma G(x: N) ensures false { }
  lemma False() ensures false { G(0); }
}

module Constant {
  newtype N = x: int | true {
    invariant Inv(this)
  }
  const C: N := 0
  ghost predicate Inv(x: raw N) ensures !Inv(x) { C == C }
  lemma G(x: N) ensures false { }
  lemma False() ensures false { G(0); }
}
