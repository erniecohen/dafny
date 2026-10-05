// Private source-only draft; not yet compiled, resolved, or verified.
// Intended mode: --type-system-refresh --general-newtypes --extended-newtype-bases
// Also compare AX off/on; no expected-count oracle is established by this draft.
datatype Payload<T> = Payload(value: T)
newtype WrappedPayload<T> = Payload<T> witness *
newtype Key = p: (int, int) | p.0 >= 0 witness (0, 0)

ghost function Image(n: int): map<Key, WrappedPayload<int>>
  requires 0 <= n
{
  map i: int | 0 <= i < n ::
    ((i, n) as Key) := (Payload(i) as WrappedPayload<int>)
}

lemma UniversalImage(n: int)
  requires 0 <= n
  ensures forall i: int | 0 <= i < n ::
    ((i, n) as Key) in Image(n) &&
    Image(n)[((i, n) as Key)].value == i
{
  forall i: int | 0 <= i < n
    ensures ((i, n) as Key) in Image(n)
    ensures Image(n)[((i, n) as Key)].value == i
  {
    assert ((i, n) as Key) in Image(n);
    assert Image(n)[((i, n) as Key)] == (Payload(i) as WrappedPayload<int>);
  }
}
