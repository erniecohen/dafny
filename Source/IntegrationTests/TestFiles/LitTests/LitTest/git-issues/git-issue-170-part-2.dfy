// NONUNIFORM: bounded maintained-proof part 3/6, independent helpers, and option compatibility.
// RUN: python3 "%S/Inputs/obligation-proof-repairs/registered.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%repositoryRoot/Binaries/z3/bin/z3-5.1.0" "%t.dir" --full-files --shard 2/6 > "%t"
// RUN: %diff "%S/git-issue-170.dfy.expect" "%t"
