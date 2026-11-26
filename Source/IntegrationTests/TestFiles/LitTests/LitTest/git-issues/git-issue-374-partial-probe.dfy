// NONUNIFORM: conservatively materializes application terms outside preconditions.
// RUN: python3 "%S/Inputs/git-issue-374-partial-probe.py" "%repositoryRoot/Source/DafnyCore/DafnyPrelude.bpl" "%t.prelude"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms --heap-free-lambda-facts --prelude "%t.prelude" "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Partial.dfy" "%S/Inputs/git-issue-374-partial-negative.dfy" > "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --additional-axioms --heap-free-lambda-facts --type-system-refresh=false --general-newtypes=false --prelude "%t.prelude" "%repositoryRoot/Source/DafnyStandardLibraries/function-domains/Partial.dfy" "%S/Inputs/git-issue-374-partial-negative.dfy" >> "%t"
// RUN: %diff "%s.expect" "%t"
