module CheckedLookupVacuity {
  import I = Incarnations
  import Ast
  import OSProcesses

  lemma NoContradiction()
    ensures false
  {
  }

  // A checked lookup may abort, but a caller cannot use its normal return to
  // establish an arbitrary Boolean as the old pure Expect function allowed.
  method LookupDoesNotImplyFalse(inc: I.Incarnations, v: Ast.Variable)
    ensures false
  {
    var value := inc.Get(v);
  }

  // Native input and the checked fresh wrapper establish no arbitrary fact.
  method ProcessDoesNotImplyFalse()
  {
    var result := OSProcesses.OSProcess.Create("unused", "");
    if result.Success? {
      assert fresh(result.value) && result.value.Valid();
      assert false;
    }
  }
}
