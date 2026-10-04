// RUN: %exits-with 2 %baredafny resolve --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// Unrun control: do not broaden base conversions merely for this feature.
// The corresponding direct ORDINAL-to-bv8 cast is not supported in general-newtypes mode.

newtype Ord = ORDINAL
newtype OrdLayer = Ord
newtype ByteView = bv8

lemma BitvectorCast(o: Ord) {
  var b := o as bv8; // ERROR: unsupported base conversion family
}

lemma BitvectorCastThroughNewtype(o: OrdLayer) {
  var b := o as ByteView; // ERROR: nominal wrapper adds no base conversion
}
