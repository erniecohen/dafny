// Private source-only draft; finite witnesses are explicit.
// Intended proof rejection is issue141 tuple target membership, not finiteness.
ghost function InvalidTupleMap(): map<(nat, nat), int> {
  map i: int | i in {-1, 0} :: ((0, i) as (nat, nat)) := i
}

lemma OrdinaryTupleCastControl(i: int)
  requires i < 0
{
  var invalid := (0, i) as (nat, nat);
}
