# Canonical safety cohort for issue #165

This public diagnostic package preserves 97 existing root files, 341 literal RUN lines, and 201 source, oracle and FileCheck closure files. The canonical inputs come from frozen source checkout `c1a360c4f77534ae2485049c880eb85e05d88989`. Their bodies, original oracles, error locations, literal options and resource limits remain unchanged. `Source/` mirrors their repository paths below this directory, and `cohort.json` binds each source, body, command and input closure.

The cohort selection originated in the earlier 44a diagnostic. Its unchanged input identity is separate from the current compiler candidate. The active `manifest.json` records current `Source/` paths and hashes, plus current cohort and README hashes. `historical-44a-package-manifest.json` preserves the original manifest bytes, including its earlier candidate identity and `tracked/` layout; it is a historical receipt, not the current payload or compiler manifest.

The current candidate is the conditional exact-argument proposal for [issue #165](https://github.com/erniecohen/dafny/issues/165). Its completed compiler build is [run 37347177735](https://github.com/erniecohen/dafny/actions/runs/37347177735), artifact `11361575690` (`default-off-compilers-shipped`). Its source and archive identities are:

| Identity | Value |
| --- | --- |
| Product base | `4e50e86cde6ab2ef6cab0762ffc4df59d646dc15` |
| Patch SHA256 | `e050312cf42cef71a9f0e404c4b98df1f75d96b26306da67276846fa0bb919e9` |
| Composed tree | `5b924f4012464b2f8aac845c1a193236c22ccf78` |
| Source tree | `f8715951b1d481236cab3f6bab3e1aed04ca3a49` |
| DafnyCore tree | `95fb3e9316e02259e4a004734d621a5c03951212` |
| Compiler version | `4.11.0+fcb2042d.review.c9c7622d.candidate.e050312c` |
| Compiler archive SHA256 | `5f60ae085aeae4efd86ddcfaced0107f2008860e25320c7be182889e83185541` |

The candidate gates its second native-function-law traversal on the existing `AdditionalAxioms` option. The local implication uses the same translated Boogie actual arguments for `#requires` and `#canCall`, inserting only the required fuel slot. It retains the original public constructor signature and ordinary permission, allocation and productive-call boundaries. Source review and a completed build do not establish restored proofs, safety-cohort success or approval to adopt the source.

The roots comprise the original #132 wrapper, 16 existing-language newtype allocation/reference controls, all 44 registered #143 roots, 20 raw/nominal productive-suspension controls, and 16 older ordinary function/lambda tests. Run them through the original fork harness, including each referenced input and every literal diff/FileCheck step. A comment-only wrapper must execute its inputs; verifying its empty body does not exercise those proofs. The collector checks exact old-harness and candidate-overlay bytes, discovers tests without a selection filter, then requires the exact 97-root TRX names, methods and passing counters. Literal macro expansions, default solvers and individual option overrides remain authoritative. A suite label does not establish an effective AX setting.

Verdict roles follow the source contracts. Documented valid-program conservative rejections, including the #143 abstemious helper controls, keep their original rejection oracles. Soundness negatives retain their intended rejection boundaries. Inhabited final-false controls require their valid assertions to succeed before the false assertion is rejected. Canonical acceptance cannot silently absorb an extra error or a resource failure in a negative program.

The public workflow executes only `prepare`, `discover`, `run` and `report`. Derived AX verification projections and the four supplemental productive-helper/capture-lambda drafts are not executed by this workflow. Their exact counts, diagnostic boundaries and generated BPL coverage remain separate observations. Source selection alone does not establish that a native-law leaf, a self/override allowance or a hidden-RHS case was reached. Any reported proof restoration and resource movement must come from actual, separately bound receipts. This cohort and its build receipt supply neither a complete trusted gate nor source-adoption approval.
