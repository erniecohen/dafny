// A library built against github-issue-266-m4-lib.dfy, as a .doo library: twice of twice of a stored function.
module FuncFour {
  import opened Std.Termination.Orders
  import opened MultisetOrders
  import opened Levels
  import opened FuncApi
  import FuncImpl

  // Its bound is that of the second call: CreateTwice needs no bound above its own, since it calls nothing that
  // other modules may provide
  method CreateFour(f: IntFuncObject) returns (o: IntFuncObject)
    ensures o.invoke.bound == multiset{Twice, Twice} + f.invoke.bound
    decreases {:global} multiset{Twice, Twice} + f.invoke.bound by Bags()
  {
    MultisetAddOne<nat>(Gt, Twice, multiset{Twice} + f.invoke.bound);
    assert multiset{Twice} + (multiset{Twice} + f.invoke.bound) == multiset{Twice, Twice} + f.invoke.bound;
    var t := FuncImpl.CreateTwice(f);
    assert multiset{Twice} + t.invoke.bound == multiset{Twice, Twice} + f.invoke.bound;
    o := FuncImpl.CreateTwice(t);
  }
}
