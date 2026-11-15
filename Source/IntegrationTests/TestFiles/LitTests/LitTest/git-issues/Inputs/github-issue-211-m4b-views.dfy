module Library {
  export P reveals T provides T.Final, T.Virtual, Closed
  export R reveals T, T.Final provides T.Virtual, Closed
  trait {:termination false} T {
    function {:termination_closed} Final(): nat { 0 }
    method Virtual(n: nat) decreases {:global} n { }
  }
  method {:termination_closed} Closed() { }
}
module Client {
  import P = Library`P
  import R = Library`R
  method A(t: P.T) decreases {:global} 1 { t.Virtual(0); P.Closed(); }
  method B(t: R.T) decreases {:global} 1 { t.Virtual(0); R.Closed(); }
  function AFinal(t: P.T): nat decreases {:global} 0 { t.Final() }
  function BFinal(t: R.T): nat decreases {:global} 0 { t.Final() }
}
