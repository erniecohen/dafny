// https://github.com/erniecohen/dafny/issues/266, M4: portable contracts of bounded values.  A library with an order
// type, bounded values and argument-dependent bounds stores, in its termination record, the contracts its clients
// use: the order type with its order, the bounded types of its fields and results, and the bounds in its specs, but
// no certified body.  Clients verify against them, a private replacement keeps them, a change of a bound or of an
// order rejects a dependent built against the old one, and tampered, incomplete or refined libraries are rejected.
// See Inputs/github-issue-266-m4.py.
// RUN: python3 "%S/Inputs/github-issue-266-m4.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%t.dir" "%repositoryRoot/Binaries/z3/bin/z3-5.1.0" > "%t"
// RUN: %diff "%s.expect" "%t"
