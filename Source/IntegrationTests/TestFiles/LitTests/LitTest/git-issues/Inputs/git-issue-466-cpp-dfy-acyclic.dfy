module Control {
  function Next(n: bv8): bv8 { n + 1 }
}
method Main() { print Control.Next(6), "\n"; }
