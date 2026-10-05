// Source-only diagnostic draft. This carrier has infinitely many maps:
// for every integer n, map[true := Payload(n)] is a distinct inhabitant.
datatype Payload<T> = Payload(value: T)
newtype WrappedPayload<T> = Payload<T> witness *

ghost function InvalidCarrier(): set<map<bool, WrappedPayload<int>>> {
  set m: map<bool, WrappedPayload<int>> | true
}
