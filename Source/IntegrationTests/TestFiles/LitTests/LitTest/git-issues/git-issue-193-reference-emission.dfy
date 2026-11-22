// RUN: python3 "%repositoryRoot/.github/review/issue193-fixed-references.py" --dafny "%repositoryRoot/Binaries/net10.0/DafnyDriver.dll" --z3 "%review-z3" --output "%t.dir" > "%t"
// RUN: %diff "%s.expect" "%t"
// https://github.com/erniecohen/dafny/issues/193
// The driver checks actual emitted equations, independently checked bootstrap
// proofs, live false/wrong-result controls, finite SAT slices, identity, and
// independence from the existing user-operator option.
