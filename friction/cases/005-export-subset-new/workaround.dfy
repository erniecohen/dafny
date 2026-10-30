module M {
  export provides T, P, Q
  datatype D = D(s: iset<int>)
  // Declare the characteristic the definition already has.
  type T(!new) = d: D | d.s != iset{} witness *
  ghost predicate Q(F: iset<T>, b: T) { forall x | x in F :: x.s <= b.s }
  // The bound variable's type is written out too: left to inference it
  // becomes D, and Q(F,b) then needs a subset check that cannot be proved.
  ghost predicate P(F: iset<T>) requires exists b: T :: Q(F,b) { true }
}
