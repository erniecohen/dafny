// Immutable checked-Std allowances bound fresh producers, independently of virtual local metrics.
// RUN: echo "the pre-type resolver" > "%t"
// RUN: python3 "%S/Inputs/github-issue-211-produced-allowances.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%review-z3" "%t.dir" >> "%t"
// RUN: echo "the legacy resolver" >> "%t"
// RUN: python3 "%S/Inputs/github-issue-211-produced-allowances.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%review-z3" "%t.legacy.dir" --legacy >> "%t"
// RUN: %diff "%s.expect" "%t"
