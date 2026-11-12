module Library {
  export provides T, T.Step
  trait {:termination false} {:termination_closed} T {
    method Step() decreases {:global} 0
  }
}
module Client {
  import Library
  method Equal(t: Library.T) decreases {:global} 0 { t.Step(); }
}
