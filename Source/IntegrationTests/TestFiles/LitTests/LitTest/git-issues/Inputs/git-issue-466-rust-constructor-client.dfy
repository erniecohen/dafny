module ConstructorClient {
  import PartialClient
  import Constructed
  import ConstructedAgain
  import A
  import B
  method Main() {
    PartialClient.Main();
    var x := new Constructed.Store(3);
    var y := new ConstructedAgain.Store(5);
    assert x.Read() == 3 + x.Tag() + 1;
    assert y.Read() == 5 + y.Tag() + 1;
    print x.Read(), " ", y.Read(), " ", x.Tag(), " ", y.Tag(), "\n";
    var first := new Constructed.Cell<A.Tree<int>>.Init(A.Node(B.Branch(A.Leaf(149))));
    var second := new ConstructedAgain.Cell<int>.Init(151);
    assert first.Read().child.tree.value == 149;
    assert second.Read() == 151;
    print first.Read().child.tree.value, " ", second.Read(), "\n";
  }
}
