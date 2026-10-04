// Private unexecuted control: ordinary decreasing function calls are unchanged.
function Natural(n: nat): nat
  ensures Natural(n) == n
  decreases n
{
  if n == 0 then 0 else 1 + Natural(n - 1)
}
lemma NaturalWorks(n: nat) ensures Natural(n) == n {}
