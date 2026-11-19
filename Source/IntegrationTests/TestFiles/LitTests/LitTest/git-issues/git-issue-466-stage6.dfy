// Archive integrity and group-boundary controls run in independent compiler processes.
// RUN: python3 "%repositoryRoot/.github/review/recursive-module-libraries.py" "%binaryDir/Dafny.dll" "%review-z3" "%S/Inputs/git-issue-466-stage6-library.dfy" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
