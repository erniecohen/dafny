// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

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
