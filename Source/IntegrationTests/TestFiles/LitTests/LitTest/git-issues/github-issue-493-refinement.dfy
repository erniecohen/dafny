// Refined bounded bodies must check inherited calls in their current context.
// RUN: python3 "%S/Inputs/github-issue-493-refinement.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%repositoryRoot/Binaries/z3/bin/z3-5.1.0" "%t.controls" > "%t"
// RUN: %diff "%s.expect" "%t"
