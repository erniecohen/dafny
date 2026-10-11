datatype Option = None | Some(v: nat)

ghost function BadBranch(o: Option): nat
{
  assert true by {
    match o {
      case None => assert false;
      case Some(n) => assert n < 0;
    }
  }
  0
}

ghost function BadConclusion(o: Option): nat
{
  assert false by {
    match o {
      case None => assert true;
      case Some(n) => assert n >= 0;
    }
  }
  0
}
