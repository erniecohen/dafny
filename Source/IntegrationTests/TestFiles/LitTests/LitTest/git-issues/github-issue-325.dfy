// RUN: %build -t:lib "%s" --output "%t.n3b.doo" > "%t"
// RUN: %exits-with 4 %verify --library "%t.n3b.doo" "%S/Inputs/github-issue-325-client.dfy" >> "%t"
// RUN: %build -t:lib "%S/Inputs/github-issue-325-lib.dfy" --output "%t.lib.doo" >> "%t"
// RUN: %exits-with 4 %verify --library "%t.lib.doo" "%S/Inputs/github-issue-325-lib-client.dfy" >> "%t"
// RUN: echo "Legacy resolver" >> "%t"
// RUN: %baredafny build -t:lib --cores:2 --standard-libraries:false --use-basename-for-filename --show-snippets:false --type-system-refresh:false "%s" --output "%t.n3b-legacy.doo" >> "%t"
// RUN: %exits-with 4 %baredafny verify --cores:2 --standard-libraries:false --use-basename-for-filename --show-snippets:false --type-system-refresh:false --library "%t.n3b-legacy.doo" "%S/Inputs/github-issue-325-client.dfy" >> "%t"
// RUN: %baredafny build -t:lib --cores:2 --standard-libraries:false --use-basename-for-filename --show-snippets:false --type-system-refresh:false "%S/Inputs/github-issue-325-lib.dfy" --output "%t.lib-legacy.doo" >> "%t"
// RUN: %exits-with 4 %baredafny verify --cores:2 --standard-libraries:false --use-basename-for-filename --show-snippets:false --type-system-refresh:false --library "%t.lib-legacy.doo" "%S/Inputs/github-issue-325-lib-client.dfy" >> "%t"
// RUN: echo "Trait lemma marked {:opaque_reveal}" >> "%t"
// RUN: %exits-with 2 %resolve "%S/Inputs/github-issue-325-trait.dfy" >> "%t"
// RUN: echo "Auditor" >> "%t"
// RUN: %baredafny audit --use-basename-for-filename "%S/Inputs/github-issue-325-audit.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"

// https://github.com/erniecohen/dafny/issues/325
// The library printer left out of a .doo every member marked {:auto_generated}, including members on
// which the user wrote the attribute.  Here F's own f disappeared from the stored program, Bad's
// postcondition then named G.f, and the client proved false.  The printer now leaves out only the
// members Dafny generated (reveal lemmas), recognised by their origin.  The same origin test now
// decides whether a {:opaque_reveal} trait lemma may go unimplemented (github-issue-325-trait.dfy) and
// whether the auditor reports {:axiom} and {:verify false} (github-issue-325-audit.dfy).

module G {
  function f(): int { 2 }
}

module F {
  import opened G
  function {:auto_generated} f(): int { 1 }
  lemma Bad() ensures f() == 1 {}
}
