module ExternalSolvers {
  import opened Basics
  import opened Std.Wrappers
  import opened OSProcesses
  import SolverConfiguration

  export
    reveals SolverSelection
    provides StartSmtSolverProcess, StartConfiguredSolver, Send
    provides Wrappers, OSProcesses, SolverConfiguration

  datatype SolverSelection = Z3 | CVC5

  method StartSmtSolverProcess(which: SolverSelection, printLog: bool) returns (r: Result<OSProcess, string>)
    ensures r.Success? ==> var process := r.value; process.Valid() && fresh(process)
  {
    var configuration := if which == Z3 then SolverConfiguration.Default else
      SolverConfiguration.Configuration("cvc5", ["--incremental"], 30000, 0, 1048576, SolverConfiguration.CVC5);
    r := StartConfiguredSolver(configuration, printLog);
  }

  method StartConfiguredSolver(configuration: SolverConfiguration.Configuration, printLog: bool)
    returns (r: Result<OSProcess, string>)
    ensures r.Success? ==> var process := r.value; process.Valid() && fresh(process)
  {
    if !configuration.Valid() { return Result<OSProcess, string>.Failure("invalid solver configuration"); }
    var process :- OSProcess.Create(configuration.executable, configuration.arguments,
      configuration.timeoutMilliseconds, configuration.maximumResponseCharacters);
    var commands := ["(set-option :print-success true)", "(set-logic ALL)",
      "(set-option :produce-models true)"];
    for i := 0 to |commands|
      invariant process.Valid() && fresh(process)
    {
      var ack := Send(process, commands[i], printLog);
      if ack.Failure? {
        var _ := process.Dispose();
        return Result<OSProcess, string>.Failure(ack.error);
      }
    }
    if configuration.solverKind == SolverConfiguration.Z3 {
      var options := ["(set-option :auto_config false)", "(set-option :smt.mbqi false)",
        "(set-option :smt.arith.solver " + Int2String(configuration.arithmeticSolver) + ")"];
      if configuration.resourceLimit != 0 {
        options := options + ["(set-option :rlimit " + Int2String(configuration.resourceLimit) + ")"];
      }
      options := options + ["(set-option :timeout " + Int2String(configuration.timeoutMilliseconds) + ")"];
      for i := 0 to |options|
        invariant process.Valid() && fresh(process)
      {
        var ack := Send(process, options[i], printLog);
        if ack.Failure? {
          var _ := process.Dispose();
          return Result<OSProcess, string>.Failure(ack.error);
        }
      }
    } else if configuration.resourceLimit != 0 || configuration.arithmeticSolver != 2 {
      var _ := process.Dispose();
      return Result<OSProcess, string>.Failure("resource limits require an explicit Z3 configuration");
    }
    return Success(process);
  }

  method Send(process: OSProcess, cmd: string, printLog: bool) returns (r: Result<string, string>)
    requires process.Valid()
    modifies process
    ensures process.Valid()
  {
    if printLog { print "smt>> ", cmd, "\n"; }
    var _ :- process.Send(cmd);
    if cmd == "(exit)" {
      var _ :- process.Dispose();
      return Success("");
    }
    var response :- process.ReadResponse();
    if response.None? { return Failure("unexpected EOF from solver"); }
    var text := response.value;
    var head := 0;
    if text != "" && text[0] == '(' {
      head := 1;
      while head < |text| && text[head] in " \t\r\n"
        invariant 1 <= head <= |text|
      {
        head := head + 1;
      }
    }
    if (head != 0 && "error" <= text[head..]) || text == "unsupported" || text == "" {
      return Failure("solver command failed: " + text);
    }
    if cmd == "(check-sat)" {
      if text != "unsat" && text != "sat" && text != "unknown" {
        return Failure("invalid check-sat response: " + text);
      }
    } else if cmd != "(get-model)" && cmd != "(get-info :reason-unknown)" {
      if text != "success" { return Failure("expected solver acknowledgment, received: " + text); }
    } else if text[0] != '(' {
      return Failure("expected solver S-expression, received: " + text);
    }
    return Success(text);
  }
}
