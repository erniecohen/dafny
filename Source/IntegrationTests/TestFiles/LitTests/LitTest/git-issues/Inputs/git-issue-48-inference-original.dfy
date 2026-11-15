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
