// RUN: %baredafny verify --use-basename-for-filename --show-snippets:false --cores=1 --filter-position-batch=:1 --filter-position-batch=:1 %S/Inputs/git-issue-173.dfy > %t
// RUN: ! %baredafny verify --use-basename-for-filename --show-snippets:false --cores=1 --filter-position-batch=:2 --filter-position-batch=:1 %S/Inputs/git-issue-173.dfy >> %t
// RUN: %baredafny verify --use-basename-for-filename --show-snippets:false --cores=1 --isolate-assertions --filter-position-batch=:1 --filter-position-batch=:1 %S/Inputs/git-issue-173.dfy >> %t
// RUN: %diff "%s.expect" "%t"
