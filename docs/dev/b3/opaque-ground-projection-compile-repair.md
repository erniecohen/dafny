# Opaque ground projection local-name repair

This source-only repair starts from
`8b63d6ecc6b816f7e51af844422cadf3b22b148b`. Its product commit is
`7e0f321d22bb6ac5c119788e8bb0fbdcf2f8469a`.

The original `Prepare` method declared an input-audit loop variable named
`evidence`, then an enclosing submitted-evidence collection with the same name.
C# rejects those declarations with CS0136. The repair renames only the enclosing
collection to `submittedEvidence` and its two uses: the independent relation's
argument and the returned aggregate. The input audit, optional fallback, request
contents, relation, goals, allocation bounds and producer versions are unchanged.

The existing Phase B argument keeps its historical `sourceParent` and
`englishFirstCommit`. Its source manifest now names the repaired product and
refreshes the two affected file pins. It retains exactly 12 primary file records,
36 correspondence dependency records, 15 structural control methods and 47 fixed
control rows. An external complete inventory includes the manifest itself as a
49th file; the manifest does not contain its own digest. This separate repair
argument is outside that historical 49-file scope.

No test source, expected result, fixture, protocol, worker/library source or
workflow changes in this repair. The existing registered issue124 regression and
fixed structural controls remain unchanged. The replacement compiler/control
gate requires separate review and execution. This checkpoint has no successful
compilation, test, worker, corpus or proof receipt and makes no Real support or
default-verifier compatibility claim.
