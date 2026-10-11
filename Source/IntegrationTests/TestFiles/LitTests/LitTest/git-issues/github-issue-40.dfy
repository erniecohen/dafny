// RUN: %baredafny resolve --type-system-refresh:false "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny resolve --type-system-refresh:true "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

datatype D = A | B
lemma test<T>(d: D, p: T -> bool)
  requires exists x: T :: p(x)
{
  match d
  case A => var x: T :| p(x);
  case B =>
}

lemma Plain<T>(p: T -> bool)
  requires exists x: T :: p(x)
{
  var x: T :| p(x);
}

lemma Mixed<T>(d: D, p: T -> bool)
  requires exists x: T :: p(x)
{
  match d
  case A => var x: T, b: bool :| p(x) && b;
  case B => var b: bool, x: T :| b && p(x);
}
