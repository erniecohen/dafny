// RUN: %exits-with 1 dotnet "%binaryDir/DafnyB3.Worker.dll" --protocol-v1 < "%S/Inputs/git-issue-119-request.json" > "%t"
// RUN: %diff "%s.expect" "%t"
// The transport scaffold must explicitly reject every obligation until an adapter is admitted.
// This does not invoke an alternative Dafny verification backend.
