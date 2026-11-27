module PartialClient {
  import ExtendedClient
  import Partial
  import PartialAgain
  import A
  import B
  method Main() {
    ExtendedClient.Main();
    var x := new Partial.Native();
    var y := new PartialAgain.Native();
    var r := x.Value(5);
    var s := y.Twice(5);
    assert r == 106 && s == 107;
    print x.Local(), " ", y.Local(), " ", r, " ", s, " ", x.WithMethod(7), " ", y.WithMethod(8), "\n";
    var first := new Partial.Fresh<A.Tree<int>>();
    var box := new PartialAgain.Generic<int>();
    var second := new PartialAgain.Fresh<A.Tree<int>>();
    print first.Through(A.Node(B.Branch(A.Leaf(113)))).child.tree.value, " ", second.Through(A.Leaf(127)).value, " ", box.ThroughBox(131), "\n";
  }
}
