type Kv = nat->nat
datatype T = T(m:map<nat,nat>) {
  function apply(kv:Kv):Kv {
    k => if k in m then m[k] else kv(k)
  }
}
