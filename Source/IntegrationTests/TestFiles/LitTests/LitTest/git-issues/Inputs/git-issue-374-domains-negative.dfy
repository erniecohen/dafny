include "git-issue-374-domains-decls.dfy"

lemma WiderDomain() {
  var f: (nat, nat) -> int := (x: int, y: int) => if x < 0 then 7 else x + y;
  var g: (nat, nat) -> int := (x: int, y: int) => x + y;
  assert D2.Equal<nat, nat, int>(f, g);
}
lemma Distinct() {
  assert D2.Equal((x: int, y: bool) => 0, (x: int, y: bool) => 1);
}
lemma Generic<A(!new), B(!new), R>(f: (A, B) -> R, g: (A, B) -> R)
  requires forall a, b :: f(a, b) == g(a, b)
  ensures f == g
{
  assert D2.Equal(f, g);
}
lemma NoCall(f: (int, bool) -> int, g: (int, bool) -> int)
  requires forall x, y :: f(x, y) == g(x, y)
  ensures f == g
{}
lemma ExactWider() {
  var f: (nat, bool) -> int := (x: int, y: bool) => x;
  assert D2.ExactDom<nat, bool, int>(f);
}
lemma FalseAfterNullary() {
  var f := () => 0;
  assert D0.ExactDom(D0.Restrict(f));
  assert D0.Equal(D0.Restrict(f), f);
  assert false;
}
lemma FalseAfterBinary() {
  var f := (x: nat, b: bool) => x;
  assert D2.ExactDom<nat, bool, int>(f);
  assert D2.Equal<nat, bool, int>(D2.Restrict<nat, bool, int>(f), f);
  assert false;
}
lemma FalseAfterFive() {
  var f := (a: int, b: bool, c: nat, d: real, e: int) => a;
  assert D5.ExactDom<int, bool, nat, real, int, int>(f);
  assert D5.Equal<int, bool, nat, real, int, int>(D5.Restrict<int, bool, nat, real, int, int>(f), f);
  assert false;
}

datatype D = One | Two
lemma FalseAfterDatatype() {
  var f := (x: D, y: nat) => if x.One? then y else 0;
  var g := (x: D, y: nat) => if x.Two? then 0 else y;
  assert D2.ExactDom<D, nat, int>(f) && D2.ExactDom<D, nat, int>(g);
  assert D2.Equal<D, nat, int>(f, g);
  assert false;
}
type Empty = x: int | false witness *
lemma FalseAfterEmptyProduct() {
  var f: (Empty, Empty) -> int := (x: Empty, y: int) => 0;
  var g: (Empty, Empty) -> int := (x: int, y: Empty) => 1;
  assert D2.Equal<Empty, Empty, int>(f, g);
  assert false;
}
