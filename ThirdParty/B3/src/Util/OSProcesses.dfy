module OSProcesses {
  import opened Std.Wrappers

  export
    reveals OSProcess
    provides OSProcess.Create, OSProcess.Valid, OSProcess.ExecutableName
    provides OSProcess.Send, OSProcess.ReadLine, OSProcess.ReadResponse, OSProcess.Dispose
    provides Wrappers

  class {:extern} OSProcess {
    ghost predicate {:extern} Valid()
    function {:extern} ExecutableName(): string

    // The external boundary guarantees wrapper integrity, not solver truth.
    @Axiom
    static method {:extern} Create(executable: string, arguments: seq<string>,
      timeoutMilliseconds: nat, maximumResponseCharacters: nat)
      returns (r: Result<OSProcess, string>)
      ensures r.Success? ==> var p := r.value; p.Valid() && fresh(p)

    @Axiom
    method {:extern} Send(msg: string) returns (r: Result<(), string>)
      requires Valid()
      modifies this
      ensures Valid()

    // Success(None) means EOF before a response. IO and truncation are failures.
    @Axiom
    method {:extern} ReadLine() returns (r: Result<Option<string>, string>)
      requires Valid()
      modifies this
      ensures Valid()

    @Axiom
    method {:extern} ReadResponse() returns (r: Result<Option<string>, string>)
      requires Valid()
      modifies this
      ensures Valid()

    @Axiom
    method {:extern} Dispose() returns (r: Result<(), string>)
      requires Valid()
      modifies this
      ensures Valid()
  }
}
