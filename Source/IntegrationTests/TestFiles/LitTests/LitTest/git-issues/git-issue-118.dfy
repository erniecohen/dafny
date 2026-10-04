// Preserve default Boogie success, failure diagnostics, source positions and isolated batches.
// RUN: %exits-with 0 %baredafny verify "%s" --filter-symbol Positive --solver-path "%review-z3" --cores 1 --resource-limit 200000 --verification-time-limit 0 --show-snippets:false --use-basename-for-filename > "%t"
// RUN: %exits-with 4 %baredafny verify "%s" --filter-symbol Negative --solver-path "%review-z3" --cores 1 --resource-limit 200000 --verification-time-limit 0 --show-snippets:false --use-basename-for-filename >> "%t"
// RUN: %exits-with 0 %baredafny verify "%s" --filter-symbol Isolated --solver-path "%review-z3" --cores 1 --resource-limit 200000 --verification-time-limit 0 --show-snippets:false --use-basename-for-filename >> "%t"
// RUN: %diff "%s.expect" "%t"

method Positive(x: int) returns (y: int)
  ensures y > x
{
  y := x + 1;
  assert y > x;
}

method Negative(x: int) {
  assert x == 0;
}

method {:isolate_assertions} Isolated(x: int) {
  assert x + 1 > x;
  assert x * 0 == 0;
}
