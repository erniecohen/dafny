// Controls for git-issues/github-issue-363.dfy: wrong values are not proved, and the
// reductions do not prove false.

function Id(p: ORDINAL): ORDINAL { p }

function Call(f: ORDINAL -> ORDINAL, o: ORDINAL): ORDINAL { f(o) }

lemma WrongIdentity(o: ORDINAL)
  ensures ((p: ORDINAL) => p)(o) == o + 1
{}

lemma WrongConstant(o: ORDINAL)
  ensures ((x: int) => o)(0) == 0
{}

lemma WrongHandle(o: ORDINAL)
  ensures Call(Id, o) != o
{}

lemma Vacuity(o: ORDINAL)
  ensures false
{
  assert ((p: ORDINAL) => p)(o) == o;
  assert ((x: int) => o)(0) == o;
  assert Call(Id, o) == o;
}
