// Acyclic ancestors keep their preloaded identities only when used by a group.
module {:extern "ForeignOutside"} Outside {
  trait {:extern "NativeOutsideRoot"} OutsideRoot extends object {
  }
  trait {:extern "NativeOutsideGenericRoot"} OutsideGenericRoot<T> extends object {
  }
  trait {:extern "NativeUnused"} Unused extends object { }
}
