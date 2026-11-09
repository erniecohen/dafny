// Out-of-range literals in newly accepted programs.  Each resolves with
// --conservative-match-inference and then fails verification at the literal: inferring
// the literal's type does not prove that its value satisfies int32's predicate.
newtype int32 = x: int | -0x8000_0000 <= x <= 0x7fff_ffff
datatype Option<+U> = None | Some(val: U)

// For x != 0, foo returns Some, so the Some case below is reachable.
function foo(x: int32): Option<int32> { if x == 0 then None else Some(x) }

function EarlierHigh(x: int32): Option<int32> {
  if x == 0 then Some(0x8000_0000) else
  var d := foo(x);
  match d
  case None => None
  case Some(n) => Some(0)
}

function LaterHigh(x: int32): Option<int32> {
  if x == 0 then Some(0) else
  var d := foo(x);
  match d
  case None => None
  case Some(n) => Some(0x8000_0000)
}

function EarlierLow(x: int32): Option<int32> {
  if x == 0 then Some(-0x8000_0001) else
  var d := foo(x);
  match d
  case None => None
  case Some(n) => Some(0)
}

function LaterLow(x: int32): Option<int32> {
  if x == 0 then Some(0) else
  var d := foo(x);
  match d
  case None => None
  case Some(n) => Some(-0x8000_0001)
}

// Controls: the bounds themselves are in range.
function Bounds(x: int32): Option<int32> {
  if x == 0 then Some(0x7fff_ffff) else
  var d := foo(x);
  match d
  case None => None
  case Some(n) => Some(-0x8000_0000)
}
