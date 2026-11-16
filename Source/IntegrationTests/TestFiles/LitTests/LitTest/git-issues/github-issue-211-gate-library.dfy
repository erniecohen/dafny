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
//  - sealed, assume, refine: an implementation of a trait that the library did not open, by a class of
//    another module, with @AssumeCrossModuleTermination, and from a module that refines the library's.
// The accepted client (ok) has an 'assert false' control.  The driver also checks that a library without
// a checked termination record (a source file passed to --library, or a .doo built without the option)
// is refused under the option, that a library with a record is refused without the option, and that the
// 'raise' client verifies when neither side uses the option, which is the hole the option closes.
//
// The last two checks use a library with a least lemma under a bound.  It cannot be built into a .doo
// yet (https://github.com/erniecohen/dafny/issues/461), so the library and its client are verified as one
// program: implementations of the library's lemma slot that call the least lemma, or its prefix lemma,
// above the bound of the slot are rejected (https://github.com/erniecohen/dafny/issues/456).
