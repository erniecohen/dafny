// NONUNIFORM: checks CLI/project precedence and backend-specific output filenames.
// RUN: python3 "%S/Inputs/github-issue-104.py" "%repositoryRoot/Binaries/DafnyDriver.dll" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
