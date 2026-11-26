module A {
  import B
  function Count(n: nat): nat { if n == 0 then 0 else 1 + B.Count(n - 1) }
  method Main() {
    var values := new bool[1];
    print "nonempty allocation returned\n";
  }
  method False() {
    var values := new int[0];
    assert values.Length == 0;
    assert false;
  }
}
module B {
  import A
  function Count(n: nat): nat { if n == 0 then 0 else 1 + A.Count(n - 1) }
}
