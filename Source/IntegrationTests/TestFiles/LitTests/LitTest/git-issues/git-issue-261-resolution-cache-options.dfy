// RUN: python3 "%repositoryRoot/.github/review/resolution-cache-options.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
// UNSUPPORTED: windows
// https://github.com/erniecohen/dafny/issues/261
method M() {}
