type Kv = nat->nat
type Positive = n: int | 0 < n witness 1

function Identity(kv: Kv): Kv { k => kv(k) }

function Choose(kv: Kv, other: Kv): Kv {
  k => if k == 0 then other(k) else kv(k)
}

function Pair(kv: (nat, nat)->nat): (nat, nat)->nat {
  (x, y) => kv(x, y)
}

function PositiveIdentity(kv: Positive->Positive): Positive->Positive {
  k => kv(k)
}

function Explicit(kv: Kv): Kv { (k: nat) => kv(k) }

function Generic<A, B>(f: A->B): A->B { x => f(x) }

lemma Use(kv: Kv) {
  var f := Identity(kv);
  assert f.requires(0);
  assert f(0) == kv(0);
}
