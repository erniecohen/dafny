module VerificationResults {
  import Solvers
  import opened Std.Wrappers

  datatype Attempt = Attempt(sequenceNumber: nat, description: string,
    breadcrumbs: seq<string>, outcome: Solvers.ProofResult, obligationId: string)

  datatype UnitResult = UnitResult(procedureName: string, attempts: seq<Attempt>,
    complete: bool, error: Option<string>)
}
