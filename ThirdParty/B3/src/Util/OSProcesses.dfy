module OSProcesses {
  import opened Std.Wrappers

  export
    reveals OSProcess
    provides OSProcessHandle, OSProcess.handle
    provides OSProcess.Create, OSProcess.Valid, OSProcess.ExecutableName
    provides OSProcess.Send, OSProcess.ReadLine
    provides Wrappers

  // The native handle has no logical state or contracts. I/O results are arbitrary
  // inputs to the Dafny code; none establishes a proof fact or solver success.
  class {:extern} OSProcessHandle {
    method {:extern} ExecutableName() returns (name: string)
    static method {:extern} Create(executable: string, arguments: string) returns (r: Result<OSProcessHandle, string>)
    method {:extern} Send(msg: string) returns (r: Result<(), string>)
    method {:extern} ReadLine() returns (r: Result<Option<string>, string>)
  }

  class OSProcess {
    const handle: OSProcessHandle

    constructor (handle: OSProcessHandle)
      ensures this.handle == handle
    {
      this.handle := handle;
    }

    ghost predicate Valid() { true }

    method ExecutableName() returns (name: string) { name := handle.ExecutableName(); }

    static method Create(executable: string, arguments: string) returns (r: Result<OSProcess, string>)
      ensures r.Success? ==> var p := r.value; p.Valid() && fresh(p)
    {
      var h :- OSProcessHandle.Create(executable, arguments);
      var p := new OSProcess(h);
      return Success(p);
    }

    method Send(msg: string) returns (r: Result<(), string>)
      requires Valid()
      modifies this
      ensures r.Success? ==> Valid()
    {
      r := handle.Send(msg);
    }

    method ReadLine() returns (r: Result<Option<string>, string>)
      requires Valid()
      modifies this
      ensures r.Success? ==> Valid()
    {
      r := handle.ReadLine();
    }
  }
}
