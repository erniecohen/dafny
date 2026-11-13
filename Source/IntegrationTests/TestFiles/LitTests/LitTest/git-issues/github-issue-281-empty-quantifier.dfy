// RUN: %exits-with 4 %verify --type-system-refresh=false --general-newtypes=false --boogie /typeEncoding:m "%s" > "%t"
// RUN: %exits-with 4 %verify --type-system-refresh=false --general-newtypes=false --boogie /typeEncoding:a "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/281): a labeled assertion is assumed
// through a quantifier that binds no variable.  Boogie's monomorphizer kept that quantifier,
// the solver rejected it, and every assertion of the program was reported as an error.
// Under /typeEncoding:m the program must verify as it does under /typeEncoding:a.

predicate P(x: int)

function FirstAssertNotProved(): (i: int)
  ensures P(i)
{
  assert rule2: P(12) by { // error: cannot be verified here
  }
  assert P(12) by {
    reveal rule2;
  }
  12
}

function EverythingVerifies(): (i: int)
  requires rule: P(12)
  ensures P(i)
{
  assert rule2: P(12) by {
    reveal rule;
  }
  assert P(12) by {
    reveal rule2;
  }
  12
}

least predicate ShouldNotWork(): (y: bool)
  requires rule: P(12)
{
  assert P(12) by { // error: cannot be verified here
  }
  true
}

least predicate ShouldWork(): (y: bool)
  requires rule: P(12)
{
  assert P(12) by {
    reveal rule;
  }
  true
}
