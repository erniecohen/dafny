# Real SMT capture diagnostic routing

Issue #124 has an explicit `real-smt-capture` focus in the fork's review workflow.
It requires `workflow_dispatch` on a `scratch/*` branch with
`b3_compile_only=true` and `b3_full_gate=false`. Ordinary workflow defaults and
job guards are unchanged. The route's source is reviewed but unexecuted at this
checkpoint; a completed diagnostic job does not establish Real acceptance.

The workflow checks the outer entry point's literal SHA256 before invoking it
with `python3 -B`. That entry point requires the approved 19-file diagnostic seal
`3589db0edcf63e94808a2b254fae0e13b3d6d8f7ffb7948e5604b7ec3f336da4`,
captures bounded regular source files, checks every byte length and digest, and
executes the captured `gate.py` bytes directly. No cached source-module bytecode
is loaded. The archive-layout repair refreshes the seal; the original f6 receipt remains preserved. The entry point records its
own hash, workflow hash and declared CI source identity, then rechecks the whole
sealed payload after the diagnostic. These are review receipts, not a signed
origin attestation.

The route installs the exact Ubuntu noble package `strace=6.8-0ubuntu2`, as listed
by [Ubuntu](https://packages.ubuntu.com/noble/strace), and requires the first
version line to be exactly `strace -- version 6.8`. Package installation uses
sudo before any captured replay. All SDK, worker and traced solver stages run as
the ordinary non-root runner user; the entry point rejects UID0. The install log,
package/version lines and executable digest are uploaded alongside the diagnostic
receipts. The setup stage initially sets `REAL_SMT_SETUP_OK=false`, uses fixed
180-second apt and 20-second metadata deadlines with five-second kill backstops,
and streams at most 4MiB to each command log. Any command, package/version, log
bound or digest failure records its status and exits zero as unavailable diagnostic
evidence. The flag becomes true only after all checks succeed. The outer wrapper
requires that flag and the exact setup receipts before loading any diagnostic,
SDK, parser or trace stages; a preexisting tracer cannot override failed setup.
It checks the captured `/usr/bin/strace` digest and rejects PATH shadowing.
Captured descendants receive no download token environment. These installer
timeouts are fixed CI preparation, separate from the pidfd-owned non-root replay
scope; they do not establish proof-process lifecycle acceptance.

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
zero exit; it is never relabeled acceptance. Prerequisite failure also produces a
zero diagnostic exit and an outer failure receipt without starting proof stages.
Unexpected entry-point or source-binding failures may fail the workflow while
still uploading their available evidence.


The first routed run 37240724688 stopped before parser/build/worker stages because
the original archive included its legitimate `Binaries/net8.0` root. The source-only
repair audits and pins each archive's permitted root counts and preflights all
members before creating extraction output. Extra payload roots are inventoried and
hashed but never loaded. The fixed control denominator is now 27 (21 unchanged
parser controls plus six archive admission controls); the repaired route is unexecuted.
