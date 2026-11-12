module Library {
  export P reveals T provides T.Final, T.Virtual
  trait {:termination false} T {
    function Final(): nat { 0 }
    method Virtual(n: nat) decreases {:global} n { }
  }
}
module Client {
  import P = Library`P
  method Equal(t: P.T) decreases {:global} 0 { t.Virtual(0); }
}
