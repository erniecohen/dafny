// NONUNIFORM: opt-in obligation checks, false controls, and project option precedence.
// The registered regression is split over git-issue-100.dfy and git-issue-100-shard{1,2}.dfy,
// each running every third section of the script, so that each stays well under the per-test
// timeout (https://github.com/erniecohen/dafny/issues/229).  This one runs sections 2, 5, ... .
// RUN: python3 "%S/Inputs/assertion-invariance/registered-regression.py" "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" "%repositoryRoot/Binaries/z3/bin/z3-5.1.0" "%t.dir" 2/3 > "%t"
// RUN: %diff "%s.expect" "%t"
