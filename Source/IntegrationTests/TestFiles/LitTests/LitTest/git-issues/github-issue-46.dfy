// RUN: %baredafny resolve --type-system-refresh:true --general-newtypes:true --use-basename-for-filename --show-snippets:false "%s" > "%t"
// RUN: %exits-with 2 %baredafny resolve --type-system-refresh:true --general-newtypes:true --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-46-range.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// The original crash: the synthetic receiver T has a type but no literal value.
newtype T = bv1 { static const c: int := 0 }
const c := T.c

module BareDeclaration {
  newtype T = bv1 { static const c: int := 0 }
}

newtype Byte = bv8 {
  static const c: int := 1
  static function F(): int { 2 }
}
newtype Wide = bv64 {
  static const c: int := 3
  static function F(): int { 4 }
}
function SelectByte(x: Byte): int { Byte.c + Byte.F() + x.c + x.F() }
function SelectWide(x: Wide): int { Wide.c + Wide.F() + x.c + x.F() }

// Valid boundary literals still resolve.
const small: bv1 := 1
const largestByte: bv8 := 255
