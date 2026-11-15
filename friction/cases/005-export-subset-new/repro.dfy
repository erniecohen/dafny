module M {
  export provides T, P, Q
  datatype D = D(s: iset<int>)
  type T = d: D | d.s != iset{} witness *
  ghost predicate Q(F: iset<T>, b: T) { forall x | x in F :: x.s <= b.s }
  ghost predicate P(F: iset<T>) requires exists b :: Q(F,b) { true }
}
