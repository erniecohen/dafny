// Proposed extension; outcomes UNVERIFIED. Requires the same options/milestone
// as q2-positive.dfy. Each bad_* member must fail its stated precondition.
// false_control must independently fail at assert false; no assumptions used.
datatype V = V(xs: seq<int>) {
  function {:operator "[:=]"} put(i: int, v: int): V
    requires 0 <= i < |xs|
  { V(xs[i := v]) }

  function {:operator "[_.._]"} between(lo: int, hi: int): V
    requires 0 <= lo <= hi <= |xs|
  { V(xs[lo..hi]) }

  function {:operator "[_..]"} from(lo: int): V
    requires 0 <= lo <= |xs|
  { V(xs[lo..]) }

  function {:operator "[.._]"} until(hi: int): V
    requires 0 <= hi <= |xs|
  { V(xs[..hi]) }

  function {:operator "[..]"} all(): V
    requires 0 < |xs|
  { this }
}

method bad_update() {
  var x := V([10]);
  var y := x[1 := 20]; // FAIL: put requires 1 < 1.
}

method bad_between() {
  var x := V([10]);
  var y := x[1..0]; // FAIL: between requires lo <= hi.
}

method bad_from() {
  var x := V([10]);
  var y := x[2..]; // FAIL: from requires lo <= |xs|.
}

method bad_until() {
  var x := V([10]);
  var y := x[..2]; // FAIL: until requires hi <= |xs|.
}

method bad_all() {
  var x := V([]);
  var y := x[..]; // FAIL: omitted bounds add no implicit domain guarantee.
}

lemma false_control() {
  var x := V([10]);
  assert x.put(0, 20).xs == [20];
  assert false; // Expected verifier failure, independent of bad_* obligations.
}
