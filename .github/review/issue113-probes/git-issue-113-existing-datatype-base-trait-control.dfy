// Private direct-base control; no execution or expected-output file yet.
// Intended options: --type-system-refresh=true --general-newtypes=true
//   --extended-newtype-bases=false --general-traits=datatype
// Intended Main output: 7 14 9 7 14

trait Tagged {
  function Tag(): int
  function Twice(): (r: int) ensures r == 2 * Tag() { 2 * Tag() }
}
datatype Record extends Tagged = Record(value: int) {
  function Tag(): int { value }
}
trait Value<T> { function Read(): T }
datatype Box<T> extends Value<T> = Box(value: T) {
  function Read(): T { value }
}

lemma BaseSignatures(n: Record, generic: Box<int>) {
  var tag: int := n.Tag();
  var twice: int := n.Twice();
  var field: int := generic.value;
  var read: int := generic.Read();
  assert tag == n.value;
  assert twice == 2 * n.value;
  assert field == read;
  var declaredImplementation: Tagged := n;
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
  export API provides Tagged.Tag, Tagged.Twice reveals Tagged, Record, Record.Tag
}
module Client {
  import P = Provider`API
  method Observe() returns (tag: int, twice: int) {
    var record := P.Record(7);
    var implementation: P.Tagged := record;
    tag := record.Tag();
    twice := record.Twice();
    assert tag == 7 && twice == 14;
    assert implementation.Tag() == tag;
  }
}

method Main() {
  var record := Record(7);
  var generic := Box(9);
  expect record.Tag() == 7 && record.Twice() == 14;
  expect generic.Read() == 9;
  var implementation: Tagged := record;
  expect implementation.Tag() == 7;
  var tag, twice := Client.Observe();
  print record.Tag(), " ", record.Twice(), " ", generic.Read(), " ", tag, " ", twice, "\n";
}
