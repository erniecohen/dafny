// RUN: %exits-with 4 %verify "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %exits-with 4 %verify --type-system-refresh "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Opt<int> and Opt<bool> share DatatypeType. Unboxing three int payloads as bools
// must not identify their boxes with just the boxes of true and false.
datatype Opt<T> = None | Some(v: T)
function F(b: bool): int { if b then 1 else 0 }
lemma Bad(x: Opt<int>, y: Opt<int>, z: Opt<int>)
  requires x == Some(0) && y == Some(1) && z == Some(2)
  requires forall o: Opt<bool> :: o.Some? ==> F(o.v) <= 1
  ensures false
{
  assert x.v == 0 && y.v == 1 && z.v == 2;
}
