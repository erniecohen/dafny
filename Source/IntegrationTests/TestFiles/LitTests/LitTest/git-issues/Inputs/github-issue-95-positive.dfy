class O { }
class Cell { var objects: set<O> }
function {:axiom} Pure(o: O): set<O>
function {:axiom} Generic<T>(x: T): seq<T>
function {:axiom} Mapping(o: O): map<int, O>
function {:axiom} Reading(c: Cell): set<O> reads c
function {:axiom} Conditional(o: O): set<O> requires o != null

twostate function Both(o: O, new n: O): set<O> reads o, n { {o, n} }

method Positive(o: O, c: Cell) {
  assert forall v: O | v in Pure(o) :: allocated(v);
  assert forall v: O | v in Generic(o) :: allocated(v);
  assert forall i: int | i in Mapping(o) :: allocated(Mapping(o)[i]);
  assert forall v: O | v in Reading(c) :: allocated(v);
  assert forall v: O | v in Conditional(o) :: allocated(v);
  assert old(forall v: O | v in Pure(o) :: allocated(v));
  var n := new O;
  assert forall v: O | v in Both(o, n) :: allocated(v);
}
