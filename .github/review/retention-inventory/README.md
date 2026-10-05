# Read-only retention diagnostic inventory prerequisite

This source-only prerequisite prepares a byte inventory for independent review.
Its only route is `b3_focus_gate=retention-inventory`, with
`b3_compile_only=true`, `b3_full_gate=false`, and every other dispatch flag false.
Every existing workflow job is excluded for this focus, including invalid flag
combinations. Invalid flags produce a routing receipt and skip the inventory.
The valid route uses the runner's fixed `/usr/bin/python3` with `-I -S -B` under
`env -i`. It has no SDK setup, managed build, product or tool invocation, shell
subprocess, namespace creation, privileged operation or solver call. The inspected
archives are extracted with mode 0644. Python inspects bytes without loading any
downloaded assembly, executable or native library.

The intended next invocation, after independent source review and separate
authorization, is one fixed diagnostic-only workflow dispatch. The source has
not been executed. Workflow exit zero delivers diagnostics; `inventoryCaptured`
is an inventory receipt, not a successful proof or a qualified diagnostic host.

The official runtime archive is .NET **8.0.31**, SHA256
`e2e392eedd49fd5a6eba078d508383fa9191059cb362c9d93cf533d5207b8db6`,
with all **188** regular file pins and all **7** directory names. The analyzer
archive is dotnet-dump **9.0.652701**, SHA256
`a89bb212881600dde4b8e0c3b39c75957d84f6ec3e1cb7a874d4048e774494c9`,
with all **96** regular file pins. `official-archives.json` contains complete
inventories and exact official URLs. The NuGet runtime pack is not substituted
for the runtime distribution. Its managed CoreLib bytes differ. This prerequisite
downloads only these two fixed archives; it does not download a compiler product
or execute the collector or analyzer.

`inventory.py` captures the existing original44 source manifest and every declared
file before and after inspection. Those bytes, including the previously qualified
original19 lifecycle files, remain unchanged. It also captures its exact three
sources, `global.json`, the unchanged product base, and the routing workflow.
The new source manifest declares three files; the separate routing manifest binds
that source manifest, the base and workflow. The workflow verifies the exact
source-manifest and inventory-body hashes before executing the captured Python
source bytes. It does not import a cached body from the checkout.

The candidate catalog contains complete extracted tool assets, the runner's fixed
Python3.12 standard-library tree, installed hostfxr/framework/reference-pack trees,
the highest installed stable SDK8.0 directory under the repository's fixed
8.0.111/latestFeature request, actual imported Python files and mapped images,
loader configuration, and explicitly named ICU/OpenSSL/NSS library families.
The SDK choice is a static candidate; no SDK resolver is executed. Requested
paths, resolved real paths, alias targets, file sizes and SHA256 digests are
detached into the receipt. Every captured file, alias, tree membership, SDK
selection set and native lookup pattern is rechecked before publication. This
avoids silently adding a file that was absent during selection.

ELF inspection is implemented in bounded Python. It reads ELF64 little-endian
x64 program headers, interpreter paths, DT_NEEDED, DT_RPATH, DT_RUNPATH and
DT_SONAME. It records origin-expanded candidate search directories and inherited
RPATH contexts, then reads the glibc1.1 loader cache. A dependency is selected
only when all eligible cache/default aliases resolve to one real image. It
records missing files, multiple images, unsupported cache hardware capabilities,
hardware-capability directory choices, relative search paths and unreviewed
loader tokens. Foreign architecture and musl assets remain in the complete file
inventory and are classified as unselected platform assets. No ELF is executed;
`ldd`, `readelf`, SDK discovery commands and native loaders are not used.

This graph is a **candidate**, not proof that all native loading has been closed.
DT_NEEDED does not account for every `dlopen`, ICU/OpenSSL version probe, runtime
hostfxr selection, SOS/DAC load, Python native import or SDK native task. The
receipt keeps `completeDynamicLoadClosure=false`, `reviewedRunnerClosure=false`,
`fullDiagnosticEnabled=false`, and a fixed list of required source contracts.
Unresolved dependencies and ambiguous selections remain explicit even when
the archive and file inventory was captured successfully. No generic caller
pin file or arbitrary additional-library allowance is accepted.

Independent review of the actual catalog must determine the complete fixed
loader/tool/runtime byte closure, including source-backed dynamic lookup rules.
A future full diagnostic child must freeze that exact reviewed typed catalog in
its own source and reject a freshly selected runner whose bytes, choices or
unknown mapped libraries differ **before SDK or target load**. That future child
also needs separate namespace/process ownership and failure-drain review. None
of those routes or permissions is enabled by this prerequisite.

The motivating public six-smoke run
[37243994794](https://github.com/erniecohen/dafny/actions/runs/37243994794)
remains failed: baseline/true produced an actual valid native VC and 1955 resource
units, but the private context did not collect; only one of six cases ran and
seven of twenty-eight audits were recorded. Its exact owned solver was terminated
after CLI completion. The actual managed retention root is unobserved. This
prerequisite changes no assembly loader, static field, event, cache, source,
solver flag, query, hash seed, resource count, WeakReference or acceptance rule.
It establishes no native parity or B3 completeness result.

Bounds are fixed: 600 seconds for read-only inventory, 65,536 unique files,
512 MiB per file, 8 GiB total unique file bytes, 32 MiB per JSON receipt,
8,192 ELF graph contexts, and 30 seconds per fixed HTTPS read. Missing prerequisites
or exceeded bounds produce a failed diagnostic receipt, preserved in the artifact.
All official archive bytes are hard pinned; no credentials enter the inventory
process environment. The prerequisite necessarily executes the trusted runner's
Python host and standard/OS libraries; its actual imported and mapped files are
recorded. The prohibition concerns launching SDK, downloaded tools, managed
products or proof/solver workloads.

## Source-only ELF classification and failure-evidence repair

The first [read-only inventory run](https://github.com/erniecohen/dafny/actions/runs/37252189863)
used source `4d308a73f930285b8dc35e7b916ead0f8d7786cf`. It remains failed:
the summary reported `Bounded ELF program headers required`, with no candidate
catalog or offending path. Both exact archives and all 284 embedded member hashes
matched. Every one of the nineteen uploaded x64 shared objects met that bound.
The actual offending runner file is therefore unobserved. Relocatable or debug
assets are possible explanations, not an observed cause.

The reviewed repair preserves every byte-catalog entry. ELF classification records
class, byte order, type, machine, header version/size, program-header fields and
section-header fields from bytes verified against the captured file digest.
ET_REL assets remain explicitly catalogued outside the runtime load graph.
ET_EXEC and ET_DYN remain loadable candidates with the existing strict header,
interpreter, dynamic-table and string bounds. A `.dbg` filename or missing table
does not waive any of those bounds. Unknown, reserved, ET_NONE and ET_CORE types
remain explicit blockers. This follows the distinctions in the
[ELF ABI header](https://gabi.xinuos.com/elf/02-eheader.html); it does not qualify a
compiler/tool invocation's use of an object file as data.

Before graph traversal, rechecked source/file/path/tree facts are detached into
a pinned pre-graph snapshot. It states that the graph is incomplete and the
inventory/closure/diagnostic is unqualified. If graph inspection fails, a bounded
fault record binds the exact catalog path, size and digest, captured ELF header,
graph phase and original failing bound. A partial snapshot never becomes a
completed catalog. The primary failure and later finalization faults are separate.

Finalization has its own fixed 30-second budget, followed by a separate 5-second
terminal receipt-publication budget. It does not reuse an expired 600-second
inspection deadline or retry graph work. If rechecking sources or files runs out
of budget, that is additional failed evidence; it cannot replace the original
parser fault. Summary serialization/publication is independently bounded and
records the detached primary fault even when a final recheck fails. An outer
publication failure still fails delivery evidence and does not qualify the run.

The fixed artifact upload includes hidden evidence files so runtime `.version`,
analyzer `.signature.p7s`, and `_rels/.rels` are preserved outside their archives
too. Upload paths remain only the new evidence and routing directories. No home,
credential, checkout or cache tree is uploaded. Official archive pins, original44,
product base, ordinary/default proof routes and the full namespace diagnostic
remain unchanged. The source and new manifests need independent review before
any fresh read-only invocation; no inspected image is executed by this repair.
