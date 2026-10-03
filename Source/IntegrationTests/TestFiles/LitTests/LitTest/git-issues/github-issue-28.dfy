// RUN: %baredafny run --no-verify --target cs --cs-replacement-types "%S/Inputs/github-issue-28-collections.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny run --no-verify --target cs --cs-replacement-types "%S/Inputs/github-issue-28-generic.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny run --no-verify --target cs --cs-replacement-types "%S/Inputs/github-issue-28-ordinary.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny run --no-verify --target cs "%S/Inputs/github-issue-28-ordinary.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny run --no-verify --target cs --cs-replacement-types=false "%S/Inputs/github-issue-28-ordinary.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny run --no-verify --target cs --cs-replacement-types "%s" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny run --no-verify --target cs --cs-replacement-types "%S/Inputs/github-issue-28-alias.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny run --no-verify --target cs --cs-replacement-types "%S/Inputs/github-issue-28-record.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %baredafny run --no-verify --target cs --cs-replacement-types "%S/Inputs/github-issue-28-enum.dfy" > "%t"
// RUN: %diff "%s.expect" "%t"

replaceable module Spec {
  type T(==,!new,00)
  function {:axiom} Size(t: T): int
  function {:axiom} Pick(): T
}
module Impl replaces Spec {
  newtype T = x: int | 0 <= x < 4
  function Size(t: T): int { 32 }
  function Pick(): T { 3 }
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
