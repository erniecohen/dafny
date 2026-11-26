// Implicit defaults must traverse sibling modules and the extern-backed witness.
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
    var tree := B.Echo(A.Node(B.Branch(A.Leaf(A.Identity(42)))));
    print tree.child.tree.value, "\n";
    var trees := new A.Tree<int>[1];
    var edges := new B.Edge[1];
    var paths := new A.Path[1];
    print trees[0].Leaf?, " ", edges[0].path.Stop?, " ", paths[0].Stop?, "\n";
    var boxes := new B.Box[1];
    print boxes[0].p.n, "\n";
    var counter: A.Counter := new B.Implementation();
    var native := new B.Native();
    var implementation := new B.Implementation();
    print implementation.ForeignEval(), " ", native.Value(), " ", native.Local(), " ", Refined.Identity(5), "\n";
    var handle := B.Handle.Create();
    var incremented := native.Add(8);
    var qualified := A.Increment(4);
    print qualified, " ", A.LocalBody(), " ", handle.Value(), " ", native.Number, " ", incremented, "\n";
    print counter.Eval(4), " ", Refined.Count(3), "\n";
  }
}
