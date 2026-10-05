// Arbitrary captures and renamed actual arguments must give separate maps.
datatype Payload<T> = Payload(value: T)
newtype WrappedPayload<T> = Payload<T> witness *
newtype Key = p: (int, int) | p.0 >= 0 witness (0, 0)

ghost function Captured(n: int): map<Key, WrappedPayload<int>> {
  map i: int | i in {0, 1} ::
    ((i, n) as Key) := (Payload(i + n) as WrappedPayload<int>)
}

lemma UniversalCaptures(n: int, m: int)
  ensures ((0, n) as Key) in Captured(n)
  ensures Captured(n)[((0, n) as Key)].value == n
  ensures Captured(n)[((1, n) as Key)].value == n + 1
  ensures Captured(m)[((0, m) as Key)].value == m
  ensures Captured(n + 1)[((1, n + 1) as Key)].value == n + 2
{
  var first := Captured(n);
  var second := Captured(m);
  var third := Captured(n + 1);
  assert first[((0, n) as Key)].value == n;
  assert first[((1, n) as Key)].value == n + 1;
  assert second[((0, m) as Key)].value == m;
  assert third[((1, n + 1) as Key)].value == n + 2;
}
