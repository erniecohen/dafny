// Vacuity control: a newly accepted program does not make false provable.
newtype int32 = x: int | -0x8000_0000 <= x <= 0x7fff_ffff
datatype Option<+U> = None | Some(val: U)

function foo(x: int32): Option<int32> { if x == 0 then None else Some(x) }

function goo(x: int32): Option<int32> {
  if x == 0 then Some(0) else
  var d := foo(x);
  match d
  case None => None
  case Some(n) => Some(0)
}

method UsesGoo(x: int32) {
  var r := goo(x);
  assert r == Some(0);
  assert false; // must fail
}
