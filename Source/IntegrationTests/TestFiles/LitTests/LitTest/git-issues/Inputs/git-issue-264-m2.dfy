module A {
  trait {:termination false} T {
    method f(r: real)
      requires 0.0 < r <= 1.0
      decreases {:global} r, 0
  }
  method h(t: T, r: real)
    requires 0.0 < r <= 1.0
    decreases {:global} r, 1
  {
    t.f(r);
  }
}
module B {
  import A
  class C extends A.T {
    method f(r: real)
      requires 0.0 < r <= 1.0
    {
      A.h(this, r / 2.0);
    }
  }
}
