lemma FalseAfterMembership(ofs: int, e: seq<int>, j: int)
  requires 0 <= j < |e|
{
  var m := map i: int {:trigger e[i]} | 0 <= i < |e| :: ofs + i := e[i];
  var x := e[j];
  assert ofs + j in m;
  assert false;
}

lemma OutsideDomain(ofs: int, e: seq<int>)
{
  var m := map i: int {:trigger e[i]} | 0 <= i < |e| :: ofs + i := e[i];
  assert ofs - 1 in m;
}

lemma FalseAfterInfiniteMembership(ofs: int, e: int -> int, j: int)
{
  var m := imap i: int {:trigger e(i)} :: ofs + i := e(i);
  var x := e(j);
  assert ofs + j in m;
  assert false;
}
