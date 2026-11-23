// RUN: python3 "%repositoryRoot/.github/review/cache-requires-refresh.py" "%binaryDir/Dafny.dll" "%review-z3" "%s" "%t.dir" --cache-dir > "%t"
// RUN: %diff "%s.expect" "%t"
// UNSUPPORTED: windows
// https://github.com/erniecohen/dafny/issues/241
method M() {}
