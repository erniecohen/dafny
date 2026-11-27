module BodyClient {
  import TraitClient
  import Bodied
  import BodiedAgain
  import A
  import B
  function Seed(value: int): int { value }
    by method { print "seed ", value, "\n"; return value; }
  method Main() {
    TraitClient.Main();
    var x := new Bodied.Store(Seed(7));
    var y := new BodiedAgain.Store(Seed(11));
    assert x.Read() == 7 + x.Tag() + 1;
    assert y.Read() == 11 + y.Tag() + 1;
    print x.Read(), " ", y.Read(), " ", x.Tag(), " ", y.Tag(), "\n";
    var n := x.Advance(211);
    var m := y.Advance(223);
    assert n == 212 && m == 224;
    print n, " ", m, "\n";
    print x.Echo(A.Node(B.Branch(A.Leaf(227)))).child.tree.value, " ", y.Echo(229), "\n";
    var a := new Bodied.Cell<A.Tree<int>>.Init(A.Node(B.Branch(A.Leaf(233))));
    var b := new BodiedAgain.Cell<int>.Init(239);
    assert a.Read().child.tree.value == 233;
    assert b.Read() == 239;
    print a.Echo(a.Read()).child.tree.value, " ", b.Echo(b.Read()), "\n";
  }
}
