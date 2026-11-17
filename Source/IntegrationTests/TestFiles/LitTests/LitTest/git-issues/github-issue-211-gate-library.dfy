// RUN: python3 "%S/Inputs/github-issue-211-gate-a.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%t.dir" "%repositoryRoot/Binaries/z3/bin/z3-5.1.0" > "%t"
// RUN: %diff "%s.expect" "%t"

// Soundness gate (a) of the plan for https://github.com/erniecohen/dafny/issues/211: a library that is
// verified alone under --modular-termination is not invalidated by a later extension.
//
// The driver builds Inputs/github-issue-211-gate-a-lib.dfy into a .doo with each resolver and verifies
// clients against it.  Each rejected client would prove false if its extension were accepted:
//  - raise: an implementation of an open slot that calls library code with a larger bound than the slot;
//  - via, via-open: the same through a trait of the client, which cannot raise the bound of the slot
//    (a declared bound binds every implementation);
//  - unbounded: an implementation of a member with no bound that is not a leaf and declares no bound (a
//    foreign implementation of such a member must be a leaf or declare a bound of its own);
//  - bounded: the same with a bound of its own, rejected because it calls library code that has no bound and
//    dispatches, which is TOP;
//  - refine: the same from a module that refines the library's module.
// The accepted clients (ok, and leaf, a leaf implementation of the member with no bound) have 'assert
// false' controls.  One client (assume) uses @AssumeCrossModuleTermination, which keeps its meaning under
// the option: an unchecked premise of the client.  It verifies, 'assert false' included, and the driver
// checks that the default warning reports the premise and fails the run.  The driver also checks that a library without
// a checked termination record (a source file passed to --library, or a .doo built without the option)
// is refused under the option, that a library with a record is refused without the option, and that the
// 'raise' client verifies when neither side uses the option, which is the hole the option closes.
//
// The last three checks use a library with a least lemma under a bound.  It is built into a .doo
// (https://github.com/erniecohen/dafny/issues/461), and implementations of its lemma slot that call the least
// lemma, or its prefix lemma, above the bound of the slot are rejected
// (https://github.com/erniecohen/dafny/issues/456), both against the built library and with the library and
// its client verified as one program.
