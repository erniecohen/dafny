// RUN: python3 "%repositoryRoot/.github/review/issue193-newtype-operators.py" --dafny "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" --z3 "%review-z3" --output "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
// https://github.com/erniecohen/dafny/issues/193
// Direct integer-reference lowering, both resolvers, full result membership,
// source-count bounds, opacity, model clients and conclusive live false controls.
