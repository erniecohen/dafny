// https://github.com/erniecohen/dafny/issues/303
// NONUNIFORM: Exercises library serialization and read-back with both resolvers.
// RUN: %baredafny build -t:lib "%S/../dafny2/CalcDefaultMainOperator.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false  --output "%t.corpus0-false.doo" > "%t.corpus0-false.build"
// RUN: %baredafny resolve "%t.corpus0-false.doo" --cores 1 --allow-warnings --type-system-refresh:false  > "%t.corpus0-false.readback"
// RUN: %baredafny build -t:lib "%S/../dafny2/CalcDefaultMainOperator.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true  --output "%t.corpus0-true.doo" > "%t.corpus0-true.build"
// RUN: %baredafny resolve "%t.corpus0-true.doo" --cores 1 --allow-warnings --type-system-refresh:true  > "%t.corpus0-true.readback"
// RUN: %baredafny build -t:lib "%S/../dafny4/Lucas-up.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false  --output "%t.corpus1-false.doo" > "%t.corpus1-false.build"
// RUN: %baredafny resolve "%t.corpus1-false.doo" --cores 1 --allow-warnings --type-system-refresh:false  > "%t.corpus1-false.readback"
// RUN: %baredafny build -t:lib "%S/../dafny4/Lucas-up.dfy" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true  --output "%t.corpus1-true.doo" > "%t.corpus1-true.build"
// RUN: %baredafny resolve "%t.corpus1-true.doo" --cores 1 --allow-warnings --type-system-refresh:true  > "%t.corpus1-true.readback"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:false --output "%t.reduced-false.doo" > "%t"
// RUN: %baredafny resolve "%t.reduced-false.doo" --cores 1 --allow-warnings --type-system-refresh:false >> "%t"
// RUN: %baredafny build -t:lib "%s" --no-verify --cores 1 --solver-path "%review-z3" --allow-warnings --type-system-refresh:true --output "%t.reduced-true.doo" >> "%t"
// RUN: %baredafny resolve "%t.reduced-true.doo" --cores 1 --allow-warnings --type-system-refresh:true >> "%t"
// RUN: %diff "%s.expect" "%t"

lemma SetCalculationSteps(s: set<int>) {
  calc {
    s;
    ({});
    ({1} + s);
  }
  calc {
    true;
    ({1} + s <= {1} + s);
  }
}
