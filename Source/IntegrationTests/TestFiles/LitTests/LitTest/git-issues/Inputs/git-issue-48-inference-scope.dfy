// A case that --conservative-match-inference does not fix (see docs/DafnyRef).  The
// match source Some(0) has a known outer head but an unknown element type, so
// propagation alone leaves it a proxy, the fallback solve with decisions runs, and its
// default advice fixes the earlier literal to int before the declared result type
// Option<int32> is seen.  The legacy resolver accepts it; the refreshed resolver
// rejects it, with the option on or off.
newtype int32 = x: int | -0x8000_0000 <= x <= 0x7fff_ffff
datatype Option<+U> = None | Some(val: U)

function UnknownElement(x: int32): Option<int32> {
  if x == 0 then Some(0) else
  var d := Some(0);
  match d
  case None => None
  case Some(n) => Some(0)
}
