// The cycle that a ~> field admits, through the heap: ~> fields get no rank fact.

datatype H = HLeaf | HNode(f: nat ~> H)
class C { var t: H }
method Bad(c: C) modifies c ensures false {
  var f := (n: nat) reads c => c.t;
  c.t := HNode(f);
  assert f.requires(0) && f(0) == HNode(f);
  assert f(0) == c.t;
}
ghost function HSize(h: H): nat {
  if h.HLeaf? || !h.f.requires(0) || h.f.reads(0) != {} then 0 else HSize(h.f(0)) + 1
}
