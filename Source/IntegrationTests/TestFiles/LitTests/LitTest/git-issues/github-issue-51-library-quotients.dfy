// Verify the actual library lemma and its Auto wrapper, rather than a caller
// whose proof could rely on the lemma's contract without checking its body.
// Bare Dafny preserves the library project's normal resource and time limits;
// the usual %verify helper would override them with a 50M resource limit.
// The exact success summary also catches an empty or partial symbol filter.
// RUN: %exits-with 0 %baredafny verify "%repositoryRoot/Source/DafnyStandardLibraries/src/Std/dfyconfig.toml" --filter-symbol LemmaIndistinguishableQuotients --solver-path "%review-z3-4.12.1" --cores 2 > "%t"
// RUN: %diff "%s.expect" "%t"
