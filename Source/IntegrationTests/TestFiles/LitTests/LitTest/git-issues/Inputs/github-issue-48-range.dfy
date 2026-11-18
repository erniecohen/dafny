// Inferring the type int32 for a literal does not prove that its value is in range: each of these
// resolves, and verification reports the newtype constraint at the literal.
newtype int32 = x: int | -0x8000_0000 <= x <= 0x7fff_ffff
datatype Option<+U> = None | Some(val: U)

function EarlierTooLarge(x: int32): Option<int32> {
  if x == 0 then Some(0x8000_0000) else
  var d := Some(0);
  match d
  case None => None
  case Some(n) => Some(0)
}

function LaterTooSmall(x: int32): Option<int32> {
  if x == 0 then Some(0) else
  var d := Some(0);
  match d
  case None => None
  case Some(n) => Some(-0x8000_0001)
}
