// NONUNIFORM: maintained obligation proofs, independent helpers, and option compatibility.
// RUN: python3 "%S/Inputs/obligation-proof-repairs/registered.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%repositoryRoot/Binaries/z3/bin/z3-5.1.0" "%t.dir" --full-files > "%t"
// RUN: %diff "%s.expect" "%t"
