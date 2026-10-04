// Private unexecuted anti-vacuity control.
// Only the final assert false may fail; raw productive definitions must pass.
codatatype NatStream = N(head: nat, tail: NatStream)
function Count(n: nat): NatStream { N(n, Count(n + 1)) }
lemma DoesNotProveFalse(n: nat) {
  var s := Count(n);
  assert s.head == n;
  assert s.tail.head == n + 1;
  assert false;
}
