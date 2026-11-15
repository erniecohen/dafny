datatype Tree = Leaf | Node(kids: nat -> Tree)

function Size(t: Tree): nat {
  match t
  case Leaf => 0
  case Node(k: nat -> Tree) => Size(k(0)) + 1
}

datatype Prog = Ret(v: int) | Read(k: int -> Prog)

function Run(p: Prog, input: int): int {
  if p.Ret? then p.v else Run(p.k(input), input)
}

datatype PTree = PLeaf | PNode(g: nat --> PTree)

ghost function Depth(t: PTree): nat {
  if t.PLeaf? then 0
  else if t.g.requires(0) then Depth(t.g(0)) + 1
  else 1
}

// Contrast: a seq field already has the rank fact.
datatype STree = SLeaf | SNode(kids: seq<STree>)
function SSize(t: STree): nat {
  if t.SLeaf? || |t.kids| == 0 then 0 else SSize(t.kids[0]) + 1
}
