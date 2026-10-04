// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
type Box;
function $Box<T>(x: T): Box;
function $Unbox<T>(b: Box): T;
axiom (forall<T> x: T :: $Unbox($Box(x)) == x);
procedure P(m: [int]Box, b: Box);
implementation P(m: [int]Box, b: Box) {
  assert m[0 := b][0] == b;
  assert $Box(($Unbox(b): int)) == b;
}
