// The match source Some(0) has a known outer head but an unknown element type.  Without
// --conservative-match-inference, the pattern identifier n needs that type (n could name a
// constructor), so the solve with decisions runs and gives the earlier literal the type int
// before the declared result type Option<int32> is seen.  With the option (the default), n is
// a bound variable: its type has the literal's type as a subtype, so it is not a datatype.
// The legacy resolver accepts the program; the refreshed resolver accepts it with the option.
newtype int32 = x: int | -0x8000_0000 <= x <= 0x7fff_ffff
datatype Option<+U> = None | Some(val: U)

function UnknownElement(x: int32): Option<int32> {
  if x == 0 then Some(0) else
  var d := Some(0);
  match d
  case None => None
  case Some(n) => Some(0)
}
