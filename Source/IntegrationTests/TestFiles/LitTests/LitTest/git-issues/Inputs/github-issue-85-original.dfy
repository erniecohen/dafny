lemma L(n: nat) ensures n + 0 == n { }

ghost function F(o: Option): nat
{
  assert true by {
    match o {
      case None => L(0);
      case Some(n) => L(n);
    }
  }
  0
}

datatype Option = None | Some(v: nat)
