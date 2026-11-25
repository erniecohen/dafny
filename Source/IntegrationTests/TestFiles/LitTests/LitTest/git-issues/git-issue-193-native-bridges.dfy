// RUN: python3 "%repositoryRoot/.github/review/issue193-native-bridges.py" --dafny "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" --z3 "%review-z3" --output "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
// https://github.com/erniecohen/dafny/issues/193
// Checked source proofs, native and composed callers, wrong equations,
// inhabited false controls and invalid conversions, with both resolvers.
