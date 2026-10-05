// The map carrier is infinite, but each map has only two possible keys.
// The unbounded integer witnesses do not make the Boolean image infinite.
datatype Payload<T> = Payload(value: T)
newtype WrappedPayload<T> = Payload<T> witness *

ghost function BooleanTable(n: int): map<bool, WrappedPayload<int>> {
  map i: int | true ::
    (i >= 0) := (Payload(if i >= 0 then n else n + 1) as WrappedPayload<int>)
}

lemma UniversalBooleanTable(n: int)
  ensures true in BooleanTable(n) && false in BooleanTable(n)
  ensures BooleanTable(n)[true].value == n
  ensures BooleanTable(n)[false].value == n + 1
{
  assert 0 >= 0;
  assert !(-1 >= 0);
  assert true in BooleanTable(n);
  assert false in BooleanTable(n);
}
