// https://github.com/erniecohen/dafny/issues/300
// NONUNIFORM: Exercises library serialization and read-back with both resolvers.
// RUN: %baredafny build -t:lib "%S/../dafny0/ParserOptions.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false  --output "%t.corpus-false.doo" > "%t.corpus-false.build"
// RUN: %baredafny resolve "%t.corpus-false.doo" --cores 1 --allow-warnings --type-system-refresh:false  > "%t.corpus-false.readback"
// RUN: %baredafny build -t:lib "%S/../dafny0/ParserOptions.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true  --output "%t.corpus-true.doo" > "%t.corpus-true.build"
// RUN: %baredafny resolve "%t.corpus-true.doo" --cores 1 --allow-warnings --type-system-refresh:true  > "%t.corpus-true.readback"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --output "%t.reduced-false.doo" > "%t"
// RUN: %baredafny resolve "%t.reduced-false.doo" --cores 1 --allow-warnings --type-system-refresh:false >> "%t"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --output "%t.reduced-true.doo" >> "%t"
// RUN: %baredafny resolve "%t.reduced-true.doo" --cores 1 --allow-warnings --type-system-refresh:true >> "%t"
// RUN: %diff "%s.expect" "%t"

module {:options "/functionSyntax:3"} Legacy {
  function method Compiled(): int { 1 }
  function Ghost(): int { 1 }
  module {:options "/functionSyntax:4"} Modern {
    function Compiled(): int { 1 }
    ghost function Ghost(): int { 1 }
  }
  function method AfterNestedModule(): int { 1 }
}
module DefaultSyntax {
  function Compiled(): int { 1 }
  ghost function Ghost(): int { 1 }
}
