// RUN: %verify --type-system-refresh --general-traits=datatype --general-newtypes --extended-newtype-bases
// Expected: the 0-as-Positive cast must fail. Both outer and inner actuals have the same Stream declaration.
newtype Positive = value: int | value > 0 witness 1
codatatype Stream<T> = Cons(value: T, next: Stream<T>)
newtype Wrap<T> = Stream<T>
newtype Nested = Stream<Wrap<Positive>>

function Ones(): Wrap<Positive> {
  Cons(1 as Positive, Ones() as Stream<Positive>) as Wrap<Positive>
}

function BadOuter(): Nested {
  Cons(Cons(0 as Positive, Ones() as Stream<Positive>) as Wrap<Positive>,
       BadOuter() as Stream<Wrap<Positive>>) as Nested
}
