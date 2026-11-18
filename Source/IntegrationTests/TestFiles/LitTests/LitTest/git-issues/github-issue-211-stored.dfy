// https://github.com/erniecohen/dafny/issues/211
// VeriFast's examples/termination/funcptr.c and cons.c in their stored-value form: function pointers stored in a
// struct, each with a level, here bounded values (https://github.com/erniecohen/dafny/issues/266) in const fields
// of a class.  The level of a stored function is its bound; the wrapper that applies it, int_func_object_invoke or
// list_object_contains, runs one level above.  VeriFast's levels are bags of functions; these are nat.  The same
// examples in VeriFast's bags are github-issue-211-order-types.dfy (funcptr.c, with an order type of bags) and
// github-issue-211-stored-arguments.dfy (cons.c with intersects, whose permission append(d, d_other) depends on its
// argument: an argument-dependent bound).
// RUN: echo "funcptr.c and cons.c (contains), stored values" > "%t"
// RUN: %exits-with 0 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --type-system-refresh --general-traits=datatype "%s" >> "%t"
// RUN: echo "controls (assert false in each positive context), and levels that do not go down" >> "%t"
// RUN: %exits-with 4 %baredafny verify --solver-path %review-z3 --use-basename-for-filename --show-snippets:false --type-system-refresh --general-traits=datatype "%s" "%S/Inputs/github-issue-211-stored-verify.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// funcptr.c
module FuncApi {
  // struct int_func_object: a stored function pointer, whose level is its bound
  class {:termination_closed} IntFuncObject {
    const {:termination_closed} invoke: bounded<nat, int -> int>
    constructor (invoke: bounded<nat, int -> int>) ensures this.invoke == invoke decreases {:global} 0 { this.invoke := invoke; }
  }
  // int_func_object_invoke: the wrapper runs one level above the stored function
  function Invoke(o: IntFuncObject, x: int): int
    decreases {:global} o.invoke.bound + 1
  {
    o.invoke(x)
  }
}
module FuncImpl {
  import opened FuncApi
  method CreatePlusOne() returns (o: IntFuncObject)
    ensures o.invoke.bound == 0
    decreases {:global} 1
  {
    o := new IntFuncObject(bounded.certify(0, (x: int) => x + 1));
  }
  method CreateTwice(f: IntFuncObject) returns (o: IntFuncObject)
    ensures o.invoke.bound == f.invoke.bound + 2
    decreases {:global} f.invoke.bound + 3
  {
    o := new IntFuncObject(bounded.certify(f.invoke.bound + 2, (x: int) => 2 * Invoke(f, x)));
  }
}
module FuncClient {
  import opened FuncApi
  import FuncImpl
  method Main() decreases {:global} 6 {
    var plusOne := FuncImpl.CreatePlusOne();
    var twicePlusOne := FuncImpl.CreateTwice(plusOne);
    var twiceTwicePlusOne := FuncImpl.CreateTwice(twicePlusOne);
    var r := Invoke(twiceTwicePlusOne, 10);
  }
}

// cons.c, without intersects
module ListApi {
  class {:termination_closed} ListObject {
    const {:termination_closed} contains: bounded<nat, int -> bool>
    constructor (contains: bounded<nat, int -> bool>) ensures this.contains == contains decreases {:global} 0 {
      this.contains := contains;
    }
  }
  function Contains(o: ListObject, x: int): bool
    decreases {:global} o.contains.bound + 1
  {
    o.contains(x)
  }
}
module ListImpl {
  import opened ListApi
  method CreateNil() returns (o: ListObject)
    ensures o.contains.bound == 0
    decreases {:global} 1
  {
    o := new ListObject(bounded.certify(0, (x: int) => false));
  }
  method CreateCons(head: int, tail: ListObject) returns (o: ListObject)
    ensures o.contains.bound == tail.contains.bound + 2
    decreases {:global} tail.contains.bound + 3
  {
    o := new ListObject(bounded.certify(tail.contains.bound + 2, (x: int) => x == head || Contains(tail, x)));
  }
}
module ListClient {
  import opened ListApi
  import ListImpl
  method Main() decreases {:global} 8 {
    var l := ListImpl.CreateNil();
    l := ListImpl.CreateCons(3, l);
    l := ListImpl.CreateCons(2, l);
    l := ListImpl.CreateCons(1, l);
    var b := Contains(l, 2);
  }
}
