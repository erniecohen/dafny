module Library {
  const Weight: nat := 1
  type Small = n: nat | n < 10
  datatype D = D(n: nat)
}
module Client {
  import Library
  function F(): nat decreases {:global} 0 { Library.Weight }
  method Types(s: Library.Small, d: Library.D) decreases {:global} 0 { }
}
