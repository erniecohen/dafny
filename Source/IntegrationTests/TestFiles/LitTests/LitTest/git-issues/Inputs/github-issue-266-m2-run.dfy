// A stored bounded value, compiled: the compilers erase "bounded.certify(a, f)" to "f".
class C {
  const cb: bounded<nat, (C, int) -> int>
  const k: int
  constructor (h: bounded<nat, (C, int) -> int>, k: int) ensures cb == h { cb := h; this.k := k; }
  function Use(x: int): int
    requires cb.bound < 3
    decreases {:global} 3
  {
    cb(this, x)
  }
}
method Main() {
  var h := bounded.certify(1, (c: C, x: int) => c.k + x);
  var c := new C(h, 40);
  print c.Use(2), " ", h(c, 1), " ", h.erase(c, 0), "\n";
}
