// RUN: %verify "%s" --general-newtypes=true --type-system-refresh=true --extended-newtype-bases=true
codatatype Stream<T> = S(head: T, tail: Stream<T>)
newtype Wrapped<T> = Stream<T>
function CarrierChange(): Wrapped<int> {
  S(0, (CarrierChange() as Stream<int>) as Stream<nat>) as Wrapped<int>
}
