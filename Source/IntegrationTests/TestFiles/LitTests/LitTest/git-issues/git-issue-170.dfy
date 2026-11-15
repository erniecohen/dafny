// NONUNIFORM: bounded maintained-proof part 1/12, independent helpers, and option compatibility.
// RUN: python3 "%S/Inputs/obligation-proof-repairs/registered.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%repositoryRoot/Binaries/z3/bin/z3-5.1.0" "%t.dir" --full-files --shard 0/12 > "%t"
// RUN: %diff "%S/git-issue-170.dfy.expect" "%t"
