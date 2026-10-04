// Diagnostic draft: preserve existing recursive-newtype/cardinality policy.
// If resolution accepts these, compile on every applicable backend with erasure on/off.
// W and G must not be erased through a W -> NW -> W or G<T> -> NG<T> -> G<T> cycle.

datatype W = ghost Stop | Wrap(payload: NW)
newtype NW = W ghost witness Stop
datatype G<T> = ghost Ground | G(payload: NG<T>)
newtype NG<T> = G<T> ghost witness Ground

method RepresentationCycleRecord() {
  var w: W := *;
  var g: G<int> := *;
}
