// With no later refining member, only the function itself can attach its deferred type check.
module Base {
  trait T<A> {
    ghost function F(a: A): A
  }
}
module Changed refines Base {
  trait T<B> ... {
    ghost function F(b: int): B
  }
}
