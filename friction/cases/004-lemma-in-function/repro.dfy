lemma Find(n: nat) returns (m: nat) ensures m > n { m := n+1; }
ghost function Above(n: nat): nat { var m := Find(n); m }
