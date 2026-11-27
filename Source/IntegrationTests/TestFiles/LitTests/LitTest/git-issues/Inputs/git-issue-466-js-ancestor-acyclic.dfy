module {:extern "ForeignOutside"} Outside {
  trait {:extern "NativeOutsideRoot"} Root extends object { }
  trait {:extern "NativeUnused"} Unused extends object { }
}
module {:extern "ForeignB"} A {
  import Outside
  trait {:extern "NativeView"} View extends Outside.Root { }
}
module {:extern "ForeignFinal"} Refined refines A { }
