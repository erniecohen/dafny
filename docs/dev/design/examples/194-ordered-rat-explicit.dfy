type Pos = x: int | 0 < x witness 1

trait Ordered<T extends Ordered<T>> {
  ghost function self(): T
    ensures this == (self() as Ordered<T>)
  predicate eq(other: T)
  predicate le(other: T)
  lemma eq_reflexive()
    ensures eq(self())
  lemma eq_symmetric(other: T)
    requires eq(other)
    ensures (other as Ordered<T>).eq(self())
  lemma eq_transitive(other: T, last: T)
    requires eq(other) && (other as Ordered<T>).eq(last)
    ensures eq(last)
  lemma le_congruent(other: T, last: T)
    requires (other as Ordered<T>).eq(last)
    ensures le(other) == le(last)
  lemma le_congruent_left(other: T, last: T)
    requires eq(other)
    ensures le(last) == (other as Ordered<T>).le(last)
  lemma reflexive()
    ensures le(self())
  lemma antisymmetric(other: T)
    requires le(other) && (other as Ordered<T>).le(self())
    ensures eq(other)
  lemma transitive(other: T, last: T)
    requires le(other) && (other as Ordered<T>).le(last)
    ensures le(last)
}

datatype Rat extends Ordered<Rat> = Rat(n: int, d: Pos) {
  ghost function self(): Rat
    ensures this == self() as Ordered<Rat>
  { this }
  predicate eq(other: Rat) { n * other.d == other.n * d }
  predicate le(other: Rat) { n * other.d <= other.n * d }
  function times(other: Rat): Rat { Rat(n * other.n, d * other.d) }
  function plus(other: Rat): Rat { Rat(n * other.d + other.n * d, d * other.d) }
  lemma eq_reflexive()
    ensures eq(this)
  { }
  lemma eq_symmetric(other: Rat)
    requires eq(other)
    ensures other.eq(this)
  { }
  lemma eq_transitive(other: Rat, last: Rat)
    requires eq(other) && other.eq(last)
    ensures eq(last)
  { }
  lemma le_congruent(other: Rat, last: Rat)
    requires other.eq(last)
    ensures le(other) == le(last)
  { }
  lemma le_congruent_left(other: Rat, last: Rat)
    requires eq(other)
    ensures le(last) == other.le(last)
  { }
  lemma reflexive()
    ensures le(this)
  { }
  lemma antisymmetric(other: Rat)
    requires le(other) && other.le(this)
    ensures eq(other)
  { }
  lemma transitive(other: Rat, last: Rat)
    requires le(other) && other.le(last)
    ensures le(last)
  { }
}

function test(a: Rat, b: Rat, c: Rat, d: Rat): bool {
  a.times(b).plus(c).le(d)
}

method Main() {
  var a := Rat(1, 2);
  var b := Rat(2, 3);
  var c := Rat(1, 6);
  var d := Rat(1, 2);
  print a.times(b).plus(c).le(d), " ", a.eq(Rat(2, 4)), " ", a == Rat(2, 4), "\n";
}
