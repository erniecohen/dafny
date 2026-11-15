// Programs that the refreshed resolver accepts only with --conservative-match-inference.
// Each has a match whose source type follows from a declared type by propagation alone,
// and an earlier plain literal that takes its type from the enclosing result type.
newtype int32 = x: int | -0x8000_0000 <= x <= 0x7fff_ffff
datatype Option<+U> = None | Some(val: U)

function foo(x: int32): Option<int32> { if x == 0 then None else Some(x) }

// The issue's program, with a body for foo.
function goo(x: int32): Option<int32> {
  if x == 0 then Some(0) else
  var d := foo(x);
  match d
  case None => None
  case Some(n) => Some(0)
}

lemma GooIsSomeZero(x: int32)
  ensures goo(x) == Some(0)
{
}

// The source reaches the match through a chain of let aliases.
function Aliases(x: int32): Option<int32> {
  if x == 0 then Some(1) else
  var a := foo(x);
  var b := a;
  var d := b;
  match d
  case None => None
  case Some(n) => Some(2)
}

function bar(x: int32): Option<Option<int32>> { Some(foo(x)) }

// A nested pattern over a source whose type is known.
function Nested(x: int32): Option<Option<int32>> {
  if x == 0 then Some(Some(0)) else
  var d := bar(x);
  match d
  case None => None
  case Some(None) => Some(None)
  case Some(Some(n)) => Some(Some(3))
}

// Only the later literal is cast; the earlier one is plain.
function LaterCast(x: int32): Option<int32> {
  if x == 0 then Some(0) else
  var d := foo(x);
  match d
  case None => None
  case Some(n) => Some(0 as int32)
}

// A match statement in a method, with the same shape.
method M(x: int32) returns (r: Option<int32>)
  ensures r == Some(0)
{
  r := if x == 0 then Some(0) else
    var d := foo(x);
    match d
    case None => Some(0)
    case Some(n) => Some(0);
}

method Main() {
  var a := goo(0);
  var b := goo(5);
  print a.val, " ", b.val, "\n";
}
