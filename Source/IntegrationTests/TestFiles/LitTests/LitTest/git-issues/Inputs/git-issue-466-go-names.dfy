// Separate namespaces in each group, two groups, and dependencies outside each group.
module Leaf { const Seed: int := 11 }
module Left {
  import Right
  import Leaf
  trait Value { function Get(): int }
  class Item extends Right.Value {
    constructor () { }
    function Get(): int { Top }
  }
  datatype T = End | Link(t: Right.T)
  newtype N = n: int | 0 <= n < 10 witness 0
  type P = n: int | n > 0 witness 1
  const Bottom: int := Leaf.Seed
  const Top: int := Right.Middle + 1
  iterator Step() yields (v: int) { v := Right.Middle; yield; }
}
module Right {
  import Left
  trait Value { function Get(): int }
  class Item extends Left.Value {
    constructor () { }
    function Get(): int { Middle }
  }
  datatype T = End | Link(t: Left.T)
  newtype N = n: int | 10 <= n < 20 witness 10
  type P = n: int | n > 1 witness 2
  const Middle: int := Left.Bottom + 1
}
module First {
  import Second
  import Left
  datatype T = T(n: int)
  const Answer: int := Second.Answer + 1
}
module Second {
  import First
  import Right
  datatype T = T(t: First.T)
  const Answer: int := Right.Middle + 1
}
module Nested {
  module One {
    import X = Two
    datatype T = End | Link(t: X.T)
  }
  module Two {
    import X = One
    datatype T = End | Link(t: X.T)
  }
}
method Main() {
  var left: Right.Value := new Left.Item();
  var right: Left.Value := new Right.Item();
  print left.Get(), " ", right.Get(), " ", First.Answer, " ", Second.Answer, "\n";
  print left, " ", right, "\n";
  var step := new Left.Step();
  print step, "\n";
  var ls := new Left.T[1];
  var rs := new Right.T[1];
  print ls[0].End?, " ", rs[0].End?, "\n";
  var ln := new Left.N[1];
  var rn := new Right.N[1];
  var lp := new Left.P[1];
  var rp := new Right.P[1];
  print ln[0], " ", rn[0], " ", lp[0], " ", rp[0], "\n";
  print Nested.One.Link(Nested.Two.End), "\n";
  print First.T(5), " ", Second.T(First.T(6)), "\n";
}
