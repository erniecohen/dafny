newtype Ord = ORDINAL
newtype OtherOrd = ORDINAL

method Nominality(o: ORDINAL, n: Ord) {
  var a: Ord := o; // ERROR: base variable needs an explicit cast
  var b: OtherOrd := n; // ERROR: sibling identity needs an explicit cast
}

function UnsupportedMultiply(x: Ord, y: Ord): Ord {
  x * y // ERROR: ordinal multiplication remains unsupported
}

newtype {:nativeType "uint"} NativeOrd = ORDINAL
// ERROR: nativeType only supports integer/bitvector bases
