function ApplyFirst<T, Q>(pair: (T, Q), fT: T --> bool) : bool
  requires fT.requires(pair.0)
{
  fT(pair.0)
}

function NOK<T, Q>(pair: (T, Q), fT: T -> bool) : bool {
  assert fT.requires(pair.0);
  ApplyFirst(pair, fT)
}
