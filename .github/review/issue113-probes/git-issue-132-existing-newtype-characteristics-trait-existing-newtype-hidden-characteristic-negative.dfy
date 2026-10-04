// RUN: %verify "%s" --type-system-refresh=true --general-traits=datatype --general-newtypes=true
module Provider {
  export provides Hidden
  newtype Hidden = int
}
module Client {
  import P = Provider
  // The hidden definition supplies no explicit (!new) guarantee.
  type Wrong(!new) = P.Hidden
}
