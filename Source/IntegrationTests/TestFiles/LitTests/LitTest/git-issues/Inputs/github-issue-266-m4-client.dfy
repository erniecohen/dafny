// A client of github-issue-266-m4-lib.dfy and github-issue-266-m4-dependent.dfy, as .doo libraries.
module Client {
  import opened MultisetOrders
  import opened Levels
  import opened FuncApi
  import FuncImpl
  import FuncFour

  method Main() decreases {:global} multiset{MainL} by Bags() {
    UnderMain(multiset{Twice});
    var plusOne := FuncImpl.CreatePlusOne();
    UnderMain(multiset{Twice, Twice});
    assert multiset{Twice, Twice} + plusOne.invoke.bound == multiset{Twice, Twice};
    var four := FuncFour.CreateFour(plusOne);
    UnderMain(multiset{Invoke, Twice, Twice});
    assert multiset{Invoke} + four.invoke.bound == multiset{Invoke, Twice, Twice};
    var r := Apply(four, 10);
  }
}
