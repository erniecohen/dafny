# Checked worker library

`library/dfyconfig.toml` builds the C# library without the command-line entry
point. The normal project configuration continues to build the B3 command-line
adapter. For example:

```sh
dafny build library/dfyconfig.toml --target cs --output build/B3Worker
dafny test --no-verify test/worker/dfyconfig.toml --output build/worker-tests
```

The test command checks runtime protocol behavior. It does not verify the Dafny
source; run a verifying build separately using the project's pinned compiler and
solver.

`B3Library.CheckAndVerify` accepts a `RawAst.Program`, a procedure name, and a
`SolverConfiguration.Configuration`. It rejects unsupported inputs before
running resolution, type checking, static consistency checking, and verification
of the selected procedure. Domains, signature types, domain instantiations,
custom literals, closure expressions, reachability statements, and unsafe SMT
identifiers are currently excluded from this boundary.

Native `real` uses the SMT Real carrier, with exact rational literals whose
denominator is positive. Arithmetic uses homogeneous Int or Real operands;
integer `div`/`mod` stay integer-only. Real division, integer embedding, and
floor use the native SMT primitives. The text forms are `#real(n, d)`,
`#to_real(e)`, and `#to_int(e)`. Raw invalid denominators and mixed numeric
signatures fail before verification. Parser/printer and runtime controls are in
`test/worker/RealTests.dfy`. The source extension requires a fresh verifying
library build; an earlier library receipt does not apply to this source.

The generated C# method returns `VerificationResults._IUnitResult`. Each attempt
has a sequence number, description, breadcrumbs, outcome, and obligation ID.
Place an obligation's ID on its outermost `RawAst.LabeledExpr` to retain that ID
without parsing diagnostic text. Outcomes distinguish proved (`unsat`), failed
(`sat`), inconclusive (`unknown`, including the solver's reason), and tool error.
The structured path does not request models; the command-line adapter retains
raw model diagnostics for failed attempts.

`complete` records that the selected procedure's traversal finished. It is not a
successful verdict: callers must also inspect the unit's error and every attempt.
An error in startup, an SMT acknowledgment, a query, restoration, or cleanup is a
tool error. Any session error poisons that session; no later command or result can
be accepted as a proof. Restoration failures override an earlier `unsat` result.
Source well-formedness and ghost stack contracts describe B3's client traversal;
they do not claim that a rejected SMT command changed the external solver state.

Solver configuration uses an explicit executable and argument sequence, an IO
deadline, a Z3 resource limit, a response bound, solver kind, and arithmetic solver
selection. The deadline and response bound must be positive and fit signed 32-bit
integers. A zero resource limit means no configured Z3 resource cap. Arithmetic
solver values 0 through 6 are forwarded to Z3. Explicit resource limits and
nondefault arithmetic settings are rejected for CVC5. The process boundary uses
argument lists, bounded stderr retention, framed SMT responses, EOF detection,
and termination on IO failure. A supervising host must also terminate its worker
process group on cancellation or worker failure.

The existing B3 source contracts are preserved. Native process methods are an
explicit trusted boundary. The current upstream semantics work does not supply a
soundness theorem for the complete B3 verification pipeline; passing these checks
must not be described as such a theorem.
