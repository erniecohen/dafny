// Vacuity controls: allocation of function results is in play in each method, and none may verify.
class O { const f: nat }

function F(): set<O>
function G(n: int): set<O>

method Vacuity(n: int) {
  assert forall o | o in F() :: allocated(o);
  assert forall o | o in G(n) :: allocated(o);
  assert false;
}

method Unrelated(n: int) {
  assert forall o | o in F() :: allocated(o);
  assert forall o | o in G(n) :: o in F();
}

method Member(n: int) {
  var x := new O;
  assert forall o | o in G(n) :: allocated(o);
  assert x in G(n);
}

method Positive(n: int) {
  assert forall o | o in G(n) :: allocated(o);
  assert forall o | o in G(n) :: o.f > 0;
}
