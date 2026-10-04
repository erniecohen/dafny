module SolverConfiguration {
  datatype SolverKind = Z3 | CVC5

  datatype Configuration = Configuration(
    executable: string,
    arguments: seq<string>,
    timeoutMilliseconds: nat,
    resourceLimit: nat,
    maximumResponseCharacters: nat, solverKind: SolverKind := Z3, arithmeticSolver: nat := 2)
  {
    predicate Valid() {
      executable != "" && 0 < timeoutMilliseconds <= 2147483647 &&
      0 < maximumResponseCharacters <= 2147483647 && arithmeticSolver <= 6
    }
  }

  const Default := Configuration("z3", ["-in", "-smt2"], 30000, 0, 1048576)
}
