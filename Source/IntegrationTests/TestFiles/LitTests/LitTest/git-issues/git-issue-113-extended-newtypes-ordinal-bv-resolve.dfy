// RUN: %exits-with 2 %verify --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true --cores=1 --resource-limit=16000000 --show-snippets=false --allow-warnings --boogie /normalizeDeclarationOrder:0 "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

newtype Ord = ORDINAL
newtype OrdLayer = Ord
newtype ByteView = bv8

lemma BitvectorCast(o: Ord) {
  var b := o as bv8; // ERROR: unsupported base conversion family
}

lemma BitvectorCastThroughNewtype(o: OrdLayer) {
  var b := o as ByteView; // ERROR: nominal wrapper adds no base conversion
}
