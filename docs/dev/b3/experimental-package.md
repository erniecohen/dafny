# Experimental B3 package

The initial bool/int subset archive passed clean-install controls in public
[run 37209385158](https://github.com/erniecohen/dafny/actions/runs/37209385158)
on source `f927757337371038a61406a3b107543ca2b72f77`. It used the unchanged
library whose 557 proof batches passed in public run 37201990430, plus the
reviewed Z3 5.1.0 Linux x64 executable. That receipt remains evidence for the
initial archive only. Native Real and the positive-width bitvector language change the library proof source and protocol;
new packaging and checking fail closed until a fresh exact library hash, verified
batch count and public proof receipt are recorded. `package-manifest.json` records their exact hashes and source/build
identities. CLI metadata/source identities are build declarations; immutable file
hashes and the public build receipt provide review evidence, not signed source
attestation. The generated B3 library remains a trusted compiler artifact; its
proof receipt is not an end-to-end soundness theorem.

Use Linux x64 with glibc 2.39 or newer and a .NET 8 runtime already installed.
The bundle requires no dependency download to run. The CLI launcher preserves
Boogie as the default backend. From this directory, select B3 explicitly:

```sh
./dafny verify example.dfy --verification-backend b3 \
  --b3-worker "$PWD/cli/b3/DafnyB3Host.dll" \
  --solver-path "$PWD/solver/z3" --verification-time-limit 30
```

The supported experimental bool/int, contract and structured-loop subset is
recorded in the bundled `support-matrix.json` and `discrepancies.json`. Unsupported
features fail explicitly. Native real/bitvector theories, general visibility,
models and verification caching remain outside this package's advertised scope.
Do not interpret a successful archive build as complete backend acceptance.

Rebuild from the source checkout with `Scripts/package-b3-experimental.py`,
passing the built CLI directory, published worker directory, reviewed solver
and its license, exact CLI source commit, and output archive. The script binds
the exact verified library and source manifest to the inspected public proof
receipt. Source inventory hashes alone cannot authorize a new library package.
The checker requires the archive SHA256 from the independent builder/public
build receipt. A self-declared package manifest is not its origin check.
Archive metadata and gzip timestamps are deterministic; input binary hashes
remain exact. `Scripts/check-b3-package.py` audits all extracted files and runs
strict valid/invalid controls in a directory with spaces and non-ASCII characters.

The clean-install gate is Linux-specific. It uses a dedicated child-subreaper
scope, bounded CLI/output deadlines and pidfd signals for adopted descendants.
A completed command with a residual child fails; timeout or cleanup failure also
fails. Successful proof controls require actual complete B3 progress and a
positive verified-symbol count. These controls do not establish general language
coverage or native proof-cost parity.
