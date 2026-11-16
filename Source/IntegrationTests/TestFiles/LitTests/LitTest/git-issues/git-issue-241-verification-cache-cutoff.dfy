// Fresh processes require the external CLI rather than the singleton verdict planner.
// Split from git-issue-241-verification-cache.dfy so that each part finishes well inside
// the lit timeout (https://github.com/erniecohen/dafny/issues/389).
// RUN: python3 "%repositoryRoot/.github/review/verification-cache-cutoff.py" "%binaryDir/Dafny.dll" "%review-z3" "%S/Inputs/git-issue-241-cache-cutoff.dfy" "%t.cutoff" > "%t"
// RUN: %diff "%s.expect" "%t"
// UNSUPPORTED: windows

// https://github.com/erniecohen/dafny/issues/241
// The program is Inputs/git-issue-241-cache-cutoff.dfy.
