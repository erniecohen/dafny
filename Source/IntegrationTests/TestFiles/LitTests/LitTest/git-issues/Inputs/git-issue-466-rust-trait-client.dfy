module TraitClient {
  import ConstructorClient
  import Traitful
  import TraitfulAgain
  import A
  import B
  method Main() {
    ConstructorClient.Main();
    var first := new Traitful.Generated<A.Tree<int>>();
    var second := new TraitfulAgain.Generated<int>();
    var third := new Traitful.Foreign<int>();
    var fourth := new TraitfulAgain.Foreign<A.Tree<int>>();
    var p: Traitful.Protocol<A.Tree<int>> := first;
    var q: TraitfulAgain.Protocol<int> := second;
    var r: Traitful.Protocol<int> := third;
    var s: TraitfulAgain.Protocol<A.Tree<int>> := fourth;
    print p.Get(A.Node(B.Branch(A.Leaf(173)))).child.tree.value, " ", q.Get(179), " ", r.Get(181), " ", s.Get(A.Leaf(191)).value, "\n";
    print first.Tag(), " ", q.Tag(), " ", third.Tag(), " ", s.Tag(), "\n";
    var marker: Traitful.Marker := third;
    var again: TraitfulAgain.Marker := second;
    print marker.BaseTag(), " ", again.BaseTag(), "\n";
  }
}
