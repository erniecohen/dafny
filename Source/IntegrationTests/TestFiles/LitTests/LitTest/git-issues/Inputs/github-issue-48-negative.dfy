// Ill-typed programs that the refreshed resolver must still reject.
newtype int32 = x: int | -0x8000_0000 <= x <= 0x7fff_ffff
newtype other32 = x: int | -0x8000_0000 <= x <= 0x7fff_ffff
datatype Option<+U> = None | Some(val: U)
datatype Box<U> = Empty | Full(val: U)

// A payload whose type is already int is not an int32.
function TypedInt(x: int32, k: int): Option<int32> {
  if x == 0 then Some(k) else
  var d := Some(0);
  match d
  case None => None
  case Some(n) => Some(0)
}

// A distinct newtype with the same range is not int32.
function OtherNewtype(x: int32, k: other32): Option<int32> {
  if x == 0 then Some(k) else
  var d := Some(0);
  match d
  case None => None
  case Some(n) => Some(0)
}

// A bool is not an int32.
function Boolean(x: int32): Option<int32> {
  if x == 0 then Some(true) else
  var d := Some(0);
  match d
  case None => None
  case Some(n) => Some(0)
}

// Box is not covariant: a Box<int> is not a Box<int32>.
function NonVariant(x: int32, b: Box<int>): Box<int32> {
  if x == 0 then b else
  var d := Full(0);
  match d
  case Empty => Empty
  case Full(n) => Full(0)
}

// The bound variable n has the literal's type, not a datatype, so it cannot be used as one.
function NotADatatype(x: int32): Option<int32> {
  var d := Some(0);
  match d
  case None => None
  case Some(n) => n
}
