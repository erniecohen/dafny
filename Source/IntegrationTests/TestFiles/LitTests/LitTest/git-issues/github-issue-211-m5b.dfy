// RUN: python3 "%S/Inputs/github-issue-211-m5b.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%t.dir" "%repositoryRoot/Binaries/z3/bin/z3-5.1.0" "%repositoryRoot/Source/DafnyStandardLibraries/src/Std" > "%t"
// RUN: %audit --modular-termination "%repositoryRoot/Source/DafnyStandardLibraries/modular-termination/trusted/dfyconfig.toml" >> "%t"
// RUN: %diff "%s.expect" "%t"

// The standard-library trust list under --modular-termination, milestone M5b of
// https://github.com/erniecohen/dafny/issues/211.  The driver builds libraries from the real
// src/Std/Axioms.dfy and src/Std/RealCompleteness.dfy, and from edited copies, with and without
// --modular-termination-trust:standard-library.  It checks the record of the trusted library, the
// summary line, the refusals of an edited premise statement, an edited premise context, an unlisted
// {:axiom}, an assume statement and a client that did not opt in, and that a premise-free library
// is byte for byte the same with the trust list, with none, and without the option.  The audit of the
// trusted standard-library project, modular-termination/trusted, is the regression of the list's
// table: each of its 18 premises is admitted only while its statement and context digests match.
