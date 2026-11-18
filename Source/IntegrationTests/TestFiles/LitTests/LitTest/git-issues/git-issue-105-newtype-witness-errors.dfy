// RUN: %exits-with 2 %resolve --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105) of newtypes: the witness rules
// (section 3.5 of docs/dev/issue105-member-invariants-plan.md).  The witness of a newtype with
// invariants is given in its header or as a member, not both, and a newtype with no invariant takes
// no witness member.  Each error has a control beside it that resolves.

module Witnesses {
  newtype Both = x: int | x < 10 witness 3 {
    invariant this as int > 0
    witness 1 // error: a witness in the header and a witness member
  }
  newtype NoInvariant = x: int | x < 10 {
    witness 3 // error: no invariant, so no witness member
  }
  newtype HeaderWitness = x: int | x < 10 witness 3 {
    invariant this as int > 0
  }
  newtype MemberWitness = x: int | x < 10 {
    invariant this as int > 0
    witness 3
  }
  newtype NoInvariantHeaderWitness = x: int | x < 10 witness 3
}
