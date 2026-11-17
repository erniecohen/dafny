// RUN: %exits-with 0 %baredafny resolve --type-system-refresh --use-basename-for-filename --show-snippets:false --standard-libraries:false "%s" > "%t"
// RUN: %exits-with 0 %baredafny resolve --type-system-refresh:false --use-basename-for-filename --show-snippets:false --standard-libraries:false "%s" >> "%t"
// RUN: %exits-with 0 %baredafny verify --type-system-refresh --use-basename-for-filename --show-snippets:false --standard-libraries:false --solver-path "%review-z3" "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

// Hiding or revealing a static function or constant through an object used to crash
// the resolver under --type-system-refresh (https://github.com/erniecohen/dafny/issues/416).

class Foo {
  static function Q(x: int): bool { true }
  static opaque function P(x: int): int { x + 1 }
  static opaque function G<T>(x: T): T { x }
  static opaque const K: int := 3
}

class Bar<T> {
  static opaque function R(x: int): int { x }
}

datatype D = D {
  static opaque function S(): int { 5 }
}

method RevealThroughAnObject(foo: Foo, bar: Bar<bool>, d: D) {
  reveal foo.Q;
  reveal foo.P, foo.G, foo.K;
  reveal bar.R;
  reveal d.S();
  assert Foo.P(1) == 2;
  assert Foo.G(true);
  assert Foo.K == 3;
  assert Bar<bool>.R(4) == 4;
  assert D.S() == 5;
}

method HideThenRevealThroughAnObject(foo: Foo) {
  hide foo.Q;
  reveal foo.Q;
  assert Foo.Q(0);
}
