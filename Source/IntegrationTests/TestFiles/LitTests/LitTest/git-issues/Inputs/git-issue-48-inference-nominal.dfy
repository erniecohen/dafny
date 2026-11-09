// Values whose types are already fixed are not turned into int32: each function is
// rejected in every resolver mode, with and without --conservative-match-inference.
newtype int32 = x: int | -0x8000_0000 <= x <= 0x7fff_ffff
newtype other32 = x: int | -0x8000_0000 <= x <= 0x7fff_ffff
datatype Option<+U> = None | Some(val: U)

function foo(x: int32): Option<int32> { if x == 0 then None else Some(x) }

// An explicitly typed int.
function ExplicitInt(x: int32): Option<int32> {
  var i: int := 0;
  if x == 0 then Some(i) else
  var d := foo(x);
  match d
  case None => None
  case Some(n) => Some(0)
}

// A distinct newtype with the same predicate.
function DistinctNewtype(x: int32, o: other32): Option<int32> {
  if x == 0 then Some(o) else
  var d := foo(x);
  match d
  case None => None
  case Some(n) => Some(0)
}

// A bool.
function Bool(x: int32): Option<int32> {
  if x == 0 then Some(true) else
  var d := foo(x);
  match d
  case None => None
  case Some(n) => Some(0)
}
