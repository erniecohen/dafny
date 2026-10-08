# Native support-order diagnostic

This scratch-only build compares a pristine driver with an instrumented driver
at the same supported-line product pin. No solver workload runs in this workflow.
Every actual build/identity stage has a separate exit-status artifact; the outer
workflow records expected diagnostic failures without promoting them to success.

Instrumentation is enabled after the pristine build. It changes only the driver,
with the applied source hashes recorded, and requires both builds to have an
identical DafnyCore assembly. It is not part of the product PR.

For two named existing regression methods, a diagnostic may move one existing
can-call assumption across a validated pure private-argument preparation fragment
within one native block, or add a deliberate false entry check. It retains every
original command/check object and every branch-transfer object. Unchanged native
controls must reproduce the preceding native query and result before attributing
any movement to this diagnostic. Original scopes, metadata, expression objects,
fuel and facts remain in the native AST; printed Boogie is not reparsed.
