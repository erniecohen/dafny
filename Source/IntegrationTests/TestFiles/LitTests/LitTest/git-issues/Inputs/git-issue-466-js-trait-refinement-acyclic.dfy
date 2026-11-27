module {:extern "ForeignBase"} A {
  trait {:extern "NativeView"} View<T> {
    function {:extern} Read(): T
  }
}
module {:extern "ForeignRefined"} Refined refines A { }
