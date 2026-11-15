// Initial-heap path extension, as used before Schorr-Waite pointer reversal.
class Node {
  var children: seq<Node?>
}

datatype Path = Empty | Extend(Path, Node)

ghost predicate Reachable(source: Node, sink: Node, S: set<Node>)
  reads S
{
  exists via :: ReachableVia(source, via, sink, S)
}

ghost predicate ReachableVia(source: Node, older p: Path, sink: Node, S: set<Node>)
  reads S
  decreases p
{
  match p
  case Empty => source == sink
  case Extend(prefix, n) => n in S && sink in n.children && ReachableVia(source, prefix, n, S)
}

twostate lemma ExtendInitialPath(root: Node, prefix: Path, parent: Node,
                                child: Node, S: set<Node>)
  requires parent in S
  requires old(ReachableVia(root, prefix, parent, S))
  requires child in old(parent.children)
  ensures old(allocated(Path.Extend(prefix, parent)))
  ensures old(ReachableVia(root, Path.Extend(prefix, parent), child, S))
  ensures old(Reachable(root, child, S))
{
  ghost var pathWitness := Path.Extend(prefix, parent);
  assert old(ReachableVia(root, pathWitness, child, S));
}

// The premises are inhabited. The final reachability concerns the initial
// graph even after the current graph's edge has been removed.
method InitialGraphWitness()
{
  var root := new Node;
  var child := new Node;
  root.children := [child];
  child.children := [];
  ghost var S := {root, child};
  label Initial:
  ExtendInitialPath@Initial(root, Path.Empty, root, child, S);
  root.children := [];
  assert old@Initial(ReachableVia(root, Path.Extend(Path.Empty, root), child, S));
  assert old@Initial(Reachable(root, child, S));
  assert child !in root.children;
}
