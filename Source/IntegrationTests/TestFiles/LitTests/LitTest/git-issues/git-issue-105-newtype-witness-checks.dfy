// RUN: %exits-with 4 %verify --type-system-refresh=true --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) of newtypes in the old syntax: the
// header's constraint and witness clause move into the type with its invariants (section 3.5 of
// docs/dev/issue105-member-invariants-plan.md), and are checked there, against every conjunct, as the
// old syntax checks them against the constraint: the well-formedness of the constraint, the witness,
// and with no witness clause, the default 0.  "raw N" is then the base type with no constraint, as
// inhabited as the base type, with nothing of its own to check.  Each check has a negative control,
// marked "error", and a control beside it that verifies.

module Defaults {
  newtype Pos = x: int | x > 0 { invariant true } // error: 0 is not a Pos
  newtype NonNeg = x: int | x >= 0 { invariant true }
  newtype Odd = x: int | true { invariant this as int % 2 == 1 } // error: 0 does not satisfy the invariant
  newtype NotOne = x: int | true { invariant this as int != 1 }
}

module Witnesses {
  newtype Pos = x: int | x > 0 witness 0 { invariant true } // error: the witness is not a Pos
  newtype Pos' = x: int | x > 0 witness 1 { invariant true }
  newtype Ghost = x: int | x > 0 ghost witness 0 { invariant true } // error: the witness is not a Ghost
  newtype Ghost' = x: int | x > 0 ghost witness 1 { invariant true }
  newtype Two = x: int | x > 0 witness 1 { invariant this as int != 1 } // error: the invariant might not hold
  newtype Two' = x: int | x > 0 witness 2 { invariant this as int != 1 }
  newtype Maybe = x: int | x > 0 witness * { invariant this as int != 1 }
}

module WellFormedness {
  newtype Div = x: int | 10 / x > 0 witness 1 { invariant true } // error: division by zero
  newtype Div' = x: int | x != 0 && 10 / x > 0 witness 1 { invariant true }
  // With a witness member, the header's constraint is checked too.
  newtype Member = x: int | 10 / x > 0 { invariant true  witness 1 } // error: division by zero
  newtype Member' = x: int | x != 0 && 10 / x > 0 { invariant true  witness 1 }
}

module Bases {
  newtype Pos = x: int | x > 0 witness 1 { invariant true }
  // "raw Over" is Pos with no constraint, which needs no witness of its own.
  newtype Over = x: Pos | true witness 2 { invariant true }
  newtype Zero = x: Pos | true { invariant true } // error: 0 is not a Pos

  // "raw Above" is as inhabited as Empty, so a local of type "raw Above" has no value to assume.
  newtype Empty = x: int | false witness * { invariant true }
  newtype Above = x: Empty | true witness * { invariant true }
  ghost method NoValue() { var r: raw Above; assert false; } // error: the assertion might not hold
}

// A base type that is inferred is checked as one that is written.
module InferredBases {
  newtype NewSix = x | 6 <= x witness 6
  predicate Yes(x: NewSix) { true }
  newtype Four = six | Yes(six) witness 4 { invariant true } // error: 4 is not a NewSix
  newtype Six = six | Yes(six) witness 6 { invariant true }
  method Convert() returns (y: Six) { y := 3 as Six; } // error: 3 is not a NewSix
  method ConvertChecked() returns (y: Six) { y := 7 as Six; }
}

// Over a base type that may be empty, the witness, the default 0 included, is checked by the verifier, as in
// the old syntax without invariants, not rejected by the resolver.
module EmptyBases {
  type Empty = x: int | false witness *
  newtype Default = x: Empty | true { invariant true } // error: 0 is not an Empty
  newtype Header = x: Empty | true witness 1 { invariant true } // error: 1 is not an Empty
  newtype Member = x: Empty | true { invariant true  witness 1 } // error: 1 is not an Empty
  newtype Maybe = x: Empty | true witness * { invariant true }
}

// The well-formedness of an invariant assumes the header's constraint, which is the conjunct before it.
module EarlierConjuncts {
  newtype Inverse = x: int | true witness 1 { invariant 12 / (this as int) != 5 } // error: division by zero
  newtype Inverse' = x: int | x != 0 witness 1 { invariant 12 / (this as int) != 5 }
}

// The default witness of a newtype is checked against the invariants of its base type, and the error says
// which witness was tried.
module BaseInvariants {
  newtype NonZero = int { invariant this as int != 0  witness 1 }
  newtype Over = x: NonZero | true { invariant true } // error: trying witness 0
  newtype Over' = x: NonZero | true witness 1 { invariant true }
}
