# Lucas induction after canonical map payloads

The upstream-harness job in [the PR 313 review run](https://github.com/erniecohen/dafny/actions/runs/43452771602)
reports `Lucas-down.dfy` with eleven verified batches and one resource exhaustion
in `Lucas_Binary''`, under its existing 50-million-resource limit. This harness
uses Z3 4.12.1; the fork's verdict comparisons use Z3 5.1.0. Both profiles matter
when maintaining this fixture.

The canonical native-result map rows must remain enabled. The unguarded raw
read-over-write equation contradicts Boolean-map extensionality, as
[the soundness report, section 6.2](https://github.com/erniecohen/dafny/blob/edaf1a6f4e1c8b6fa5aac00748630ab80185a9f9/docs/dev/design/typeencoding-a-soundness.md#62-minimum-logical-repair-to-the-boolean-map-support-axiom),
and [the Boogie repair](https://github.com/erniecohen/boogie/pull/8) explain.
The conditional round-trip equality restricts a raw stored payload to its native
representation when the result type code is native. Removing it restores the
known inconsistency.

The distinguishing comparison is the same complete source invocation with
`--legacy-noncanonical-map-payloads` absent and present, retaining the solver,
resource limit, two verifier cores and refreshed-type-system options. The
emitted Boogie program is byte-identical. In the failing correctness query,
the SMT difference is exactly the canonicality antecedent on each of the two
same-index map rows; the remaining query lines agree. This rules out changes to
the arithmetic contract or generated identifiers as the distinguishing input.
Compare the old Boogie pin as well to separate this row change from the
idle-handshake change in [issue 287](https://github.com/erniecohen/dafny/issues/287).
The weakened support theory changes solver search; the CI resource failure is
not evidence that the Lucas theorem is false or that its arithmetic proof needs
the map inconsistency.

The fixture now states the particular Pascal recurrence needed in its even/even
induction case. The enclosing branch has `a > 0` and `b > 0`. When both are even,
`a / 2 > 0`, `b / 2 > 0`, `(a - 1) / 2 == a / 2 - 1`, and likewise for `b`.
The asserted equation is therefore an instance of the existing definition of
`binom`. Together with the two recursive induction hypotheses, it supplies the
recurrence at the halved arguments without leaving that instance to quantifier
search. Dafny checks this assertion. The theorem, induction calls, function
bodies, resource limits and expected output stay unchanged.

For a controlled resource comparison, run the complete file with
`--type-system-refresh --general-traits=datatype --general-newtypes
--standard-libraries:false --cores:2 --resource-limit:50e6`, naming each solver
explicitly and recording `--log-format json`. Compare the correctness result for
`Lucas_Binary''` on base `af43ea31`, the unmodified PR tree, its legacy-map mode,
and the repaired fixture. Keep well-formedness costs separate. Seeds 0, 1 and 2
can be selected with `--boogie /randomSeed:N`; also retain the ordinary invocation
without an explicit seed. A negative control calls the repaired lemma and then
asserts `false`, which must still fail.
