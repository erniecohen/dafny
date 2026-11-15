abstract module A {
  trait Add<T> {
    function {:operator "+"} plus(y: T): T
  }
  type T extends Add<T> {
    function plus(y: T): T
  }
  lemma same(x: T)
    ensures x + x == x.plus(x)
  { }
}
module B refines A {
  newtype T = int {
    function plus(y: T): T {
      ((this as int) + (y as int) + 1) as T
    }
  }
  lemma bad() {
    same(0);
    assert false;
  }
}
