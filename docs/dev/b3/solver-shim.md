# Experimental B3 solver shim

This is a milestone of [P2](https://github.com/erniecohen/dafny/issues/119), stacked on
the [transport milestone](https://github.com/erniecohen/dafny/pull/556). It adds
`DafnyB3.Solver`, a framework-only library that drives one SMT-LIB solver process
and decides, for each goal, whether the solver proved it. Nothing calls it yet:
the worker scaffold still answers `Unsupported`, ordinary Dafny does not reference
the assembly, and no Dafny option selects it. It is the boundary that a future
B3 lowering will submit its declarations and goals through, in place of the
original B3 process shim, which treats EOF and unparsed output as answers.

## What counts as a proof

`SolverSession.CheckGoalAsync(goalId, goalTerm)` returns `Proved` only when all of
the following happened, in this order, on a session that was not poisoned:

1. an echo of a fresh nonce came back as exactly that nonce (the transcript is
   aligned before the goal);
2. `(push 1)` was acknowledged by exactly `success`;
3. `(assert (not G))`, where `G` is the caller's goal re-serialized from its parse,
   was acknowledged by exactly `success`;
4. a second nonce came back exactly;
5. `(check-sat)` was answered by the simple symbol `unsat`, spelled exactly so
   (not `|unsat|`, `"unsat"`, `UNSAT`, a list, or `unsat` adjacent to another token);
6. a third nonce came back exactly, so the answer was neither duplicated nor
   followed by a late error;
7. `(pop 1)` was acknowledged, and a fourth nonce came back exactly.

`sat` is `Failed`. `unknown` is followed by `(get-info :reason-unknown)`, whose
answer must have the form `(:reason-unknown R)`: a reason naming a timeout is
`Timeout`; one naming memory or a resource is `ResourceLimit`; Z3's `canceled` is
attributed to a resource limit or timeout only when the session configured one;
anything else is `Unknown`. A wall-clock deadline is `Timeout`, caller cancellation
is `Cancelled`, and every other failure is `ToolError`. No answer other than step 5
can produce `Proved`, and a failure at any later step replaces it with `ToolError`.

The caller must also require `CloseAsync` to succeed before it reports any result
from a session: `(exit)` must be acknowledged, followed by end of output and exit
status zero. `CloseAsync` returns the session's results in order, and throws for a
poisoned session or an unclean exit.

## Lock-step correlation

SMT-LIB has no request identifiers, so correlation is by position. The session
enables `:print-success` first, sends one command at a time, and reads exactly one
response for each before sending the next. Every command except `echo`,
`check-sat` and `get-info` must be acknowledged by exactly `success`; an `(error …)`
or any other response poisons the session. A stale, duplicated, missing or
reordered response shifts the transcript, and the nonce echoes that bracket every
check detect the shift before any answer is accepted. A missing response stalls
the exchange until its deadline. A poisoned session kills the solver's process
tree, and every later call on it fails.

## Accepted input

Declarations go through `DeclareAsync`, which accepts exactly one command whose
head is the symbol `declare-sort`, `define-sort`, `declare-const`, `declare-fun`,
`define-fun`, `define-fun-rec`, `define-funs-rec`, `declare-datatype` or
`declare-datatypes`. Background assertions go through `AssertAsync`. Goals,
assertions and declarations must each parse as exactly one S-expression and are
re-serialized from the parse, so text such as `true)) (assert false` is rejected
before anything is sent. Commands that would change the stack, the options or the
output (`push`, `pop`, `check-sat`, `echo`, `set-option`, `reset`, `exit`) are not
accepted from the caller. Background assertions are premises: the shim does not
check that they are consistent, and a caller that asserts `false` proves every goal.

## Response parsing

`SmtReader` reads one complete S-expression at a time from the solver's output.
String literals (with doubled-quote escapes), quoted symbols and `;` comments are
lexed exactly, so parentheses and quotes inside them never change the structure.
End of input is legal only between expressions. A response is bounded in bytes,
including leading layout and comments (1 MiB by default), and in nesting depth
(64 by default). Control characters other than whitespace, a backslash in a quoted
symbol, tokens adjacent to a string or quoted symbol, and invalid UTF-8 are errors.

## Process boundary

The solver executable and its arguments are an explicit list, without shell
interpretation. Every exchange has a mandatory wall-clock deadline (at most one
hour). Standard error is drained continuously and at most 8,192 bytes are kept.
`:rlimit` and `:timeout` can be set at startup and must be acknowledged. Disposal
kills the live process tree and reaps it within a separate bounded wait.

## Controls

```sh
Z3=/path/to/z3-5.1.0 dotnet test Source/DafnyB3.Test -p:NuGetAudit=false --settings .github/review/ci.runsettings
```

The controls use an adversarial fake solver that answers like Z3 except for one
fault per mode: malformed, truncated, oversized, deeply nested, invalid UTF-8,
duplicated, stale, reordered and misspelled answers; errors and missing
acknowledgements for declarations, assertions, the goal, push and pop; unknown
with each reason; unresponsive and crashing solvers; stderr flooding; unclean
exits; and a solver with a child process. A recorded transcript fixes the exact
command sequence. The real-solver controls require Z3 5.1.0 (from `Z3`, or
`Binaries/z3/bin/z3-5.1.0`) and fail without it: a valid goal is proved; an
invalid goal, `false`, and a goal that holds only if an earlier goal's negation
leaked out of its scope are not; ill-typed goals and declarations are tool
errors; a matching loop under `:rlimit` is a resource limit, and under `:timeout`
a timeout.

## Not established here

This shim decides what a solver's transcript means; it does not make the solver
trustworthy. A `Proved` result is exactly as good as the solver and the premises
the caller submitted. The B3 source admission, the lowering of a Dafny unit to
these commands, and the accounting of its obligations remain the other P2
milestones listed in [the transport document](transport.md).
