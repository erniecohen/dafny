// NONUNIFORM: checks existing C# run build-alias output placement and runtime configurations.
// RUN: python3 "%S/Inputs/github-issue-104-build-boogie.py" "%repositoryRoot/Binaries/net8.0/DafnyDriver.dll" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
