// Certificate production, admission and bounded applications need no migration flag.
// RUN: python3 "%S/Inputs/github-issue-211-by-use.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%repositoryRoot/Binaries/z3/bin/z3-5.1.0" "%t.controls" > "%t"
// RUN: %diff "%s.expect" "%t"
