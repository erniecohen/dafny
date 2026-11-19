// Recursion across module boundaries (https://github.com/erniecohen/dafny/issues/466), second
// stage: tools.  A program with a recursive module group is verified, with any number of
// resolution threads, with --isolate-assertions and with --filter-symbol. C# build and run
// and Python translation succeed. Stage 6 supports library builds. The option
// needs the new resolver.
// NONUNIFORM: C# execution, Python translation and archive construction exercise distinct tool paths.
// RUN: %verify --member-invariants --recursive-modules "%s" > "%t"
// RUN: %verify --member-invariants --recursive-modules --resolution-threads 1 "%s" >> "%t"
// RUN: %verify --member-invariants --recursive-modules --resolution-threads 4 "%s" >> "%t"
// RUN: %verify --member-invariants --recursive-modules --isolate-assertions "%s" >> "%t"
// RUN: %verify --member-invariants --recursive-modules --filter-symbol Count2 "%s" >> "%t"
// RUN: %build --member-invariants --recursive-modules "%s" --output "%t-build" >> "%t"
// RUN: %build --no-verify --member-invariants --recursive-modules "%s" --output "%t-build" >> "%t"
// RUN: %run --member-invariants --recursive-modules "%s" --output "%t-run" >> "%t"
// RUN: %translate py --type-system-refresh --use-basename-for-filename --show-snippets:false --standard-libraries:false --member-invariants --recursive-modules "%s" --output "%t-py" >> "%t"
// RUN: %build -t:lib --member-invariants --recursive-modules "%s" --output "%t.doo" >> "%t"
// RUN: %exits-with 2 %baredafny verify --use-basename-for-filename --show-snippets:false --standard-libraries:false --type-system-refresh:false --member-invariants --recursive-modules "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"

module Count1 {
  import Count2
  function F(n: nat): nat { if n == 0 then 0 else 1 + Count2.G(n - 1) }
  method Main() {
    print F(3), "\n";
  }
}
module Count2 {
  import Count1
  function G(n: nat): nat { if n == 0 then 0 else 1 + Count1.F(n - 1) }
}
