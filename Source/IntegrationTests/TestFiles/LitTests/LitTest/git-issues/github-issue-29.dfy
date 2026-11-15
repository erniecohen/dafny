// RUN: %baredafny run --no-verify --target py --python-reorder-replacements "%S/Inputs/github-issue-29-nested.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny run --no-verify --target py --python-reorder-replacements "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny run --no-verify --target py --python-reorder-replacements "%S/Inputs/github-issue-29-early.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny run --no-verify --target py --python-reorder-replacements "%S/Inputs/github-issue-29-helper.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny run --no-verify --target py --python-reorder-replacements "%S/Inputs/github-issue-29-chain.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny run --no-verify --target py --python-reorder-replacements "%S/Inputs/github-issue-29-eager.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny run --no-verify --target py --python-reorder-replacements "%S/Inputs/github-issue-29-ordinary.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny run --no-verify --target py "%S/Inputs/github-issue-29-early.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny run --no-verify --target py "%S/Inputs/github-issue-29-ordinary.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"

// RUN: %baredafny run --no-verify --target py --python-reorder-replacements=false "%S/Inputs/github-issue-29-early.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny run --no-verify --target py --python-reorder-replacements=false "%S/Inputs/github-issue-29-ordinary.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"

// RUN: %exits-with 3 %baredafny run --no-verify --target py --python-reorder-replacements --show-snippets:false --use-basename-for-filename "%S/Inputs/github-issue-29-cycle.dfy" > "%t"
// RUN: %diff "%S/Inputs/github-issue-29-cycle.dfy.expect" "%t"

replaceable module Spec {
  type T(==,!new,00)
  function {:axiom} Size(t: T): int
  function {:axiom} Pick(): T
}
module Client {
  import opened Spec
  function Twice(t: T): int { 2 * Size(t) }
}
module App {
  import Client
  import opened Spec
  method Main() { print Client.Twice(Pick()), "\n"; }
}
module Impl replaces Spec {
  newtype T = x: int | 0 <= x < 4
  function Size(t: T): int { 32 }
  function Pick(): T { 3 }
}
