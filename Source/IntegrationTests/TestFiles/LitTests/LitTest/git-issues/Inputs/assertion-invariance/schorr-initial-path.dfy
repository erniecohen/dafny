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

// Each proved postcondition supports the next under sequential checking.
twostate lemma ExtendInitialPath(root: Node, prefix: Path, parent: Node,
                                child: Node, S: set<Node>)
  requires parent in S
  requires old(ReachableVia(root, prefix, parent, S))
  requires child in old(parent.children)
  ensures old(allocated(Path.Extend(prefix, parent)))
  ensures old(ReachableVia(root, Path.Extend(prefix, parent), child, S))
  ensures old(Reachable(root, child, S))
{
}
