module Client {
  import A
  import B
  import C
  import Refined
  method Main() {
    print A.Count(6), " ", B.Count(7), " ", C.Count(8), "\n";
    print A.Start, " ", B.Middle, " ", C.Last, " ", A.End, "\n";
    // Enter the non-generic default chain from the sibling first.
    var first := new B.Edge[1];
    print first[0].path.Stop?, "\n";
    var tree := A.Node(B.Branch(A.Leaf(A.Identity(42))));
    print tree.child.tree.value, "\n";
    print tree, "\n";
    var trees := new A.Tree<int>[1];
    var edges := new B.Edge[1];
    var paths := new A.Path[1];
    print trees[0].Leaf?, " ", edges[0].path.Stop?, " ", paths[0].Stop?, "\n";
    var boxes := new B.Box[1];
    print boxes[0].p.n, "\n";
    var counter: A.Counter := new B.Implementation();
    var native := new A.Native();
    var refined := new Refined.Native();
    print native.Local(), " ", native.Value(), " ", refined.Local(), " ", refined.Value(), "\n";
    print counter.Eval(4), " ", Refined.Count(3), "\n";
  }
}
