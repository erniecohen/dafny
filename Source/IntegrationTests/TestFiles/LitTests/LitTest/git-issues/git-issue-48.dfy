// RUN: %baredafny resolve --type-system-refresh:false --allow-axioms --use-basename-for-filename --show-snippets:false "%s" > "%t"
// RUN: %exits-with 2 %baredafny resolve --type-system-refresh:true --allow-axioms --use-basename-for-filename --show-snippets:false "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

newtype int32 = x: int | -0x8000_0000 <= x <= 0x7fff_ffff
function foo(x: int32): Option<int32>
datatype Option<+U> = None | Some(val: U)
function goo(x: int32): Option<int32> {
  if x == 0 then Some(0) else
  var d := foo(x);
  match d
  case None => None
  case Some(n) => Some(0)
}
