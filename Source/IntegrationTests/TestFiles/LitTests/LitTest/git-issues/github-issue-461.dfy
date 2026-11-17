// https://github.com/erniecohen/dafny/issues/461: under --modular-termination, a library with a least or greatest
// predicate or lemma was refused, as if it had an {:axiom}: the {:axiom} that Dafny puts on the generated prefix
// predicate or lemma.  The controls show that the attributes and statements a user writes still refuse it.
// RUN: echo "extreme predicates and lemmas: the library builds" > "%t"
// RUN: %exits-with 0 %baredafny build -t:lib --use-basename-for-filename --show-snippets:false --standard-libraries:false --modular-termination --solver-path %review-z3 --cores 1 --output "%t.lib.doo" "%S/Inputs/github-issue-461-lib.dfy" >> "%t"
// RUN: echo "a client verifies against it" >> "%t"
// RUN: %exits-with 0 %baredafny verify --use-basename-for-filename --show-snippets:false --standard-libraries:false --modular-termination --solver-path %review-z3 --cores 1 --library "%t.lib.doo" "%S/Inputs/github-issue-461-client.dfy" >> "%t"
// RUN: echo "control: {:axiom} on a least lemma" >> "%t"
// RUN: %exits-with 3 %baredafny build -t:lib --use-basename-for-filename --show-snippets:false --standard-libraries:false --modular-termination --solver-path %review-z3 --cores 1 --output "%t.axiom.doo" "%S/Inputs/github-issue-461-axiom.dfy" >> "%t"
// RUN: echo "control: {:verify false} on a least lemma" >> "%t"
// RUN: %exits-with 3 %baredafny build -t:lib --use-basename-for-filename --show-snippets:false --standard-libraries:false --modular-termination --solver-path %review-z3 --cores 1 --allow-warnings --output "%t.vf.doo" "%S/Inputs/github-issue-461-verify-false.dfy" >> "%t"
// RUN: echo "control: assume in a least lemma" >> "%t"
// RUN: %exits-with 3 %baredafny build -t:lib --use-basename-for-filename --show-snippets:false --standard-libraries:false --modular-termination --solver-path %review-z3 --cores 1 --allow-warnings --output "%t.assume.doo" "%S/Inputs/github-issue-461-assume.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
