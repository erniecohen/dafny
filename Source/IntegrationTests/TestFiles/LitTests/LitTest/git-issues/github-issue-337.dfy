// RUN: %build -t:lib "%s" --output "%t.doo" > "%t"
// RUN: %exits-with 4 %verify --library "%t.doo" "%S/Inputs/github-issue-337-client.dfy" >> "%t"
// RUN: echo "Legacy resolver" >> "%t"
// RUN: %baredafny build -t:lib --cores:2 --standard-libraries:false --use-basename-for-filename --show-snippets:false --type-system-refresh:false "%s" --output "%t.legacy.doo" >> "%t"
// RUN: %exits-with 4 %baredafny verify --cores:2 --standard-libraries:false --use-basename-for-filename --show-snippets:false --type-system-refresh:false --library "%t.legacy.doo" "%S/Inputs/github-issue-337-client.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
// RUN: %resolve --print "%t.printed.dfy" "%s" > "%t.resolve"
// RUN: %OutputCheck --file-to-check "%t.printed.dfy" "%s"

// https://github.com/erniecohen/dafny/issues/337
// The printer, which writes the program a .doo file stores, wrote `opaque const` as `const`.  A
// client of the library then saw the constants' values, and its `reveal` of them did not resolve.
// The client reveals each opaque constant where it needs its value; the one lemma that does not
// reveal must fail, which shows that the constants are still opaque.

module Lib {
  opaque const c := 5
  ghost opaque const g: int := 6
  class K {
    static opaque const s: int := 7
    opaque const i: int := 8
    constructor () {}
  }
  const plain := 9
}

// The printed program keeps every modifier.
// CHECK: opaque const c := 5
// CHECK: ghost opaque const g: int := 6
// CHECK: const plain := 9
// CHECK: static opaque const s: int := 7
// CHECK: opaque const i: int := 8
