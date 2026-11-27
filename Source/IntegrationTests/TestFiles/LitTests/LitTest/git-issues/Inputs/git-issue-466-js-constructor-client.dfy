// Implicit defaults must traverse sibling modules and the extern-backed witness.
module Client {
  import A
  import B
  import C
  import Refined
  function Argument<T>(value: T, marker: int): T { value }
    by method { print "argument ", marker, "\n"; return value; }
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
    var firstCell := new B.Cell<A.Tree<int>>.Init(999, Argument(A.Node(B.Branch(A.Leaf(29))), 1), Argument(31, 2));
    var secondCell := new B.Cell<int>.Init(999, Argument(37, 3), Argument(41, 4));
    var thirdCell := new B.Cell<A.Positive>.Init(999, A.Positive(43), 47);
    assert firstCell.Read().child.tree.value == 29 && firstCell.Tag() == 31;
    assert secondCell.Read() == 37 && secondCell.Tag() == 41;
    assert thirdCell.Read().n == 43 && thirdCell.Tag() == 47;
    print firstCell.Read().child.tree.value, " ", secondCell.Read(), " ", thirdCell.Read().n, "\n";
    print firstCell.Tag(), " ", secondCell.Tag(), " ", thirdCell.Tag(), "\n";
    print firstCell.InitialValue().Leaf?, " ", secondCell.InitialValue(), " ", thirdCell.InitialValue().n, "\n";
    var zero := new B.Zero(999);
    print zero.Read(), "\n";
    var scalar := new B.IntCell(999, Argument(59, 5));
    assert scalar.Read() == 59;
    print scalar.Read(), "\n";
  }
}
