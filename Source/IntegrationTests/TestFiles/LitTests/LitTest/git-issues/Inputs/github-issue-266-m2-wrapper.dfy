// Knots through the parameter and result types of a bounded value's function.  Only the top-level arrow of the
// function type is exempt from the rule against storing a function on an object's own type in its fields: a
// bounded function that hands out a raw function on the object would let code without a bound apply it again.
// Before the fix, Wrapper verified "ensures false" and, compiled, recursed until the stack overflowed.
module Wrapper {
  class Y {
    const f: bounded<nat, () -> (Y -> int)>
    constructor (f: bounded<nat, () -> (Y -> int)>) ensures this.f == f { this.f := f; } // error: Y -> int in the result
  }
  method Main() ensures false {
    var g := (x: Y) => 1 + x.f()(x);
    var y := new Y(bounded.certify(0, () => g));
    assert y.f.erase() == g;
    assert g(y) == 1 + y.f()(y);
  }
}
// The same through a sequence in the result.
module WrapperSeq {
  class Y {
    const f: bounded<nat, () -> seq<Y -> int>>
    constructor (f: bounded<nat, () -> seq<Y -> int>>) { this.f := f; } // error: Y -> int in the result
  }
}
// A raw function on the object as a parameter of the bounded function.
module WrapperParameter {
  class Y {
    const f: bounded<nat, (Y -> int) -> int>
    constructor (f: bounded<nat, (Y -> int) -> int>) { this.f := f; } // error: Y -> int in a parameter
  }
}
// A bounded value inside a raw function's domain is checked as the raw function's own domain is.
module UnderArrow {
  class Y {
    const f: bounded<nat, Y -> int> -> int
    constructor (f: bounded<nat, Y -> int> -> int) { this.f := f; } // error: Y left of the raw arrow
  }
}
