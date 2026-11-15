// RUN: %verify --type-system-refresh "%s" > "%t"
// RUN: %verify --type-system-refresh:false --general-traits:legacy --general-newtypes:false "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/327): under --type-system-refresh, a variable
// bound by a case pattern of a match statement or expression gets the declared type of the constructor's
// formal, with the source's type arguments substituted, as a let-pattern variable does and as the legacy
// resolver does.  The refreshed resolver used to give an arrow-typed field such as "nat -> Tree" the type
// "int ~> Tree", which is not a supertype of it, so the binding failed a subset check and every call
// through the variable failed its precondition and reads checks.  Both resolvers verify this file.

type Pos = x: int | x > 0 witness 1
datatype Tree = Leaf | Node(kids: nat -> Tree)
datatype PTree = PLeaf | PNode(g: nat --> PTree)
datatype RTree = RLeaf | RNode(h: nat ~> RTree)
datatype N = N(x: nat)
datatype S = S(p: Pos)
datatype Q = Q(s: seq<nat>)
datatype Box<A> = Box(a: A)
datatype Pair<A, B> = Pair(fst: A, snd: B)
datatype Opt<A> = None | Some(val: A)
type NodeTree = t: Tree | t.Node? witness Node(_ => Leaf)

// The program of the issue
method TotalMatch(t: Tree) { match t case Leaf => case Node(k) => var u := k(0); }
method TotalLet(t: Tree) requires t.Node? { var Node(k) := t; var u := k(0); }
method TotalDestr(t: Tree) requires t.Node? { var u := t.kids(0); }
method PartialMatch(t: PTree) { match t case PLeaf => case PNode(g) => assert g.reads(0) == {}; }
method NatField(n: N) { match n case N(y) => assert y >= 0; }
method SubsetField(s: S) { match s case S(y) => assert y > 0; }
method SeqField(q: Q) { match q case Q(s) => assert forall i :: 0 <= i < |s| ==> s[i] >= 0; }
method MatchExpr(t: Tree) returns (b: bool) { b := match t case Leaf => true case Node(k) => k(0) == Leaf; }
method Annotated(t: Tree) { match t case Leaf => case Node(k: nat -> Tree) => var u := k(0); }

// Match expressions and let expressions
function MatchFn(t: Tree): Tree { match t case Leaf => Leaf case Node(k) => k(0) }
function LetExpr(t: Tree): Tree requires t.Node? { var Node(k) := t; k(0) }

// Nested patterns
method Nested(o: Opt<Tree>) { match o case None => case Some(Leaf) => case Some(Node(k)) => var u := k(0); }
function NestedFn(o: Opt<Tree>): Tree { match o case Some(Node(k)) => k(0) case _ => Leaf }
method NestedLet(o: Opt<Tree>) requires o.Some? && o.val.Node? { var Some(Node(k)) := o; var u := k(0); }
function NestedLetExpr(o: Opt<Tree>): Tree requires o.Some? && o.val.Node? { var Some(Node(k)) := o; k(0) }

// Generic datatypes: the source's type arguments are substituted into the formal's type
method Generic(b: Box<nat -> int>) { match b case Box(f) => var u := f(0); }
method GenericPair(p: Pair<Pos, nat --> int>) requires p.snd.requires(p.fst) {
  match p case Pair(x, f) => assert x > 0; var u := f(x);
}
method GenericNested(b: Box<Box<nat -> Tree>>) { match b case Box(Box(f)) => var u := f(0); }
method Tuple(p: (nat -> int, Pos)) { match p case (f, y) => var u := f(0); assert y > 0; }

// Partial and general arrows
method Reads(t: RTree) requires t.RNode? ==> t.h.requires(0) && t.h.reads(0) == {} {
  match t case RLeaf => case RNode(h) => var u := h(0);
}

// Other patterns: a variable bound at the top, a subset-typed source, a wider annotation, wildcards, literals
method TopVar(t: NodeTree) { match t case x => assert x.Node?; var u := x.kids(0); }
method SubsetSource(t: NodeTree) { match t case Node(k) => var u := k(0); }
method AnnotatedWider(t: Tree) { match t case Leaf => case Node(k: nat ~> Tree) => }
method Wildcard(t: Tree) { match t case Leaf | Node(_) => }
method IntMatch(n: nat) { match n case 0 => case x => assert x > 0; }
