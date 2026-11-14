// Vacuity controls: each body triggers the rank fact for an arrow-typed field, and none of them may
// prove false.

datatype Tree = Leaf | Node(kids: nat -> Tree)
datatype PTree = PLeaf | PNode(g: nat --> PTree)
datatype Ord = Zero | Succ(pred: Ord) | Lim(f: nat -> Ord)
datatype G<X> = GLeaf(x: X) | GNode(h: nat -> G<X>)
datatype SeqT = SL | SN(s: nat -> seq<SeqT>)
datatype MapT = PL | PN(m: nat -> map<int, MapT>)

function Size(t: Tree): nat {
  if t.Leaf? then 0 else Size(t.kids(0)) + 1
}

lemma FalseFromSize(t: Tree)
  requires t.Node?
  ensures false
{
  assert Size(t.kids(0)) < Size(t) || true;
  assert t.kids(0) < t;
}

lemma FalseFromPartial(t: PTree)
  requires t.PNode? && t.g.requires(0)
  ensures false
{
  assert t.g(0) < t;
}

// Every function into Ord, made into a limit: the limit is above all its values.
opaque function Up(n: nat): Ord { if n == 0 then Zero else Succ(Up(n - 1)) }

lemma FalseFromLimit(n: nat)
  ensures false
{
  var w := Lim(Up);
  assert Up(n) < w;
  assert Up(n + 1) < w;
}

// A limit whose branches include the limit's own branches.
lemma FalseFromNestedLimit(f: nat -> Ord)
  ensures false
{
  var w := Lim(f);
  var v := Lim((n: nat) => if n == 0 then w else f(n - 1));
  assert v.f(0) == w;
  assert w < v;
  assert f(0) < w;
}

lemma FalseFromGeneric(t: G<int>)
  requires t.GNode?
  ensures false
{
  assert t.h(3) < t;
}

lemma FalseFromSeq(t: SeqT)
  requires t.SN? && |t.s(0)| > 0
  ensures false
{
  assert t.s(0)[0] < t;
}

lemma FalseFromMap(t: MapT)
  requires t.PN? && 7 in t.m(0)
  ensures false
{
  assert t.m(0)[7] < t;
}

// A precondition that only an impossible value meets: the lemma verifies, but no caller can call it.
lemma Impossible(f: nat -> Tree)
  requires f(0) == Node(f)
  ensures false
{
  assert f(0) < Node(f);
}

method CallImpossible() {
  var f: nat -> Tree := (n: nat) => Leaf;
  Impossible(f);
  assert false;
}
