// Controls for github-issue-211-stored.dfy, which is verified with this file: an assert false in each positive
// context, and levels that do not go down.

module Controls {
  import opened FuncApi
  import FuncImpl
  import opened ListApi
  import ListImpl

  method FuncControl() decreases {:global} 6 {
    var plusOne := FuncImpl.CreatePlusOne();
    var twicePlusOne := FuncImpl.CreateTwice(plusOne);
    var r := Invoke(twicePlusOne, 10);
    assert false; // error: control
  }

  method ListControl() decreases {:global} 8 {
    var l := ListImpl.CreateNil();
    l := ListImpl.CreateCons(3, l);
    var b := Contains(l, 3);
    assert false; // error: control
  }

  method CreateControl(f: IntFuncObject) returns (o: IntFuncObject)
    decreases {:global} f.invoke.bound + 3
  {
    o := FuncImpl.CreateTwice(f);
    assert false; // error: control
  }
}

module Rejected {
  import opened FuncApi
  import opened ListApi

  // The wrapper at the level of the stored function
  function FlatInvoke(o: IntFuncObject, x: int): int
    decreases {:global} o.invoke.bound
  {
    o.invoke(x) // error: the bound of o.invoke is not below the caller's
  }

  // twice at the level of the function it calls, below the wrapper (at the wrapper's own level it is accepted:
  // the wrapper's application is still one level down)
  method FlatTwice(f: IntFuncObject) returns (o: IntFuncObject)
    decreases {:global} f.invoke.bound + 3
  {
    o := new IntFuncObject(bounded.certify(f.invoke.bound, (x: int) => 2 * Invoke(f, x))); // error: Invoke's bound is f's + 1
  }

  // A cons cell whose contains is at the level of its tail's
  method FlatCons(head: int, tail: ListObject) returns (o: ListObject)
    decreases {:global} tail.contains.bound + 3
  {
    o := new ListObject(bounded.certify(tail.contains.bound, (x: int) => x == head || Contains(tail, x))); // error: Contains is above tail's level
  }

  // main with too small a budget: the wrapper of twice of twice of plus_one runs at level 5
  method LowMain(t: IntFuncObject) requires t.invoke.bound == 4 decreases {:global} 4 {
    var r := Invoke(t, 10); // error: Invoke's bound 5 exceeds 4
  }
}
