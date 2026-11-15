// https://github.com/erniecohen/dafny/issues/280
// NONUNIFORM: Exercises library serialization and read-back with both resolvers.
// RUN: %baredafny build -t:lib "%S/git-issue-1963c.dfy" --no-verify --allow-warnings --type-system-refresh:false --output "%t-1963c-legacy.doo" > "%t"
// RUN: %baredafny resolve --allow-warnings --use-basename-for-filename --show-snippets:false "%t-1963c-legacy.doo" --type-system-refresh:false >> "%t"
// RUN: %baredafny build -t:lib "%S/git-issue-1963c.dfy" --no-verify --allow-warnings --type-system-refresh:true --output "%t-1963c-refreshed.doo" >> "%t"
// RUN: %baredafny resolve --allow-warnings --use-basename-for-filename --show-snippets:false "%t-1963c-refreshed.doo" --type-system-refresh:true >> "%t"
// RUN: %baredafny build -t:lib "%S/git-issue-1180b.dfy" --no-verify --allow-warnings --type-system-refresh:false --output "%t-1180b-legacy.doo" >> "%t"
// RUN: %baredafny resolve --allow-warnings --use-basename-for-filename --show-snippets:false "%t-1180b-legacy.doo" --type-system-refresh:false >> "%t"
// RUN: %baredafny build -t:lib "%S/git-issue-1180b.dfy" --no-verify --allow-warnings --type-system-refresh:true --output "%t-1180b-refreshed.doo" >> "%t"
// RUN: %baredafny resolve --allow-warnings --use-basename-for-filename --show-snippets:false "%t-1180b-refreshed.doo" --type-system-refresh:true >> "%t"
// RUN: %baredafny build -t:lib "%s" --no-verify --allow-warnings --type-system-refresh:false --output "%t-legacy.doo" >> "%t"
// RUN: %baredafny verify --allow-warnings --use-basename-for-filename --show-snippets:false --solver-path %review-z3 --cores 1 --resource-limit 16000000 "%S/Inputs/git-issue-280-client.dfy" --library "%t-legacy.doo" --type-system-refresh:false --filter-symbol Good >> "%t"
// RUN: %exits-with 4 %baredafny verify --allow-warnings --use-basename-for-filename --show-snippets:false --solver-path %review-z3 --cores 1 --resource-limit 16000000 "%S/Inputs/git-issue-280-client.dfy" --library "%t-legacy.doo" --type-system-refresh:false --filter-symbol Bad >> "%t"
// RUN: %baredafny build -t:lib "%s" --no-verify --allow-warnings --type-system-refresh:true --output "%t-refreshed.doo" >> "%t"
// RUN: %baredafny verify --allow-warnings --use-basename-for-filename --show-snippets:false --solver-path %review-z3 --cores 1 --resource-limit 16000000 "%S/Inputs/git-issue-280-client.dfy" --library "%t-refreshed.doo" --type-system-refresh:true --filter-symbol Good >> "%t"
// RUN: %exits-with 4 %baredafny verify --allow-warnings --use-basename-for-filename --show-snippets:false --solver-path %review-z3 --cores 1 --resource-limit 16000000 "%S/Inputs/git-issue-280-client.dfy" --library "%t-refreshed.doo" --type-system-refresh:true --filter-symbol Bad >> "%t"
// RUN: %diff "%s.expect" "%t"

abstract module Base {
  newtype Plain = int {
    function Value(): int { this as int }
  }
  newtype Positive = x: int | x > 0 witness 1 {
    function Value(): int { this as int }
  }
  datatype D = D(x: int) {
    function Value(): int { x }
  }
  codatatype C = C(x: int) {
    function Value(): int { x }
  }
}

module Refined refines Base {
  newtype Plain ... { function Value ... }
  newtype Positive ... { function Value ... }
  datatype D ... { function Value ... }
  codatatype C ... { function Value ... }
}

module Leaf refines Refined {
  newtype Plain ...
  newtype Positive ...
  datatype D ...
  codatatype C ...
}
