// RUN: echo "legacy resolve" > "%t"
// RUN: %exits-with 0 %baredafny resolve --type-system-refresh:false --use-basename-for-filename --show-snippets:false "%s" >> "%t"
// RUN: echo "refreshed run" >> "%t"
// RUN: %exits-with 0 %baredafny run --type-system-refresh:true --solver-path "%review-z3" --resource-limit:16000000 --verification-time-limit:30 --cores:1 --use-basename-for-filename --show-snippets:false "%s" >> "%t"
// RUN: echo "refreshed resolve, --conservative-match-inference:false" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --type-system-refresh:true --conservative-match-inference:false --use-basename-for-filename --show-snippets:false "%s" >> "%t"
// RUN: echo "constructor patterns" >> "%t"
// RUN: %exits-with 0 %baredafny verify --type-system-refresh:true --solver-path "%review-z3" --resource-limit:16000000 --verification-time-limit:30 --cores:1 --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-48-constructor.dfy" >> "%t"
// RUN: %exits-with 0 %baredafny verify --type-system-refresh:true --conservative-match-inference:false --solver-path "%review-z3" --resource-limit:16000000 --verification-time-limit:30 --cores:1 --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-48-constructor.dfy" >> "%t"
// RUN: echo "negative controls" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --type-system-refresh:false --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-48-negative.dfy" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --type-system-refresh:true --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-48-negative.dfy" >> "%t"
// RUN: echo "range controls" >> "%t"
// RUN: %exits-with 4 %baredafny verify --type-system-refresh:false --solver-path "%review-z3" --resource-limit:16000000 --verification-time-limit:30 --cores:1 --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-48-range.dfy" >> "%t"
// RUN: %exits-with 4 %baredafny verify --type-system-refresh:true --solver-path "%review-z3" --resource-limit:16000000 --verification-time-limit:30 --cores:1 --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-48-range.dfy" >> "%t"
// RUN: echo "false control" >> "%t"
// RUN: %exits-with 4 %baredafny verify --type-system-refresh:true --solver-path "%review-z3" --resource-limit:16000000 --verification-time-limit:30 --cores:1 --use-basename-for-filename --show-snippets:false "%S/Inputs/github-issue-48-false.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Regression (https://github.com/erniecohen/dafny/issues/48): the refreshed resolver rejected newtype
// literals in a covariant datatype under "if" and "match", here an int32 "Some(0)" in a function whose
// result type is Option<int32>, because resolving the match made default type decisions for the whole
// expression and gave the earlier literal the type int.  --conservative-match-inference, now on by
// default, first infers a match source by propagation, and decides that a pattern identifier whose type
// cannot be a datatype is a bound variable without making decisions.  --conservative-match-inference:false
// restores the old inference.  Ill-typed payloads are still rejected (Inputs/github-issue-48-negative.dfy),
// out-of-range literals still fail verification (Inputs/github-issue-48-range.dfy), constructor names in
// patterns are still constructors (Inputs/github-issue-48-constructor.dfy), and the accepted programs do
// not prove false (Inputs/github-issue-48-false.dfy).

newtype int32 = x: int | -0x8000_0000 <= x <= 0x7fff_ffff
datatype Option<+U> = None | Some(val: U)

function foo(x: int32): Option<int32> {
  if x < 0 then None else Some(x)
}

// The issue's program, with a body for foo.
function goo(x: int32): Option<int32> {
  if x == 0 then Some(0) else
  var d := foo(x);
  match d
  case None => None
  case Some(n) => Some(0)
}

// The match source is a constructor application whose element type is not yet known.
function hoo(x: int32): Option<int32> {
  if x == 0 then Some(0) else
  var d := Some(0);
  match d
  case None => None
  case Some(n) => Some(n + 1)
}

lemma Values()
  ensures goo(0) == Some(0) && goo(5) == Some(0) && goo(-5) == None
  ensures hoo(0) == Some(0) && hoo(7) == Some(1)
{
}

method Main() {
  print goo(0), " ", goo(-5), " ", hoo(7), "\n";
}
