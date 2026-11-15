// A self call in a seq constructor's initializer, in the function's own postcondition:
// F's body is 1, so its postcondition must fail.  G's postcondition holds and verifies.
ghost function F(i: int): int
  ensures seq(1, j => F(i))[0] == 0
{ 1 }

ghost function G(i: int): int
  ensures seq(1, j => G(i))[0] == G(i)
{ 1 }

lemma Contradiction()
  ensures false
{
  var x := F(0);
  assert seq(1, j => F(0))[0] == F(0);
}
