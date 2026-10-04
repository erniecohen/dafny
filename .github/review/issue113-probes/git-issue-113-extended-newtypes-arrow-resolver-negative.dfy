// Private draft; not registered or executed. EXPECT_FAIL comments are desired
// resolver boundaries, not measured diagnostics. Keep separate from proof tests.
// Intended flags: --type-system-refresh=true --general-newtypes=true --extended-newtype-bases

newtype Total = int -> int witness ((x: int) => x)
newtype Other = int -> int witness ((x: int) => x)
newtype Nonnegative = n: int | 0 <= n witness 0
newtype Domain = Nonnegative -> int witness ((x: Nonnegative) => x as int)

// EXPECT_FAIL: output position cannot satisfy contravariance.
newtype BadOutput<-T> = () -> T witness *
// EXPECT_FAIL: input position cannot satisfy covariance.
newtype BadInput<+T> = T -> int witness *
// EXPECT_FAIL: predicate stability is not supplied by a covariant base.
newtype Every<+T> = f: () -> T | (forall x: T :: x == f()) witness *

method Nominality(f: int -> int, n: Total, xs: seq<Total>) {
  var a: Total := f; // EXPECT_FAIL: explicit introduction required
  var b: Other := n; // EXPECT_FAIL: distinct nominal declarations
  var c: seq<int -> int> := xs; // EXPECT_FAIL: no container structural coercion
  var d: Total := n(0); // EXPECT_FAIL: application result is the base int
}

method WrongDomain(f: Domain, x: int) {
  var result := f(x); // EXPECT_FAIL: base domain remains Nonnegative
}

method WrongArity(f: Total) {
  var result := f(1, 2); // EXPECT_FAIL: base arity is one
}

method BadCompiledEquality(a: Total, b: Total) returns (same: bool) {
  same := a == b; // EXPECT_FAIL: nominal name does not add arrow equality
}

module Library {
  export Visible reveals Hidden
  export Opaque provides Hidden
  newtype Hidden = int -> int witness ((x: int) => x)
}

module OpaqueClient {
  import opened L = Library`Opaque
  method NoBaseApplicability(f: Hidden) {
    var result := f(0); // EXPECT_FAIL: hidden base is not exposed
    var precondition := f.requires(0); // EXPECT_FAIL: inherited base member is hidden
  }
}
