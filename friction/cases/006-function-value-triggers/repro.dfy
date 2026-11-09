// One box lemma, three formulations. The minimum (lo) and maximum (hi) of f
// over a box's corners bound f on the box; Common uses this at a point
// shared by two boxes.

// 1. Over a function value, one quantifier, an explicit trigger on lo only.
module OneTrigger {
  ghost function lo(f: (real,real) -> real, a1: real, b1: real, a2: real, b2: real): real {
    var m := if f(a1,a2) < f(a1,b2) then f(a1,a2) else f(a1,b2);
    var n := if f(b1,a2) < f(b1,b2) then f(b1,a2) else f(b1,b2);
    if m < n then m else n
  }
  ghost function hi(f: (real,real) -> real, a1: real, b1: real, a2: real, b2: real): real {
    var m := if f(a1,a2) > f(a1,b2) then f(a1,a2) else f(a1,b2);
    var n := if f(b1,a2) > f(b1,b2) then f(b1,a2) else f(b1,b2);
    if m > n then m else n
  }
  ghost predicate cornered(f: (real,real) -> real) {
    forall a1, b1, a2, b2, t1, t2 {:trigger lo(f,a1,b1,a2,b2), f(t1,t2)}
      | a1 <= t1 <= b1 && a2 <= t2 <= b2 :: lo(f,a1,b1,a2,b2) <= f(t1,t2) <= hi(f,a1,b1,a2,b2)
  }
  lemma Common(f: (real,real) -> real, a1: real, b1: real, a2: real, b2: real, c1: real, d1: real, c2: real, d2: real)
    requires cornered(f) && a1 < b1 && a2 < b2 && c1 < d1 && c2 < d2 && a1 < d1 && c1 < b1 && a2 < d2 && c2 < b2
    ensures lo(f,c1,d1,c2,d2) <= hi(f,a1,b1,a2,b2)
  {
    var t1 := if d1 < b1 then d1 else b1;
    var t2 := if d2 < b2 then d2 else b2;
    assert lo(f,c1,d1,c2,d2) <= f(t1,t2) <= hi(f,a1,b1,a2,b2);
  }
  lemma Sum() ensures cornered((a,b) => a+b) {}
  lemma Product() ensures cornered((a,b) => a*b) {}
}

// 2. Over a function value, one quantifier per bound, each with its own trigger.
module TwoTriggers {
  ghost function lo(f: (real,real) -> real, a1: real, b1: real, a2: real, b2: real): real {
    var m := if f(a1,a2) < f(a1,b2) then f(a1,a2) else f(a1,b2);
    var n := if f(b1,a2) < f(b1,b2) then f(b1,a2) else f(b1,b2);
    if m < n then m else n
  }
  ghost function hi(f: (real,real) -> real, a1: real, b1: real, a2: real, b2: real): real {
    var m := if f(a1,a2) > f(a1,b2) then f(a1,a2) else f(a1,b2);
    var n := if f(b1,a2) > f(b1,b2) then f(b1,a2) else f(b1,b2);
    if m > n then m else n
  }
  ghost predicate cornered(f: (real,real) -> real) {
    && (forall a1, b1, a2, b2, t1, t2 {:trigger lo(f,a1,b1,a2,b2), f(t1,t2)}
         | a1 <= t1 <= b1 && a2 <= t2 <= b2 :: lo(f,a1,b1,a2,b2) <= f(t1,t2))
    && (forall a1, b1, a2, b2, t1, t2 {:trigger hi(f,a1,b1,a2,b2), f(t1,t2)}
         | a1 <= t1 <= b1 && a2 <= t2 <= b2 :: f(t1,t2) <= hi(f,a1,b1,a2,b2))
  }
  lemma Common(f: (real,real) -> real, a1: real, b1: real, a2: real, b2: real, c1: real, d1: real, c2: real, d2: real)
    requires cornered(f) && a1 < b1 && a2 < b2 && c1 < d1 && c2 < d2 && a1 < d1 && c1 < b1 && a2 < d2 && c2 < b2
    ensures lo(f,c1,d1,c2,d2) <= hi(f,a1,b1,a2,b2)
  {
    var t1 := if d1 < b1 then d1 else b1;
    var t2 := if d2 < b2 then d2 else b2;
    assert lo(f,c1,d1,c2,d2) <= f(t1,t2) <= hi(f,a1,b1,a2,b2);
  }
}

// 3. First-order: an abstract function and an instantiation lemma.
abstract module Box {
  function f(a: real, b: real): real
  function lo(a1: real, b1: real, a2: real, b2: real): real {
    var m := if f(a1,a2) < f(a1,b2) then f(a1,a2) else f(a1,b2);
    var n := if f(b1,a2) < f(b1,b2) then f(b1,a2) else f(b1,b2);
    if m < n then m else n
  }
  function hi(a1: real, b1: real, a2: real, b2: real): real {
    var m := if f(a1,a2) > f(a1,b2) then f(a1,a2) else f(a1,b2);
    var n := if f(b1,a2) > f(b1,b2) then f(b1,a2) else f(b1,b2);
    if m > n then m else n
  }
  lemma cornered(a1: real, b1: real, a2: real, b2: real, t1: real, t2: real)
    requires a1 <= t1 <= b1 && a2 <= t2 <= b2
    ensures lo(a1,b1,a2,b2) <= f(t1,t2) <= hi(a1,b1,a2,b2)
  lemma Common(a1: real, b1: real, a2: real, b2: real, c1: real, d1: real, c2: real, d2: real)
    requires a1 < b1 && a2 < b2 && c1 < d1 && c2 < d2 && a1 < d1 && c1 < b1 && a2 < d2 && c2 < b2
    ensures lo(c1,d1,c2,d2) <= hi(a1,b1,a2,b2)
  {
    var t1 := if d1 < b1 then d1 else b1;
    var t2 := if d2 < b2 then d2 else b2;
    cornered(c1,d1,c2,d2,t1,t2); cornered(a1,b1,a2,b2,t1,t2);
  }
}
