newtype WrappedMaps = s: set<map<bool, int>> | true

ghost function UnboundedWrappedMaps(): WrappedMaps {
  set i: int | true :: map[true := i]
}
