// A source lambda precondition is sufficient for its .requires property.
lemma LambdaRequiresIsOnlySufficient(o: object?)
{
  var one := (x: int) requires 0 < x reads o => x;
  // The existing RequiresN encoding gives only range ==> .requires.
  // A false source range does not establish a false .requires property.
  assert !one.requires(0);
}
