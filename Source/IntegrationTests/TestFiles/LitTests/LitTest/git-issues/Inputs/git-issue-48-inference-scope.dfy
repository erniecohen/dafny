// Cases that --conservative-match-inference does not fix (see the release note and
// docs/DafnyRef).  In each, the match source needs a type decision, the fallback
// solve with decisions runs, and its default advice fixes the earlier literal to int
// before the declared result type Option<int32> is seen.  The legacy resolver accepts
// both; the refreshed resolver rejects both, with the option on or off.
newtype int32 = x: int | -0x8000_0000 <= x <= 0x7fff_ffff
datatype Option<+U> = None | Some(val: U)

// The source is a literal.
function LiteralSource(x: int32): Option<int32> {
  if x == 0 then Some(0) else
  match 1
  case 1 => Some(0)
  case _ => None
}

// The source is Some(0): its outer head is known, its element type is not.
function UnknownElement(x: int32): Option<int32> {
  if x == 0 then Some(0) else
  var d := Some(0);
  match d
  case None => None
  case Some(n) => Some(0)
}
