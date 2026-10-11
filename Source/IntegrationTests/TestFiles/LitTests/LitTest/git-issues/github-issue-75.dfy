// NONUNIFORM: build SDK and generated C# framework compatibility.
// RUN: python3 "%S/Inputs/github-issue-75.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
