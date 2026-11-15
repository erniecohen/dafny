// The declared precondition is necessary with --additional-axioms.
lemma LambdaRequiresIsOnlySufficient(o: object?)
{
  var one := (x: int) requires 0 < x reads o => x;
  // Native RequiresN alone gives only range ==> .requires.
  // The shared option additionally recovers this false declared range.
  assert !one.requires(0);
}
