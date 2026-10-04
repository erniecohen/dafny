// Private visibility rejection draft. The provided target exposes no base or predicate.
module Provider {
  datatype Box<T> = Box(value: T)
  newtype Positive = b: Box<int> | b.value > 0 witness Box(1)
  export API reveals Box provides Positive
}
module Client {
  import P = Provider`API
  method CannotInspectHiddenPredicate(b: P.Box<int>) returns (result: bool) {
    result := b is P.Positive; // ERROR: the operation view cannot reveal Positive's base
  }
}
