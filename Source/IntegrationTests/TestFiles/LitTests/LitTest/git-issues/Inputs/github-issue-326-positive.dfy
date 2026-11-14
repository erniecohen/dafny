// Uses of the rank fact for arrow-typed constructor fields.

// Countable ordinals as Brouwer trees.
datatype Ord = Zero | Succ(pred: Ord) | Lim(f: nat -> Ord)

predicate Finite(o: Ord) {
  match o
  case Zero => true
  case Succ(p) => Finite(p)
  case Lim(_) => false
}

// Structural induction through the limit.
function Add(a: Ord, b: Ord): Ord
  decreases b
{
  match b
  case Zero => a
  case Succ(p) => Succ(Add(a, p))
  case Lim(f: nat -> Ord) => Lim((n: nat) => Add(a, f(n)))
}

// The number of steps along the path that always takes branch n.
function Count(o: Ord, n: nat): nat {
  match o
  case Zero => 0
  case Succ(p) => Count(p, n) + 1
  case Lim(f: nat -> Ord) => Count(f(n), n) + 1
}

// Induction through the limit.
lemma CountAddZero(b: Ord, n: nat)
  ensures Count(Add(Zero, b), n) == Count(b, n)
{
  match b
  case Zero =>
  case Succ(p) => CountAddZero(p, n);
  case Lim(f: nat -> Ord) => CountAddZero(f(n), n);
}

// Interaction trees.
datatype Prog = Ret(v: int) | Read(k: int -> Prog) | Write(out: int, next: Prog)

function Run(p: Prog, input: seq<int>): int
  decreases p
{
  match p
  case Ret(v) => v
  case Read(k: int -> Prog) => if |input| == 0 then 0 else Run(k(input[0]), input[1..])
  case Write(_, next) => Run(next, input)
}

// Two arguments.
datatype T2 = L2 | N2(g: (int, bool) -> T2)
function D2(t: T2): nat {
  if t.L2? then 0 else D2(t.g(3, true)) + 1
}

// No arguments.
datatype T0 = L0 | N0(g: () -> T0)
function D0(t: T0): nat {
  if t.L0? then 0 else D0(t.g()) + 1
}

// Partial arrows, under their precondition.
datatype PTree = PLeaf | PNode(g: nat --> PTree)
ghost function PDepth(t: PTree, n: nat): nat {
  if t.PLeaf? then 0
  else if t.g.requires(n) then PDepth(t.g(n), n) + 1
  else 1
}

// A type parameter in the result.
datatype G<X> = GLeaf(x: X) | GNode(h: nat -> G<X>)
function GCount<X>(t: G<X>, n: nat): nat {
  match t
  case GLeaf(_) => 0
  case GNode(h: nat -> G<X>) => GCount(h(n), n) + 1
}

datatype Wrap<X> = Wrap(w: nat -> X)
datatype UsesWrap = UL | UW(Wrap<UsesWrap>)
lemma WrapRank(x: Wrap<int>, n: nat) ensures true {}

// Mutual recursion.
datatype A = AL | AN(fa: nat -> B)
datatype B = BL | BN(fb: nat -> A)
function CA(a: A): nat {
  if a.AL? then 0 else CB(a.fa(0)) + 1
}
function CB(b: B): nat {
  if b.BL? then 0 else CA(b.fb(1)) + 1
}

// Containers in the result.
datatype SeqT = SL | SN(s: nat -> seq<SeqT>)
function SeqSize(t: SeqT): nat {
  if t.SL? || |t.s(0)| == 0 then 0 else SeqSize(t.s(0)[0]) + 1
}
datatype MapT = PL | PN(m: nat -> map<int, MapT>)
function MapValueDepth(t: MapT, k: int): nat {
  if t.PL? || k !in t.m(0) then 0 else MapValueDepth(t.m(0)[k], k) + 1
}
datatype IMapT = IL | IN(m: nat -> imap<nat, IMapT>)
function IMapValueDepth(t: IMapT, k: nat): nat {
  if t.IL? || k !in t.m(k) then 0 else IMapValueDepth(t.m(k)[k], k) + 1
}

// A subset type whose chain reaches the total arrow.
type Kids<T> = f: nat -> T | true witness *
datatype Tree = Leaf | Node(kids: Kids<Tree>)
function Size(t: Tree): nat {
  if t.Leaf? then 0 else Size(t.kids(0)) + 1
}

// Through a variable of a supertype.
function Size2(t: Tree): nat {
  if t.Leaf? then 0 else
  var g: nat ~> Tree := t.kids;
  Size2(g(0)) + 1
}

// The rank comparison operator.
lemma Below(t: Tree, n: nat)
  requires t.Node?
  ensures t.kids(n) < t
{
}
