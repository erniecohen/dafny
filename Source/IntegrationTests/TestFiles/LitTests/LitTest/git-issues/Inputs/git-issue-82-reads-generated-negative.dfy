// These function-valued clauses both generate finite _reads comprehensions.
ghost function ReadFamily(f: int -> set<object?>): int
  reads f
{
  0
}

lemma GeneratedReadsRemainConsistent(o: object?)
{
  var family: int -> set<object?> := (x: int) => {o};
  ghost var finite := {o};
  assert forall n: int :: family(n) == finite;
  assert forall n: int, q: object? :: q in family(n) ==> allocated(q);
  var named := ReadFamily.reads(family);
  var outer := (x: int) reads family => x;
  var lambda := outer.reads(0);
  assert named == {o};
  assert lambda == {o};
  assert false;
}
