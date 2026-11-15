// A bodyless static function of a trait is an assumption too.
module Bad {
  trait T {
    static ghost function F(): int
      ensures false
  }

  lemma Bad()
    ensures false
  {
    var x := T.F();
  }
}
