// A client of github-issue-266-m4-args.dfy, as a .doo library.

module ListClient {
  import opened Std.Termination.Orders
  import opened MultisetOrders
  import opened Levels
  import opened ListApi
  import ListImpl

  method Main() decreases {:global} multiset{MainL} by Bags() {
    UnderMain(multiset{Cell});
    var a := ListImpl.CreateNil();
    UnderMain(multiset{Cell, Cell});
    assert multiset{Cell, Cell} + a.contains.bound == multiset{Cell, Cell};
    a := ListImpl.CreateCons(1, a);
    var b := ListImpl.CreateNil();
    assert multiset{Cell, Cell} + b.contains.bound == multiset{Cell, Cell};
    b := ListImpl.CreateCons(1, b);
    // a's intersects at b: {Cell} + {Cell}; the wrapper adds {IntersectsW}
    assert a.intersects.boundAt(b) == multiset{Cell, Cell};
    UnderMain(multiset{IntersectsW, Cell, Cell});
    assert multiset{IntersectsW} + a.intersects.boundAt(b) == multiset{IntersectsW, Cell, Cell};
    var r := Intersects(a, b);
  }
}
