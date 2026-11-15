// RUN: %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment "%s" > "%t"
// RUN: %diff "%s.verify.expect" "%t"
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment
// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --optimize-erasable-datatype-wrapper:false --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 --relax-definite-assignment

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

import Visible = Provider`API

method Main() {
  var good := PositiveValue(Box(7));
  var bad := PositiveValue(Box(0));
  var deep := DeepValues<int>(Some([42]));
  var absent := DeepValues<int>(None);
  var visible := Client.PositiveValue(Visible.Box(5));
  expect good == 7 && bad == 0;
  expect deep == [42] && absent == [];
  expect visible == 5;
  print good, " ", bad, " ", deep[0], " ", |absent|, " ", visible, "\n";
}
