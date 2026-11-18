// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

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
