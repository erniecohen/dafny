lemma MapIdentityKey(n: nat, e: seq<int>, j: int)
  requires |e| == n && 0 <= j < n
{
  var m := map i: int | 0 <= i < n :: i := e[i];
  assert j in m;
}

lemma MapOffsetKey(ofs: int, n: nat, e: seq<int>, j: int)
  requires |e| == n && 0 <= j < n
{
  var m := map i: int | 0 <= i < n :: ofs + i := e[i];
  var x := e[j];
  assert ofs + j in m;
}

lemma MapOffsetKeyExplicitTrigger(ofs: int, n: nat, e: seq<int>, j: int)
  requires |e| == n && 0 <= j < n
{
  var m := map i: int {:trigger e[i]} | 0 <= i < n :: ofs + i := e[i];
  var x := e[j];
  assert ofs + j in m;
}

lemma SetOffsetKeyExplicitTrigger(ofs: int, n: nat, e: seq<int>, j: int)
  requires |e| == n && 0 <= j < n
{
  var s := set i: int {:trigger e[i]} | 0 <= i < n :: ofs + i;
  var x := e[j];
  assert ofs + j in s;
}

lemma InfiniteMap(ofs: int, e: int -> int, j: int)
{
  var m := imap i: int {:trigger e(i)} :: ofs + i := e(i);
  var x := e(j);
  assert ofs + j in m;
}

lemma MultipleBinders(ofs: int, e: seq<int>, f: seq<int>, j: int, k: int)
  requires 0 <= j < |e| && 0 <= k < |f|
{
  var m := map i: int, h: int {:trigger e[i], f[h]}
    | 0 <= i < |e| && 0 <= h < |f| :: (ofs + i, h) := (e[i], f[h]);
  var x, y := e[j], f[k];
  assert (ofs + j, k) in m;
}

lemma NestedScope(ofs: int, e: seq<int>, j: int)
  requires 0 <= j < |e|
{
  var i := j;
  var m := map i: int {:trigger e[i]} | 0 <= i < |e| :: ofs + i := e[i];
  var x := e[i];
  assert ofs + i in m;
}
