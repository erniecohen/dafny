// Repeated declarations, nested members and Rust keywords keep distinct namespaces.
module Outer {
  module mod {
    import Collision
    import Self
    datatype Value = Item(n: int)
    function ValueOf(x: Self.Value): int { x.n + 1 }
    method Main(args: seq<string>) {
      print ValueOf(Self.Item(4)), " ", Self.ValueOf(Item(7)), "\n";
      var x := Self.Item(3);
      print x.n, " ", Left.ValueOf(Left.Item(11)), " ", Collision.Marker(), "\n";
    }
    import Left
  }
  module Self {
    import T = mod
    datatype Value = Item(n: int)
    function ValueOf(x: T.Value): int { x.n + 2 }
  }
}
module Left {
  import Right
  datatype Value = Item(n: int)
  function ValueOf(x: Value): int { x.n + Right.Bias }
}
module Right {
  import Left
  const Bias := 3
}

module {:extern "_dafny_recursive_group_Left"} Collision {
  class Left { }
  function Marker(): int { 13 }
}
