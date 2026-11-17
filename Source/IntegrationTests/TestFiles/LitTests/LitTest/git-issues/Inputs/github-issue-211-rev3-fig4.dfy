// ECOOP 2015 §2.1, Fig. 4: PlusN and Twice; Twice(f) sits one level above f.
module F {
  trait {:termination_closed} Func {
    ghost const {:termination_closed} depth: nat
    ghost predicate Valid() decreases {:global} depth, 0
    function Apply(x: int): int requires Valid() decreases {:global} depth, 1
  }
}
module PlusN { import F
  class {:termination_closed} PlusN extends F.Func {
    const n: int
    constructor (n: int) ensures Valid() && depth == 0 { this.n := n; depth := 0; }
    ghost predicate Valid() decreases {:global} depth, 0 { depth == 0 }
    function Apply(x: int): int requires Valid() decreases {:global} depth, 1 { x + n }
  }
}
module Twice { import F
  class {:termination_closed} Twice extends F.Func {
    const f: F.Func
    constructor (f: F.Func) requires f.Valid() ensures depth == f.depth + 1 && Valid()
    { this.f := f; depth := f.depth + 1; }
    ghost predicate Valid() decreases {:global} depth, 0 { depth == f.depth + 1 && f.Valid() }
    function Apply(x: int): int requires Valid() decreases {:global} depth, 1 { f.Apply(f.Apply(x)) }
  }
}
module Main { import F import PlusN import Twice
  method Main() {
    var p := new PlusN.PlusN(3);
    var t := new Twice.Twice(p);
    var tt := new Twice.Twice(t);
    var r := tt.Apply(1);

  }
}
