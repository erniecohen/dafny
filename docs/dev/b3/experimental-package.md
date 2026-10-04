# Experimental B3 package

This package contains the Dafny CLI, the B3 worker, the library whose 557 proof
batches passed in public run 37201990430, and the reviewed Z3 5.1.0 Linux x64
executable. `package-manifest.json` records their exact hashes and source/build
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
the unchanged verified library and source manifest to the public proof receipt.
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

Public [run 37209385158](https://github.com/erniecohen/dafny/actions/runs/37209385158)
passed the combined documented-subset gate and three strict clean-install
controls. Two package builds from identical captured binary inputs produced
archive SHA256 `d0684d5ebb15a93c12d75cc1ad7630bf395dbbe3dc806f3f994978dff771b62a`
(25,031,973 archive bytes). This demonstrates deterministic packaging of those
inputs; it does not establish reproducibility of compiler or library builds.
The install path included spaces and Unicode. Version and actual positive and
negative B3 verification all matched, with zero residual descendants and zero
dependency downloads. The public run artifact contains the archive and receipts;
its retention limit still applies. Broader semantics, persistent distribution
and full default-Boogie release validation remain open.
