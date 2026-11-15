lemma L(n: nat) ensures n + 0 == n { }

ghost function F(o: Option): nat
{
  assert true by {
    if o.None? { L(0); } else { L(o.v); }
  }
  0
}

datatype Option = None | Some(v: nat)
