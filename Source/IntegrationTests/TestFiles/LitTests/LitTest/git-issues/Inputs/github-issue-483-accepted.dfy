// Part of github-issue-483.dfy: stores that the check accepts.

datatype Pack<T> = Pack(ghost c: T)

method NoReads(p: Pack<() -> bool>) returns (q: Pack<() -> bool>) ensures q == p { q := p; }
method Num() returns (n: int) ensures n == 3 { n := 3; }

// A call result whose type has no reading function, stored in a location whose type has one
method IntoReadingLocation(r: array<Pack<() ~> bool>>) requires r.Length != 0 modifies r {
  r[0] := NoReads(Pack(() => false));
}

// Stores of values without functions, into multi-dimensional arrays, arrays and fields
class Cell { var v: int  constructor () { v := 0; } }
method Plain(m: array2<int>, a: array<int>, c: Cell)
  requires m.Length0 > 0 && m.Length1 > 0 && a.Length > 0
  modifies m, a, c
{
  m[0, 0] := 1;
  m[0, 0] := Num();
  a[0] := Num();
  c.v := Num();
}

// Total functions, which read nothing, may be stored in memory
method Total(m: array2<int -> int>, f: int -> int) requires m.Length0 > 0 && m.Length1 > 0 modifies m {
  m[0, 0] := f;
}

// A call result stored in a local variable
method Local() {
  var p := NoReads(Pack(() => true));
  var f: () ~> bool := () reads {} => true;
}
