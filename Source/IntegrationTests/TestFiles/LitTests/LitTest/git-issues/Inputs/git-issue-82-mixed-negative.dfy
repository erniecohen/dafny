// The finite definition is active beside a legal infinite iset.
lemma Mixed(n: nat) {
  var inf := iset x: int | 0 <= x;
  var fin := set x: int | 0 <= x < n;
  assert n in inf;
  assert n !in fin;
  assert false; // intended sole failing obligation
}
