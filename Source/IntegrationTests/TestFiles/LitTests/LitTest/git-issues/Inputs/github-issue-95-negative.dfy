class O { }
function {:axiom} Pure(): set<O>
function Id(o: O): O { o }
function {:axiom} Forbidden(): set<O> requires false

method FalseControl() {
  assert forall o: O | o in Pure() :: allocated(o);
  assert false;
}

method FreshReference() {
  var n := new O;
  assert old(allocated(Id(n)));
}

method UnallocatedArgument() {
  var n := new O;
  assert old(Id(n)) == n;
}

method InvalidCall() {
  assert forall o: O | o in Forbidden() :: allocated(o);
}
