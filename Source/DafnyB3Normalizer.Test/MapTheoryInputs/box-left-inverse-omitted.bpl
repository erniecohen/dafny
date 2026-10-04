// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
type Box;
function $Box<T>(x: T): Box;
function $Unbox<T>(b: Box): T;
axiom (forall<T> x: T :: $Unbox($Box(x)) == x);
procedure P(m: [int]Box);
implementation P(m: [int]Box) {
  assert m[0 := $Box(1)][0] == $Box(1);
  assert $Unbox(m[0 := $Box(1)][0]) == 1;
}
