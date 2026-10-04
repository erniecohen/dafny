// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

trait Tagged {
  function Tag(): int
  function Twice(): (r: int) ensures r == 2 * Tag() { 2 * Tag() }
}
datatype Record extends Tagged = Record(value: int) {
  function Tag(): int { value }
}
newtype Wrapped = Record witness Record(0)

trait Value<T> { function Read(): T }
datatype Box<T> extends Value<T> = Box(value: T) {
  function Read(): T { value }
}
newtype WrappedBox<T> = Box<T> witness *

lemma BaseSignatures(n: Wrapped, generic: WrappedBox<int>) {
  var tag: int := n.Tag();
  var twice: int := n.Twice();
  var field: int := generic.value;
  var read: int := generic.Read();
  assert tag == n.value;
  assert twice == 2 * n.value;
  assert field == read;
  var declaredImplementation: Tagged := n as Record;
  assert declaredImplementation.Tag() == n.value;
}

module Provider {
  trait Tagged {
    function Tag(): int
    function Twice(): (r: int) ensures r == 2 * Tag() { 2 * Tag() }
  }
  datatype Record extends Tagged = Record(value: int) {
    function Tag(): int { value }
  }
  newtype Wrapped = Record witness Record(0)
  export API reveals Tagged.Tag, Tagged.Twice, Tagged, Record, Record.Tag, Wrapped
}
module Client {
  import P = Provider`API
  method Observe() returns (tag: int, twice: int) {
    var wrapped := P.Record(7) as P.Wrapped;
    var implementation: P.Tagged := wrapped as P.Record;
    tag := wrapped.Tag();
    twice := wrapped.Twice();
    assert tag == 7 && twice == 14;
    assert implementation.Tag() == tag;
  }
}

method Main() {
  var wrapped := Record(7) as Wrapped;
  var generic := Box(9) as WrappedBox<int>;
  expect wrapped.Tag() == 7 && wrapped.Twice() == 14;
  expect generic.Read() == 9;
  var implementation: Tagged := wrapped as Record;
  expect implementation.Tag() == 7;
  var tag, twice := Client.Observe();
  print wrapped.Tag(), " ", wrapped.Twice(), " ", generic.Read(), " ", tag, " ", twice, "\n";
}
