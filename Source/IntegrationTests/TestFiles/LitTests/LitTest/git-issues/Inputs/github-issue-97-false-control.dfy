predicate p(x: int)
const f := (x: int) requires p(x) => x

// assert false must still fail where the recovered precondition is in play.
lemma FalseCompanion(x: int)
  requires f.requires(x)
  ensures p(x)
{
  assert false;
}
