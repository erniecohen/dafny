// NONUNIFORM: opt-in obligation checks, false controls, and project option precedence.
// RUN: python3 "%S/Inputs/assertion-invariance/registered-regression.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%repositoryRoot/Binaries/z3/bin/z3-5.1.0" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
