// RUN: %exits-with 2 %resolve --type-system-refresh=true --general-traits=datatype --general-newtypes=true --member-invariants "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Member invariants (https://github.com/erniecohen/dafny/issues/105): a trait may not declare a
// witness, a general trait included (not yet supported; see section 10 of
// docs/dev/issue105-member-invariants-plan.md).  A trait's values are those of the types that extend
// it, each with its own witness or none.  A datatype that has no invariant needs no witness; one that
// only inherits invariants may declare one (git-issue-105-general-traits.dfy), and one that extends
// only traits without invariants may not (git-issue-105-trait-errors.dfy).  A datatype declares at
// most one witness.

module Reference {
  trait P extends object {
    const k: int
    invariant k > 0
    witness 3                                          // error
  }
}

module General {
  trait G {
    const k: int
    invariant k > 0
    ghost witness 3                                    // error
  }
}

module NoInvariant {
  datatype D = D(x: int) {
    witness D(0)                                       // error: D has no invariant
  }
}

module TwoWitnesses {
  datatype D = D(x: int) {
    invariant x > 0
    witness D(1)
    witness D(2)                                       // error: D already declares a witness
  }
}
