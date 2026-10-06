# Obligation lowering validation status

The change for [issue 100](https://github.com/erniecohen/dafny/issues/100)
is experimental and default off. It has not been merged or released.
The local arguments and producer classifications are in
[obligation-lowering.md](obligation-lowering.md).

## Current development correction

The development source now includes the checked-ensures publication and legacy
reveal-scope corrections. Added method-exit checks use the ordinary assertion
publication policy, while explicit split assertions retain check-and-forget.
Method and forall proof bodies retain their previous `ReturnPosition` context;
reveals remain available wherever legacy translation kept them. All original
checked formulas, fuel rules, guards, summaries and resource ceilings remain.

Structural regressions cover both publication and reveal scope under both
resolvers, including terminal nested calculation hints and ordinary nonterminal
scopes. The registered terminal-reveal fixture and reveal-isolation negative
controls are ported from the supported line. The producer registry reflects the
development source, including its branch-specific origin construction.
Current development build and normal inventory checks are pending. The preceding
[build](https://github.com/erniecohen/dafny/actions/runs/37394258403) and
[normal inventory gate](https://github.com/erniecohen/dafny/actions/runs/37395195252)
passed 33 structural checks for the earlier preservation revision; they do not
validate these new corrections. Build probes do not execute a solver.

## Supported-line evidence

The supported correction is tracked separately in [draft 168](https://github.com/erniecohen/dafny/pull/168).
Its [native build and structural gate](https://github.com/erniecohen/dafny/actions/runs/37494376061)
passed 43 structural checks. Complete registered verification with Z3 5.1.0
passed 222 expected observations on that line. The unchanged Filter, Split and
Base64 examples verify natively under both additional-axiom settings, with
previous reveal visibility preserved. Complete library comparisons retain a
separate power exit-package failure and resource regressions. Complete suite
results and default-off library variation are recorded in that draft's validation
report. These supported-line results do not establish development-line verifier
acceptance.

## Source boundaries and acceptance

Development starts at `858e4bfbcf00fbf0146255c0a4b0efdfc4deb66f`. The supported
baseline is `ab210b78b50adb5a192542897f0a00cdb40f3c35`; its current product is
`c515251c9a5f06490591defa3df3ef86e1b8d175`, build source
`1cb6a03bb14d2d96d57a1e6ac9f1c5f8f27935d2`, version
`4.11.0+fcb2042d.review.c38abced`.

Enabled completeness/performance, controlled default-off library cost,
end-to-end invariance, ordinary required CI, regression expected-row integration
and independent human soundness review remain unaccepted or pending. Expected
verdict tables have not been replaced. The pre-existing false self-postcondition
is tracked separately in issue 166. No repository-green, uniform-superiority,
merge, release or issue-closure claim follows. AI assisted the implementation
and validation.
