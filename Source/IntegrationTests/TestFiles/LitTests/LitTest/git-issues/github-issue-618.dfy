// RUN: %exits-with 4 %verify --solver-path "%review-z3" --cores 1 --resource-limit 200000 --solver-option O:smt.bv.solver=2 "%s" > "%t"
// RUN: echo "solver-option without O:" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --cores 1 --resource-limit 200000 --solver-option smt.bv.solver=2 "%s" >> "%t"
// RUN: echo "solver-option, upper case name and padded value" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --cores 1 --resource-limit 200000 --solver-option O:SMT.BV.SOLVER=02 "%s" >> "%t"
// RUN: echo "solver-option, hexadecimal value" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --cores 1 --resource-limit 200000 --solver-option O:smt.bv.solver=#x2 "%s" >> "%t"
// RUN: echo "solver-option, value that is not a number" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --cores 1 --resource-limit 200000 --solver-option O:smt.bv.solver=two "%s" >> "%t"
// RUN: echo "boogie /proverOpt:" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --cores 1 --resource-limit 200000 --boogie /proverOpt:O:smt.bv.solver=2 "%s" >> "%t"
// RUN: echo "project file, solver-option" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --cores 1 --resource-limit 200000 "%S/Inputs/github-issue-618-solver-option/dfyconfig.toml" >> "%t"
// RUN: echo "project file, boogie" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --cores 1 --resource-limit 200000 "%S/Inputs/github-issue-618-boogie/dfyconfig.toml" >> "%t"
// RUN: echo "legacy command line, /proverOpt:" >> "%t"
// RUN: %exits-with 4 %baredafny /compile:0 /useBaseNameForFileName /proverOpt:PROVER_PATH=%review-z3 /proverOpt:O:smt.bv.solver=2 "%s" >> "%t"
// RUN: echo "legacy-allow-unsound-bv-solver: the false assertion verifies, with a warning" >> "%t"
// RUN: %verify --solver-path "%review-z3" --cores 1 --resource-limit 200000 --solver-option O:smt.bv.solver=2 --legacy-allow-unsound-bv-solver --allow-warnings "%s" >> "%t"
// RUN: echo "legacy-allow-unsound-bv-solver, project file" >> "%t"
// RUN: %verify --solver-path "%review-z3" --cores 1 --resource-limit 200000 --legacy-allow-unsound-bv-solver --allow-warnings "%S/Inputs/github-issue-618-solver-option/dfyconfig.toml" >> "%t"
// RUN: echo "default bit-vector solver: the false assertion fails" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --cores 1 --resource-limit 200000 "%s" >> "%t"
// RUN: echo "smt.bv.solver=0" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --cores 1 --resource-limit 200000 --solver-option O:smt.bv.solver=0 "%s" >> "%t"
// RUN: echo "smt.bv.solver=1" >> "%t"
// RUN: %exits-with 4 %verify --solver-path "%review-z3" --cores 1 --resource-limit 200000 --solver-option O:smt.bv.solver=1 "%s" >> "%t"
// RUN: %diff "%s.expect" "%t"
// Z3's int-blasting bit-vector solver, smt.bv.solver=2, proves the false assertion below
// (https://github.com/erniecohen/dafny/issues/618). A solver option that selects it is rejected,
// before verification, in every spelling and through every way an option reaches the solver.

lemma Witness() {
  var x: bv8 := 1;
  var y: bv8 := 2;
  var z := x - y;
  assert false;
}
