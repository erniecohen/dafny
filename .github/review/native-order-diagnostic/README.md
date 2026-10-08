# Native support-order diagnostic

This scratch-only workflow compiles instrumentation for the native command-line
verification path. No solver workload runs in the build workflow. Actual build,
download and assembly stages have separate exit-status artifacts; the outer
workflow records failures without promoting them to successful diagnostics.

The first attempt instrumented the legacy driver translation entry, which the
new CLI does not use. That attempt produced no instrumentation audit and was
rejected. Its rebuilt core identity also differed. Neither rebuilt core is used.

The current build changes only the language-server verification frontend at
`DafnyProgramVerifier.DoTranslation`, before the native program reaches Boogie.
It assembles that component onto the exact preceding public build at semantic
product `a709df226`; every other binary, including the verifier core and driver,
must remain byte exact. Applied source hashes and assembly provenance are saved.
This diagnostic is not part of the product PR.

For two named regression methods, instrumentation may move one existing can-call
assumption across a validated pure private-argument fragment within one native
block, or add a deliberate false entry check. It retains every original command,
actual check and branch-transfer object. Unchanged controls must reproduce prior
native queries, outcomes and resource vectors before causal attribution. Original
scopes, metadata, expression objects, fuel and facts remain in the native AST;
printed Boogie is not reparsed.
