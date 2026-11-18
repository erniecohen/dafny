// --general-traits datatype: a datatype that extends a trait with an arrow-typed member, and a field whose
// result is the trait.  A trait-typed result gets no rank fact, so the recursion fails, and the member
// function value gives no cycle.

trait Tr { function Get(): nat -> Tr }
datatype D extends Tr = DLeaf | DNode(f: nat -> Tr) {
  function Get(): nat -> Tr { (n: nat) => this }
}
function Size(d: D): nat {
  if d.DLeaf? then 0 else var x := d.f(0); if x is D then Size(x as D) + 1 else 1
}
lemma Bad(d: D) requires d.DNode? ensures false {
  var g := d.Get();
  assert g(0) == d;
}
