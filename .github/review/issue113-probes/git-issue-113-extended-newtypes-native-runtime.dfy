// RUN: %testDafnyForEachCompiler "%s" -- --type-system-refresh=true --general-newtypes=true --extended-newtype-bases=true
// Unrun: feature mode must preserve native conversion representation.

newtype {:nativeType "byte", "number"} Byte = i: int | 0 <= i < 256

method Main() {
  var b: Byte := 255;
  var i := b as int;
  var c := i as Byte;
  print i, " ", c, "\n";
}

// Expected program output: 255 255
