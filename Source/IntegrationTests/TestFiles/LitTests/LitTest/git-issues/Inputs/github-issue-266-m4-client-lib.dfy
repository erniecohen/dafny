// A client of github-issue-266-m4-lib.dfy, as a .doo library.

module FuncClient {
  import opened MultisetOrders
  import opened Levels
  import opened FuncApi
  import FuncImpl

  // main: twice of twice of plus_one is created at {twice, twice}, and applied through the wrapper at
  // {invoke, twice, twice}
  method Main() decreases {:global} multiset{MainL} by Bags() {
    UnderMain(multiset{Twice});
    var plusOne := FuncImpl.CreatePlusOne();
    assert multiset{Twice} + plusOne.invoke.bound == multiset{Twice};
    var twicePlusOne := FuncImpl.CreateTwice(plusOne);
    UnderMain(multiset{Twice, Twice});
    assert multiset{Twice} + twicePlusOne.invoke.bound == multiset{Twice, Twice};
    var twiceTwicePlusOne := FuncImpl.CreateTwice(twicePlusOne);
    UnderMain(multiset{Invoke, Twice, Twice});
    assert multiset{Invoke} + twiceTwicePlusOne.invoke.bound == multiset{Invoke, Twice, Twice};
    var r := Apply(twiceTwicePlusOne, 10);
  }
}
