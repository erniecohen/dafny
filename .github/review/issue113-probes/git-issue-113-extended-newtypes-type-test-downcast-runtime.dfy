// Private runtime/proof draft; no execution or expected-output file yet.
// Use --type-system-refresh --general-newtypes --extended-newtype-bases.
// Intended Main output: 7 0 42 0 5

datatype Box<T> = Box(value: T)
newtype Positive = b: Box<int> | b.value > 0 witness Box(1)
datatype Maybe<T> = None | Some(value: T)
newtype OnlySome<T> = m: Maybe<T> | m.Some? witness *
newtype DeepSome<T> = OnlySome<seq<T>> witness *

method PositiveValue(b: Box<int>) returns (r: int) {
  if b is Positive {
    var checked := b as Positive;
    assert checked.value > 0;
    r := checked.value;
  } else {
    r := 0;
  }
}
method DeepValues<T>(m: Maybe<seq<T>>) returns (r: seq<T>) {
  if m is DeepSome<T> {
    var checked := m as DeepSome<T>;
    r := checked.value;
  } else {
    r := [];
  }
}

module Provider {
  datatype Box<T> = Box(value: T)
  newtype Positive = b: Box<int> | b.value > 0 witness Box(1)
  export API reveals Box, Positive
}
module Client {
  import P = Provider`API
  method PositiveValue(b: P.Box<int>) returns (r: int) {
    if b is P.Positive {
      var checked := b as P.Positive;
      assert checked.value > 0;
      r := checked.value;
    } else {
      r := 0;
    }
  }
}

method Main() {
  var good := PositiveValue(Box(7));
  var bad := PositiveValue(Box(0));
  var deep := DeepValues<int>(Some([42]));
  var absent := DeepValues<int>(None);
  var visible := Client.PositiveValue(Provider.Box(5));
  expect good == 7 && bad == 0;
  expect deep == [42] && absent == [];
  expect visible == 5;
  print good, " ", bad, " ", deep[0], " ", |absent|, " ", visible, "\n";
}
