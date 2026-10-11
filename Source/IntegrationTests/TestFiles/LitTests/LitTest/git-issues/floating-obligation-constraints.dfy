// NONUNIFORM: native float constraint and non-vacuity controls.
// RUN: python3 "%S/Inputs/floating-obligations/registered.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%repositoryRoot/Binaries/z3/bin/z3-5.1.0" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
