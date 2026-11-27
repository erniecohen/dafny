module ExtendedClient {
  import ImportedClient
  import Extended
  import ExtendedAgain
  import A
  method Main() {
    ImportedClient.Main();
    var x := new Extended.Native();
    var y := new ExtendedAgain.Native();
    assert x.Marker() == 0;
    assert x.Added(3) == 103;
    assert y.Added(4) == 104;
    assert y.Second(5) == 205;
    var first := new Extended.Fresh<A.Tree<int>>();
    var again := new ExtendedAgain.Fresh<A.Tree<int>>();
    var box := new ExtendedAgain.Generic<int>();
    print x.Read(), " ", x.Added(3), " ", y.Read(), " ", y.Added(4), " ", y.Second(5), "\n";
    print first.Echo(A.Leaf(73)).value, " ", again.Echo(A.Leaf(79)).value, " ", box.Echo(83), "\n";
  }
}
