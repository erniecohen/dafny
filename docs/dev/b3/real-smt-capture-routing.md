# Real SMT capture diagnostic routing

Issue #124 has an explicit `real-smt-capture` focus in the fork's review workflow.
It requires `workflow_dispatch` on a `scratch/*` branch with
`b3_compile_only=true` and `b3_full_gate=false`. Ordinary workflow defaults and
job guards are unchanged. The route's source is reviewed but unexecuted at this
checkpoint; a completed diagnostic job does not establish Real acceptance.

The workflow checks the outer entry point's literal SHA256 before invoking it
with `python3 -B`. That entry point requires the approved 19-file diagnostic seal
`4792f707a0f00dfd153478a9911065ceb55428fe5f688a79c714b39aa1778d03`,
captures bounded regular source files, checks every byte length and digest, and
executes the captured `gate.py` bytes directly. No cached source-module bytecode
is loaded. The original seal is unchanged by routing. The entry point records its
own hash, workflow hash and declared CI source identity, then rechecks the whole
sealed payload after the diagnostic. These are review receipts, not a signed
origin attestation.

The route installs the exact Ubuntu noble package `strace=6.8-0ubuntu2`, as listed
by [Ubuntu](https://packages.ubuntu.com/noble/strace), and requires the first
version line to be exactly `strace -- version 6.8`. Package installation uses
sudo before any captured replay. All SDK, worker and traced solver stages run as
the ordinary non-root runner user; the entry point rejects UID0. The install log,
package/version lines and executable digest are uploaded alongside the diagnostic
receipts. Captured descendants receive no download token environment.

The approved inner wrapper downloads only the two pinned public artifacts. It
replays the four exact original requests from run 37232113837 and four separate
pure/mixed controls, all at 200k resource units and the original 20-second
whole-worker deadline. The exact library from run 37221479537 is reused after
source, proof-receipt, package and solver checks. Neither the Dafny compiler
product nor the verified B3 library is rebuilt for logging. Only the small
protocol-referencing replay host is built. The prior conversion and irrational
Unknown/ToolError results remain strict discrepancies; no expectation is waived.

`out/b3-native-compile/summary.json` is the routing receipt and
`out/b3-native-compile/real-smt-capture/` holds input metadata, raw traces, stream
observations, stage receipts and the inner diagnostic. The existing artifact
upload runs under `always()`, including prerequisite or parser failures. Expected
mathematical mismatch or incomplete capture produces diagnostic evidence and a
zero exit; it is never relabeled acceptance. Unexpected setup/entry-point
failures may fail the workflow while still uploading their available evidence.
