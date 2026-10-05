// Keep the ordinary backend positive reassertion and a reachable negative separate.
// RUN: %exits-with 0 %baredafny verify "%s" --filter-symbol ReassertIrrationalConstraint --solver-path "%review-z3" --cores 1 --resource-limit 200000 --verification-time-limit 20 --show-snippets:false --use-basename-for-filename > "%t"
// RUN: %exits-with 4 %baredafny verify "%s" --filter-symbol ReachableFalse --solver-path "%review-z3" --cores 1 --resource-limit 200000 --verification-time-limit 20 --show-snippets:false --use-basename-for-filename >> "%t"
// RUN: %OutputCheck --file-to-check "%t" "%s.expect"

lemma ReassertIrrationalConstraint(x: real)
  requires x * x == 2.0
{
  assert x * x == 2.0;
}

lemma ReachableFalse(x: real)
  requires x == 0.0
{
  assert false;
}
