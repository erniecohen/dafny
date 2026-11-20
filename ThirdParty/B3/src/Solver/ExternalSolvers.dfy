module ExternalSolvers {
  import opened Basics
  import opened Std.Wrappers
  import opened OSProcesses

  export
    reveals SolverSelection
    provides StartSmtSolverProcess, Send, ReadBalancedParentheses
    provides Wrappers, OSProcesses

  datatype SolverSelection = Z3 | CVC5

  method StartSmtSolverProcess(which: SolverSelection, printLog: bool) returns (r: Result<OSProcess, string>)
    ensures r.Success? ==> var process := r.value; process.Valid() && fresh(process)
  {
    var process;
    match which {
      case Z3 =>
        process :- OSProcess.Create("z3", "-in");
      case CVC5 =>
        process :- OSProcess.Create("cvc5", "--incremental");
    }

    // Initialize SMT solver with some SMT-LIB2 commands
    var _ :- Send(process, "(set-logic ALL)", printLog);
    var _ :- Send(process, "(set-option :produce-models true)", printLog);

    match which {
      case Z3 =>
        var _ :- Send(process, "(set-option :auto_config false)", printLog);
        var _ :- Send(process, "(set-option :smt.mbqi false)", printLog);
      case CVC5 =>
    }

    return Success(process);
  }

  method Send(process: OSProcess, cmd: string, printLog: bool) returns (r: Result<string, string>)
    requires process.Valid()
    modifies process
    ensures r.Success? && cmd != "(exit)" ==> process.Valid()
  {
    if printLog {
      print "smt>> ", cmd, "\n";
    }

    var _ := process.Send(cmd);

    // For simple commands like (push), (pop), there is no response from the SMT solver
    if
      || cmd == "(push)"
      || cmd == "(pop)"
      || "(assert " <= cmd
      || "(set-option " <= cmd
      || "(set-logic " <= cmd
      || "(declare-fun " <= cmd
      || "(declare-const " <= cmd
      || "(declare-sort " <= cmd
      || cmd == "(exit)"
    {
      return Success("");
    }

    // For get-model, read until we get a balanced set of parentheses
    if cmd == "(get-model)" {
      r := ReadBalancedParentheses(process);
      return;
    }

    // In all other cases, including the command check-sat, the response is a single line
    var line :- process.ReadLine();
    match line
    case None => return Success("");
    case Some(text) => return Success(text);
  }

  method ReadBalancedParentheses(process: OSProcess, maxLines: nat := 1000000) returns (r: Result<string, string>)
    requires process.Valid()
    modifies process
    ensures r.Success? ==> process.Valid()
  {
    // Read until we get a balanced set of parentheses
    var response := "";
    var parenCount: nat := 0;
    var started := false;
    // A solver is external input: bound the number of lines, rather than assume
    // that an unknown finite total length exists. Hitting the bound is failure.
    var remaining: nat := maxLines;
    while remaining != 0
      decreases remaining
    {
      remaining := remaining - 1;
      var line :- process.ReadLine();
      match line
      case None =>
        // at end of file; okay, so this is unexpected, but let's just return what we've got so far
        return Success(response);
      case Some(text) =>
        response := response + text + "\n";

        for i := 0 to |text| {
          var ch := text[i];
          if ch == '(' {
            started := true;
            parenCount := parenCount + 1;
          } else if ch == ')' {
            if !started || parenCount == 0 {
              // malformed parentheses; just return what we've got so far
              return Success(response);
            }
            parenCount := parenCount - 1;
          }
        }

        if started && parenCount == 0 {
          // perfect
          return Success(response);
        }

    }
    return Failure("solver response line limit exceeded");
  }

  const DEBUG: bool := false

  datatype Direction = ToProcess | FromProcess

  method Log(process: OSProcess, direction: Direction, msg: string) {
    if DEBUG {
      var name := process.ExecutableName();
      print name;
      match direction {
        case ToProcess => print " << ";
        case FromProcess => print " >> ";
      }
      print msg, "\n";
    }
  }
}