// Recursion across module boundaries (https://github.com/erniecohen/dafny/issues/466) is opt-in.
// Without --recursive-modules, a cycle of imports is the same error as before, under both resolvers
// and with --member-invariants.  The option requires --member-invariants, which requires
// --type-system-refresh.  With the option, the modules of the cycle are a recursive module group and
// the program is accepted.
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh:false "%s" > "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh "%s" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh --member-invariants "%s" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh --recursive-modules "%s" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh:false --recursive-modules "%s" >> "%t"
// RUN: %exits-with 2 %baredafny resolve --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh:false --member-invariants --recursive-modules "%s" >> "%t"
// RUN: %baredafny resolve --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh --member-invariants --recursive-modules "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

module A {
  import B
  function F(n: nat): nat { if n == 0 then 0 else B.G(n - 1) }
}

module B {
  import A
  function G(n: nat): nat { if n == 0 then 0 else A.F(n - 1) }
}
