lemma Find(n: nat) returns (m: nat) ensures m > n { m := n+1; }

// A second lemma states existence, and the function calls it and picks a
// witness. The named predicate gives the existential a trigger, so the
// witness from Find proves it.
ghost predicate Bigger(n: nat, m: nat) { m > n }
lemma FindExists(n: nat) ensures exists m: nat :: Bigger(n,m) {
  var m := Find(n); assert Bigger(n,m);
}
ghost function Above(n: nat): nat { FindExists(n); var m: nat :| Bigger(n,m); m }
