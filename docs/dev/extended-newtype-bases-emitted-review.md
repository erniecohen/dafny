# Extended newtype bases: retained emitted-formula audit

This is a source and retained-artifact review, with no new compiler or solver execution. The four benchmark families emit no new background theory for the extended-newtype option. Their nominal arms add instances of the existing declaration schemas and change local membership checks. The shared #132 repair changes arrow allocation independently of #113 and is described separately below.

## Exact evidence and limits

The retained public run is [37241854757](https://github.com/erniecohen/dafny/actions/runs/37241854757), built from `be0c7c4da87db52cfe76cc8422001a1e9228fb60`. The receipt reports Dafny `4.11.0+be0c7c4da87db52cfe76cc8422001a1e9228fb60`, Z3 `5.1.0`, and GitHub `ubuntu-24.04`. Every file and source hash below was checked against that run's `verify/results.json`. A Git comparison found no `Source/DafnyCore` changes from that source to current product `95f0e4c8e1b80c95cf8aff397d7acd3ffb9c62e6`. These retained emissions therefore correspond to the current Core translator; this is not a fresh build or full current-source verification receipt.

Each command used refresh/general-newtypes/unicode enabled, one core, 16,000,000 RU, a 60-second safety timeout, `normalizeDeclarationOrder:0`, seed 0, a CSV observer, and `--bprint`. `B-off` and `B-on` vary `--extended-newtype-bases`, not additional axioms. The recorded commands omit `--additional-axioms`; the pinned option declaration defaults to false ([CommonOptionBag.cs](https://github.com/erniecohen/dafny/blob/95f0e4c8e1b80c95cf8aff397d7acd3ffb9c62e6/Source/DafnyCore/Options/CommonOptionBag.cs), line 242). Nothing here establishes the true-axiom mode.

The exact public artifact-relative paths, 20 raw BPL hashes, source hashes, commands, receipt hashes, and comparisons are in [emitted-audit.json](extended-newtype-bases-emitted-review-data/emitted-audit.json). Raw files are retained by the public run under `performance-extended-<family>-5.1.0-0/verify/<family>-<arm>/sample-0/program.bpl`.

The BPL is a translated program before the complete solver-query pipeline. The comparisons do not establish equality of split VCs, axiom instantiation counts, or solver queries for arbitrary programs. Final arbitrary/generic ordinal casts, intermediate predicates, and suspended co-call identity casts need the fresh BPL observers already added to the queued gate draft.

## What the emitted comparisons establish

For every one of the four families:

- `B-off` and `B-on` BPL bytes are identical.
- `W-erased-on` and `W-materialized-on` BPL bytes are identical. This option changes compilation, not this verification translation.
- B, N, and W have identical bytes before the first `_module.` declaration. That common prefix has SHA-256 `23b9b5b748e2f71c0c4f5345456198001a16c6b20d04688fe495ba656712d8a5`.
- Their axiom statement multisets containing no `_module.` reference are equal. Triggers and guards are preserved in the comparison; only complete comment lines, proof-dependency IDs, and whitespace are normalized.

The current source also has byte-identical `DafnyPrelude.bpl` and every `Prelude/` source file relative to the shipped starting point `5c3d15513818334945b0cf9504ba5c71ba07932d`. `DafnyPrelude.bpl` has SHA-256 `1e9e2c2a15198eea4531910e44df8da0b44a77381d800d6c70d59356b80f6399` at both sources. Exact source comparison receipts are in [source-audit.json](extended-newtype-bases-emitted-review-data/source-audit.json).

Sample-0 recorded counts and RU are shown below. Counts are syntactic inventories, not solver instantiation or split-VC counts. All listed verification exits are 0 and receipt warning flags are false.

| Family | B axiom/assert tokens, RU | N axiom/assert tokens, RU | W axiom/assert tokens, RU |
|---|---:|---:|---:|
| ordinal-finite | 435 / 9, 41,598 | 440 / 10, 44,458 | 450 / 9, 45,529 |
| arrow-coercion | 466 / 17, 99,678 | 470 / 18, 103,156 | 480 / 17, 105,784 |
| codatatype-lazy | 465 / 15, 94,977 | 469 / 17, 106,116 | 480 / 15, 106,337 |
| datatype-root-update | 475 / 19, 69,697 | 475 / 21, 76,705 | 490 / 19, 82,505 |

The measurements make no general speedup claim. They show modest additional nominal proof cost on these workloads. The W erasure options have the same verification input and recorded RU here; their separate runtime differences are outside this emitted audit.

## Existing declaration instances, rather than a new carrier theory

`AddRedirectingTypeDeclAxioms` is byte-identical between the original shipped source and current product (method SHA-256 `f46b990b3319c42d361e81f5884f1f4b8b54578a66f6a6f217624cd65fe3b905`; [Types.cs](https://github.com/erniecohen/dafny/blob/95f0e4c8e1b80c95cf8aff397d7acd3ffb9c62e6/Source/DafnyCore/Verifier/BoogieGenerator.Types.cs), line 1198). The existing generator emits a nominal type descriptor, membership through the declared base and predicate, allocation through the base, and ordinary function consequences. #113 makes this existing generator reachable for more declared bases. It does not replace ordinal, arrow, or datatype semantics with new base axioms.

For unconstrained nominal `Value`, the predicate is `true`. Representative emitted instances are:

```boogie
$Is(c0, Tclass._module.Value()) <==> $Is(c0, TORDINAL) && Lit(true)
$IsAlloc(c1, Tclass._module.Value(), h) <==> $IsAlloc(c1, TORDINAL, h)
```

In the arrow arm the same schemas use the exact base `___hTotalFunc1(Token, TSeq(Tag))`, and in the codatatype arm they use `Stream`. Their allocation equivalences merely forward to that base's allocation judgment; they do not restore the removed reads-to-allocation converse. Normalized exact formulas with every trigger and guard retained are in [ordinal instances](extended-newtype-bases-emitted-review-data/formulas/ordinal-finite/N-on-value-instance.txt), [arrow instances](extended-newtype-bases-emitted-review-data/formulas/arrow-coercion/N-on-value-instance.txt), and [codatatype instances](extended-newtype-bases-emitted-review-data/formulas/codatatype-lazy/N-on-value-instance.txt).

Nominal minus B axiom deltas are +5 ordinal, +4 arrow, +4 codatatype, and 0 constrained-datatype. These net counts include replacing raw function requirements/consequences with nominal requirements/consequences; they are not counts of newly invented schemas. The constrained datatype B is a checked subset over `Envelope`, and N has exactly the same predicate. In separate programs both use the name `Value` and emit identical axiom multisets. This cross-program comparison does not identify distinct nominal declarations within one program.

The W arms add the ordinary single-field datatype's descriptor, constructor/destructor, equality/rank and membership/allocation instances. Their exact normalized differences are preserved in `formulas/<family>/W-vs-B-axiom-multiset.json`; raw unfiltered diffs remain beside them.

## Local obligations and exact carrier handling

The nominal ordinal and arrow arms each add only one syntactic `assert true` relative to B, from the unconstrained newtype declaration's well-formedness/witness check. These are not added logical premises. The ordinary codatatype arm additionally asserts `$Is(v, Stream)` in `Decode` before its target-typed result is assumed. This is the #141 carrier check outside any suspended co-call. The constrained datatype arm replaces two nominal subset checks with two explicit `Envelope` membership checks and two existing destination-predicate checks. The update and explicit cast still establish both `tag >= 0` and `leaf.value >= 0`. No predicate is deleted to reduce cost. Exact local statement deltas are in `formulas/<family>/N-vs-B-local-statements.json`; full raw BPL diffs show ordering and context.

Source order remains operand well-formedness first, then conversion checks, then target-typed continuation ([ExpressionWellformed.cs](https://github.com/erniecohen/dafny/blob/95f0e4c8e1b80c95cf8aff397d7acd3ffb9c62e6/Source/DafnyCore/Verifier/BoogieGenerator.ExpressionWellformed.cs), lines 1008–1078). The feature's resolver-owned arrow projection only skips a duplicate carrier introduction for a resolved visible nominal path and exactly equal arrow base, including family and actual signature. User casts still call `CheckResultToBeInType`. The recursive target-chain check instantiates every declared base and visits it before its own predicate ([Types.cs](https://github.com/erniecohen/dafny/blob/95f0e4c8e1b80c95cf8aff397d7acd3ffb9c62e6/Source/DafnyCore/Verifier/BoogieGenerator.Types.cs), lines 1626–1663).

The established-arrow optimization returns only at an already established exact subset declaration/signature (lines 1634–1641). Enclosing destination newtype predicates and differing intermediate constraints are still visited. Predicate retagging preserves the same Boogie expression, uses the instantiated binder's own type, and requires resolved visible equal source/value/formal arrow signatures (lines 1668–1692). It adds neither a heap fact nor a membership axiom. A genuine arrow signature or family conversion continues through the normal checks and representation conversion.

The arrow benchmark intentionally has a genuine matched coercion in every arm: `Tag -> seq<Token>` to `Token -> seq<Tag>`. This nominal comparison therefore prices the same coercion on both sides. Nominal encode/decode retain the existing handle after that coercion; they do not introduce an additional nominal-only adapter.

## ORDINAL full carrier and target-chain source argument

The emitted nominal ordinal `Encode#definition` (raw N BPL lines 2988–2991) and `Decode#definition` (lines 3022–3025) equate the result to the input Box. Neither definition uses `ORD#Offset`, `ORD#FromNat`, or an `IsNat` guard. `RoundTrip` accepts an arbitrary ORDINAL and proves `Decode(Encode(b)) == b` (lines 3043–3074). Only the compiled workload's finite loop invariants and arithmetic use `IsNat`/`Offset`. Thus the verification cast shown in this retained program preserves the entire ordinal, while the runtime workload intentionally exercises a finite slice.

Current `ConvertExpression` preserves an ORDINAL when the destination carrier is ORDINAL ([Types.cs](https://github.com/erniecohen/dafny/blob/95f0e4c8e1b80c95cf8aff397d7acd3ffb9c62e6/Source/DafnyCore/Verifier/BoogieGenerator.Types.cs), line 1381 onward). `CheckResultToBeInType` asserts `ORD#IsNat` only when leaving the ordinal carrier (line 1498), retains ordinal non-negativity checks for numeric introductions, and passes the entire ordinal to same-carrier target predicates (lines 1608–1611). It then visits each instantiated target redirecting base before the outer predicate. The registered arbitrary source contract [ordinal.dfy](https://github.com/erniecohen/dafny/blob/95f0e4c8e1b80c95cf8aff397d7acd3ffb9c62e6/Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/git-issue-113-extended-newtypes-ordinal.dfy) explicitly preserves `IsNat`, `IsLimit`, `IsSucc`, and `Offset` through `OrdLayer`; its generic counterpart retains the `Nonzero<seq<B>>` inner constraint and anti-vacuity final false assertion. These source contracts were inspected, but no final arbitrary/generic BPL is yet available in this audit.

Constraint expressions use the existing generator's `CanCallAssumption` and translated predicate after base checks (lines 1689–1691), rather than new global callability facts. Declaration well-formedness still checks the constraint separately. This ordering is a source argument for current code; exact generic target-chain/callability emissions remain a required final-gate observer review.

## Shared #132 changes relative to the old fork

The original arrow allocation axiom equated allocatedness with allocated reads. The current generator changes that biconditional to an implication ([arrow baseline diff](extended-newtype-bases-emitted-review-data/source/arrow-allocation-baseline-to-final.diff)). The actual current arrow N BPL lines 2307–2318 emit:

```boogie
$IsGoodHeap(h) ==>
  $IsAlloc(f, Tclass._System.___hFunc1(t0,t1),h) ==>
    (forall bx :: $IsBox(bx,t0) && $IsAllocBox(bx,t0,h)
      && Requires1(t0,t1,h,f,bx) ==>
      (forall r :: r != null && Reads1(t0,t1,h,f,bx)[$Box(r)] ==> h[r,alloc]))
```

The raw exact formula, including triggers and prelude encodings, and the separate allocated-output consequence are in [system-arrow-allocation-consequences.txt](extended-newtype-bases-emitted-review-data/formulas/arrow-coercion/system-arrow-allocation-consequences.txt). Both are shared by B, N, and W. An empty reads set no longer introduces function allocatedness.

Allocation introduction now comes from explicit captures. `LambdaAllocation` ([ExpressionTranslator.cs](https://github.com/erniecohen/dafny/blob/95f0e4c8e1b80c95cf8aff397d7acd3ffb9c62e6/Source/DafnyCore/Verifier/BoogieGenerator.ExpressionTranslator.cs), lines 1269–1310) requires a good heap, allocation of free variables classified by `MayShowReferences`, allocation of a captured receiver, and equality/successorship of captured previous/label heaps. Its local let names the existing handle and introduces only that capture-guarded implication. `FunctionHandle` allocation ([Functions.cs](https://github.com/erniecohen/dafny/blob/95f0e4c8e1b80c95cf8aff397d7acd3ffb9c62e6/Source/DafnyCore/Verifier/BoogieGenerator.Functions.cs), lines 772–789) has the corresponding receiver and previous-heap antecedents. These #132 introduction changes apply to ordinary language features before #113; they must not be described as new #113 background axioms. The benchmark's actual captured token checks occur in every arm.

The retained four-family corpus is not an independent completeness or soundness proof of the capture repair. Its quantified, old-heap, opaque-type, and non-vacuity controls have separate public receipts and must remain in the final gate. This review specifically verifies that the nominal allocation forwarding instances shown here do not reintroduce the old converse.

## Co-call scope and remaining emitted review

Current source suspends only a redundant full-carrier check for an exact instantiated codatatype identity conversion chain ending at a resolver-approved `CoCall.Yes`. Its destination path must contain no nontrivial refinement ([NewtypeOperationView.cs](https://github.com/erniecohen/dafny/blob/95f0e4c8e1b80c95cf8aff397d7acd3ffb9c62e6/Source/DafnyCore/AST/Types/NewtypeOperationView.cs), lines 180–189; [ExpressionWellformed.cs](https://github.com/erniecohen/dafny/blob/95f0e4c8e1b80c95cf8aff397d7acd3ffb9c62e6/Source/DafnyCore/Verifier/BoogieGenerator.ExpressionWellformed.cs), lines 1520–1534). Operand checks, destination predicates, productive-call restrictions, and #143 observation/binding/helper/default membership checks remain. No co-call result type or canCall fact is assumed as an exemption.

The codatatype performance fixture encodes/decodes ordinary already-typed Stream arguments; it does not exercise this suspended identity path or every generic/custom predicate. That distinction is preserved. The final queued gate must retain raw BPL for all verification-producing RUNs, then independently inspect full ordinal carriers, instantiated target-chain predicates, callability ordering, exact arrow-family projections, and suspended co-call boundaries on the final compiler receipt. Expected resolver rejection has no emitted-VC claim. No all-query-equivalence or trusted final-gate completion is asserted by this source-only package.
