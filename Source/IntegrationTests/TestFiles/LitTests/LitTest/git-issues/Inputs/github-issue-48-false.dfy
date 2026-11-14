// A false control: the newly accepted program does not prove false.
newtype int32 = x: int | -0x8000_0000 <= x <= 0x7fff_ffff
datatype Option<+U> = None | Some(val: U)

function hoo(x: int32): Option<int32> {
  if x == 0 then Some(0) else
  var d := Some(0);
  match d
  case None => None
  case Some(n) => Some(n + 1)
}

lemma Wrong()
  ensures hoo(7) == Some(0)
{
}

lemma False()
  ensures false
{
  assert hoo(0) == Some(0) && hoo(7) == Some(1);
}
