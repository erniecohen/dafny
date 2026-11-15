// RUN: %exits-with 4 %verify --boogie /typeEncoding:m "%s" > "%t"
// RUN: %exits-with 4 %verify --boogie /typeEncoding:a "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/281): `hide *` hides the definitions of
// generic functions under /typeEncoding:m as it does under /typeEncoding:a.  Boogie's
// monomorphizer made each instance of a hideable definition axiom an ordinary axiom, and its
// pruning did not see that `reveal` of a declared function covers that function's instances.

function Two<T>(x: T): int { 2 }

function Three(x: int): int { 3 }

method Hidden() {
  hide *;
  assert Two(0) == 2; // error: the definition is hidden
}

method HiddenOther() {
  hide *;
  assert Two(true) == 2; // error: the definition is hidden
}

method HiddenMonomorphic() {
  hide *;
  assert Three(0) == 3; // error: the definition is hidden
}

method Revealed() {
  hide *;
  reveal Two;
  assert Two(0) == 2;
  assert Two(true) == 2;
}

method RevealedMonomorphic() {
  hide *;
  reveal Three;
  assert Three(0) == 3;
}

method NotHidden() {
  assert Two(0) == 2;
  assert Three(0) == 3;
}
