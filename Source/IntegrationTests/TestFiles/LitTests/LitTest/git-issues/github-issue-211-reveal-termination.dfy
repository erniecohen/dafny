// Generated function reveals only change visibility; constant reveals still evaluate their definitions.
// RUN: python3 "%S/Inputs/github-issue-211-reveal-termination.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%review-z3" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
