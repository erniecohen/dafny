# Issue 124: native bitvector implementation and correspondence plan

This is a source-reviewed English design, not an implementation or verification receipt. It continues the supplied plan's P7 and section 5.7. The complete scope remains native positive-width bitvectors, the existing width-zero encoding, unsigned operations, shifts, rotations, extraction, concatenation, and conversions. The small first fragment below is a checkpoint toward that scope.

## Frozen source basis

* Dafny producer: `3218225e747d115dd81cabad6212c3187c3a5038`. The inspected bitvector producer files have the same blobs in the approved Real checkpoint.
* B3 bridge and vendored language: repaired Real checkpoint `c81a5f7be8ba5d0829c942fff01e36f72a6ac824`, based on selected B3 PR 13 commit `ea6e8a18dfe9e317d313de769291f989957dc5f2`, with the local checked-library additions.
* Pinned Boogie implementation: `73a0e214a87df85fc058270268c1d0706fd05bc9`.
* Source inventory at the Real checkpoint: `4b53100120cc9a1e8584f3c335985ed04f7b826778b114cc5e49f515f2d8d562`. Any bitvector language change invalidates that inventory and requires a fresh library proof/build receipt. No earlier receipt establishes this extension.

This source work has no completed fresh library proof receipt. The native rotation diagnostic remains inconclusive and establishes no bug or semantic equivalence.

## Actual emitted language and recognition boundary

`Source/DafnyCore/Verifier/BoogieGenerator.cs:761-787` emits, for every encountered width, the following families. The names are useful for a census, not semantic recognition keys.

| Source construct | Actual positive-width representation | Target interpretation |
| --- | --- | --- |
| and/or/xor/not | `bvbuiltin` bvand/bvor/bvxor/bvnot | Native same-width word operation |
| add/subtract/multiply | bvadd/bvsub/bvmul | Native modular arithmetic |
| unsigned divide/remainder | bvudiv/bvurem | Native unsigned operation, retain source zero checks |
| unsigned comparison | bvult/bvule/bvuge/bvugt | Native unsigned order; reverse arguments for greater comparisons if desired |
| left/right shift | bvshl/bvlshr, **two arguments of the same BV width** | Native shift, with its actual already-converted count |
| left/right rotate | ext_rotate_left/ext_rotate_right, same-width arguments | Reviewed portable lowering or explicitly tested pinned-solver restriction |
| Int to BV | actual `bvbuiltin "(_ int2bv W)"`, Int -> BV(W) | Native modular conversion |
| native BV to Int | actual `bvbuiltin "bv2int"`, BV(W) -> Int | Pinned Z3's **unsigned** conversion |
| ordinary BV to Int wrapper | ordinary function plus a separate universal range/equality axiom | Native conversion only with the precise live-axiom route below |
| extraction/concatenation | `BvExtractExpr` / `BvConcatExpr` | Native indexed extract / high-left concat |

Recognition must bind the actual resolved `FunctionCall.Func` object and its actual typed signature, not a name, suffix, detached `DefinitionBody`, or a guessed producer. Require resolved monomorphic formals/results, exact arity, positive bounded widths, and the operation's complete signature. Require ownership in the current source program for a declaration-based primitive route. Examine the effective attribute according to pinned Boogie: `bvbuiltin` takes priority over `builtin`; an allowlisted indexed spelling is parsed into integer parameters and checked, never copied as arbitrary SMT text. Ambiguous duplicate semantic attributes may be rejected conservatively. A malformed or unreviewed claimed builtin must fail closed, not silently become native or an ordinary UF.

Actual `Function.Body` takes precedence: `Boogie2VCExpr.ApplyExpansion` expands it by formal substitution before the builtin is printed. A body cannot be ignored because a misleading attribute says a different operation. Native-attribute recognition initially requires no Body. A separate narrow Body route can recognize exact native operations composed over the actual uniquely ordered formal identities, and exact literal constant bodies; it must obey source substitution and bounds. Existing identity and Real conversion routes remain subject to their reviewed availability rules.

`BoogieGenerator.Types.cs:1280-1436` already decomposes conversions into widening zero-concat, narrowing extraction, Int/BV wrapper calls, and Real/Int conversion composition. Preserve this actual tree. Do not reinterpret the expression from the original Dafny type or invent a cast from a function name.

## Carrier, types, literals, and shared language changes

For each W > 0, the semantic carrier is all W-bit words, with unsigned value U in [0, 2^W). Width is part of the sort. Different widths cannot be silently unified, widened, truncated, or promoted. Native SMT sorts and quantification provide the whole carrier, including high-bit-set values; opaque user sorts, bounded host integers, or signed machine integers do not provide equivalent support.

Add `Ast.BitvectorType(width)` and `SolverExpr.SBitvector(width)`, with a positive-width subtype in checked layers. Raw input carries an unchecked integer width; resolver/checker code rejects zero, negative, excessive, or noncanonical widths before conversion to a subtype or expensive arithmetic. Use an explicit per-width and cumulative bit-cost budget together with existing node/message/depth limits. The implementation limits are width <= 4096 and aggregate <= 4,194,304 bits. The aggregate charges every native BV expression result and each BV binding/declaration occurrence, conservatively counting repeated references; indexed operations also validate their operand/result widths. At most 512 bytes are needed for a canonical word magnitude, and the total charged magnitudes occupy at most 512 KiB before runtime object overhead. Existing 100,000-node, depth-128 and 32-MiB wire limits remain independent ceilings; a decimal word numeral has at most 1,234 digits. These are resource limits with explicit Unsupported/invalid-input outcomes, not a silently restricted hardware-width semantics. Never use a 32/64-bit host shift to interpret a word or count.

A small compatible spelling for the string-based Raw type API is `#bvW`, for example `#bv7`: this avoids reserving a previously legal textual identifier such as `bv7` as a new builtin type. Reserve this namespace explicitly for direct Raw API callers as well: an old user type constructed as `#bv7` must be rejected as a reserved declaration, never silently reinterpreted. Add a canonical decimal width parser and formatter in `RawAst/Types.dfy`, and `IsBuiltInType(TypeName)` for the width family. The existing finite `BuiltInTypes` membership checks in `RawAst.Program`, `Resolver.GoodTypeMap`, type-declaration rejection, and `ExprResolverState.Valid` must be updated coherently. Reject aliases or user/domain type declarations that claim the new primitive namespace; do not register a native BV sort as a `UserType` declaration. Add parser syntax for these types rather than relying on identifier parsing.

Add separate Raw/Ast word-literal constructors carrying value and width, plus `RSolvers.RExpr` and `SolverExpr.SExpr` native literal lowering. The checked target literal requires W > 0 and 0 <= value < 2^W. The Raw resolver must establish this dynamically; malformed Raw construction must fail without relying on a compiled-away Dafny precondition.

There is a source literal detail to preserve: Boogie's `BvConst` constructor requires only a nonnegative value, and `SMTLibLineariser.VisitBvOp` prints the lowest W bits. Thus the normalizer canonicalizes a valid source literal modulo 2^W, including a source value equal to 2^W. It first checks width and numeral bounds, then performs bounded arithmetic. In contrast, a noncanonical external Raw literal should be rejected rather than silently wrapped. Negative Int operands of the native Int-to-BV conversion remain legal and reduce modulo 2^W; they are not negative BV literals.

The SMT printer emits `(_ BitVec W)` and `(_ bvV W)` from checked metadata. Bitvectors use equality/inequality and ITE with the existing polymorphic same-sort rules. All other BV operations get distinct typed constructors, not the ordinary Int/Real arithmetic cases.

Suggested operator algebra: BvAnd/Or/Xor/Not, BvAdd/Subtract/Multiply, BvUnsignedDivide/Remainder/Less/LessEqual, BvShiftLeft/LogicalShiftRight, BvExtract(start,end), BvConcat, IntToBv(width), BvToUnsignedInt, and BvRotateLeft/Right. Greater comparisons can lower to reversed less comparisons. Every indexed parameter is validated as data, including checked concat-width addition. Do not use `ROperator.BuiltInOperator(string)` to inject untrusted indexed text; add a typed indexed operator representation or construct checked S-expression heads directly.

Update the complete shared-source surface:

* Raw constructors, `WellFormed` predicates, parser, Raw printer, and library input admission.
* Resolved Ast constructors, `ExprType`, `WellFormed`, free-variable visitors, string/resolved printers, ExprResolver and its semantic state predicates.
* `TypeChecker.TypeCorrectExpr` **and** executable `CheckExpr`: exact width/arity/bounds and result types. Their successful-check postconditions must still follow from runtime checks.
* Type/domain substitution, custom-literal rejection after substitution, function desugaring visitors, and every exhaustive expression/type match.
* `Verifier/Incarnations.dfy` type mapping, bound-variable creation, expression substitution; `RSolver` and `SolverExpr` literal/operator/sort printing and declaration handling.
* All exports and parser/printer exposed syntax. The C# worker and Java B3 target share these sources; both must compile. This is not a worker-only bypass.

Neither `RawAst/Values.dfy`'s current `Value = int` / always-true `HasType` placeholders nor an old commented semantics skeleton constitutes a BV semantic model. Leave no claim that these become an end-to-end proof just because typechecking verifies. If a checked semantic evaluator is exposed, it needs a real tagged word value and positive-width carrier, not those placeholders.

## Width zero preserves the original integer encoding

`DafnyPrelude.bpl:19` defines `Bv0 = int`; `BplBvType` uses that alias, and the existing where predicate is x == 0. `AddBitvectorFunction`, `AddBitvectorShiftFunction`, and the width-zero conversion constructors give actual constant bodies (integer zero, or the appropriate comparison Boolean).

Do not create `SBitvector(0)` or interpret a width-zero variable by forcing an arbitrary integer variable to zero. Retain its resolved Int representation and its original source where/havoc assumptions and checks, using the existing conservative treatment of omitted contexts. Recognize the exact actual literal Body to preserve the generator's constant operations. Widening from BV0 already becomes a positive-width zero literal, and narrowing to BV0 already becomes integer zero. BV0 division still carries an unsatisfiable source nonzero guard and must fail if executed; folding its result cannot erase that guard.

## The BV-to-Int wrapper requires a live source axiom

The default Dafny conversion is not a `Function.DefinitionAxiom` wrapper. `AddBitvectorNatConversionFunction:1035-1067` emits a separate axiom, with the exact AST shape:

    forall b: BV(W) :: { F(b) }
      (0 <= F(b) && F(b) < 2^W) && F(b) == N(b)

Here F is the ordinary called wrapper object, N is the same-width actual native unsigned BV-to-Int declaration, and the two range comparisons use the same F/b identity. The emitted Axiom defaults to `CanHide = false`.

A narrow recognition route should require: actual axiom object present in this program's top-level declarations; `!CanHide`; one bound variable of exactly BV(W), no type parameters or where clause; the exact unconditional conjunction above with literal zero and exact 2^W; the same resolved F and N objects throughout, no captures, guards, extra conjuncts, or existential; and the one positive trigger group containing exactly F(b). F and N are monomorphic BV(W)->Int. N must independently pass native-attribute/signature recognition and Body precedence. The bound must be compared with bounded exact arithmetic.

The trigger is an availability condition, not cosmetic matching: pinned `AxiomVisitor.VisitTriggerCustom` adds the singleton incoming dependency {F}; `Pruner.ComputeDeclarationDependencies` creates F -> axiom. Demand from a retained source call, or a retained body's transitive dependency, reaches F. `Pruner.GetLiveDeclarations` cannot hide this edge when `CanHide` is false. With pruning disabled all top-level axioms are present. This supplies a concrete demand-implies-live proof obligation for the exact route. It must be reviewed against the current G3 per-check mask/definition inventory: a candidate may not borrow a hideable or removed axiom from another mask. If this guarantee does not apply, require explicit membership in the selected active context or leave the call incomplete/unsupported.

For every source model satisfying this live axiom, F(b) equals the native unsigned value for every word, so replacing F calls preserves expression values. The range conjuncts are already consequences of the native carrier. The replacement adds no global equation or round-trip axiom. Dropping other source constraints remains a conservative loss of completeness. Detached metadata, a guarded equality, a differently triggered/hideable axiom, a wrong-width native declaration, or name similarity alone is insufficient. `--additional-axioms` remains unchanged and off by default; the backend must not synthesize its opt-in round-trip axiom.

## Universal operation correspondence obligations

Use U_W and modulus M_W = 2^W to write a width-parametric model argument, with no host-machine-width assumptions.

1. **Literals and arithmetic.** Source SMT literal printing equals the canonical target literal. Addition, subtraction and multiplication reduce their integer results modulo M_W. Bitwise operators agree at each position 0 <= i < W; unsigned comparisons order U_W. The maps between words and [0,M_W) are bijective, so native BV quantifiers and function domains have the same carrier.
2. **Divide/remainder.** For nonzero divisor, unsigned quotient/remainder agree with ordinary quotient/remainder of U values. For divisor zero, native BV division has its specified word semantics, distinct from the underspecified Int/Real zero case: unsigned quotient is all ones and remainder is the dividend. Preserve every original `DivisorNonZero` obligation and its identity. A zero-divisor source operation cannot become a verified unit merely because its target word result is total.
3. **Extract/concat.** Source `[start,end)` lowers to SMT `[end-1:start]`, requiring 0 <= start < end <= input width. Its value is floor(U/2^start) mod 2^(end-start). Concat's left operand is the high word: U_concat(x,y) = U(x)*2^(width(y)) + U(y), width = width(x)+width(y). Reject empty extract, overshoot, negative indices and width overflow rather than emitting a zero-width sort.
4. **Conversions.** IntToBV(n,W) has unsigned value n mod M_W, including negative n. Native BV-to-Int returns U_W, not a signed interpretation. Widening is high-zero concat; narrowing is low-bit extract. Retain source range and fit checks. BV/Real conversions compose the exact reviewed Real-to-Int floor or Int-to-Real embedding already emitted, with source integrality checks retained; no general implicit promotion or invented IsInteger guard.
5. **Shifts.** The native count is itself BV(W); let c = U_W(count). Logical left/right shifts return zero for c >= W. Counts are never reduced modulo W for shifts. Dafny's emitted source bound is inclusive, 0 <= n <= W, and its pre-VC tree converts n to BV(W) before calling the shift. Since W < 2^W for every W > 0, a guarded Int count survives this conversion unchanged. For a differently sized BV source count, preserve the emitted widening/extraction and original bound check. Do not replace the tree with a shift by an earlier Int expression, or construct a host power 2^c for an enormous count.
6. **Rotations.** The correspondence theorem must refer to the source's actual already-converted BV count, not an earlier unbounded Int count. A portable implementation is possible if the pinned extended-rotate contract is confirmed to be rotation by its unsigned count modulo W. It must be proved for all positive W, all words and all representable counts, not inferred from a diagnostic pair.

The mathematical native theory is described by [SMT-LIB FixedSizeBitVectors](https://smt-lib.org/theories-FixedSizeBitVectors.shtml). Its current conversion terminology differs from the pinned producer's legacy spellings. The unsigned interpretation of the selected conversion and the extended-rotate APIs must be checked against the pinned solver; current [Z3 bitvector documentation](https://microsoft.github.io/z3guide/docs/theories/Bitvectors/) and [Z3 C API documentation](https://z3prover.github.io/api/html/group__capi.html) provide primary contract references, not a version-specific verification receipt.

## Rotation implementation choice and proof

Do not treat the pending NativeRotate diagnostic as evidence of a solver or translator bug. First inspect the exact pinned solver's extended-rotate contract/implementation and captured query signature. If it differs from intended Dafny rotation, report the mismatch; do not silently repair default Boogie behavior or grant B3 a stronger interpretation. A tested pin can be an explicit experimental restriction, but operational agreement alone is not a universal semantic proof.

For a confirmed modulo-W extended contract, lower using only ordinary word operations. Let x and y already have width W, let C be the W-bit literal for W (representable because W < 2^W), and let r = bvurem(y,C). Then:

    rotateLeft(x,y)  = bvor(bvshl(x,r), bvlshr(x,bvsub(C,r)))
    rotateRight(x,y) = bvor(bvlshr(x,r), bvshl(x,bvsub(C,r)))

Use bounded AST construction and let bindings for repeated terms. r is in [0,W), C is nonzero, and C-r is exactly W-r without wrap. When r=0 the complementary shift is W and yields zero, so the result is x. This covers W=1. When r>0 the two shifted portions occupy disjoint positions, and each output bit comes from the appropriate index modulo W. That bit-index argument proves equivalence to the confirmed source rotate for every word/count. No axiom is added.

For a non-power-of-two width, `count & (W-1)` is not a remainder and is forbidden. Large counts must remain exact words; no cast to an Int32/Int64 count or hardware masking. The above formula applies to widths such as 3, 5 and 67 and to counts larger than the width or a host integer range. For actual Dafny calls the original 0 <= count <= W check is still emitted and retained, including the count == W identity case. Direct typed native controls can exercise other representable counts, without pretending those satisfy the Dafny operation's guard.

An interim direct ext_rotate spelling is defensible only as an explicitly pinned native path with exact typed signature and a reviewed contract; it cannot be advertised as portable. If that review is unresolved, report rotations as Unsupported and keep their phase open. The complete user scope is unchanged.

## G1 result and model boundary

The desired implication is normalized verification validity => validity of the corresponding source obligations, conditional on the existing G1 command/learning and G3 context correspondence. State it by countermodel preservation: for any source model and source state witnessing a failed obligation under its live source context, interpret target BV carriers through the word bijection, preserve native values, and preserve closed uninterpreted symbols through the existing abstraction map. Native expression evaluation agrees by structural induction and the universal operation lemmas. Exact Body expansion and the eligible live wrapper rewrite preserve values; omitted source assumptions/definitions enlarge available target models rather than restrict them. Therefore a source countermodel remains a target countermodel. This is a proposed English theorem, not an already mechanized end-to-end theorem.

Each width-indexed target UF or map instance still uses its exact typed arguments/results; do not merge width-specific instances by names or erase a BV sort into Int. Existing map support can then admit closed BV key/value sorts through the type extension; higher-order or polymorphic map limitations remain explicit. Builtin recognition does not justify new global injectivity, extensionality, reachability, literal uniqueness, or boxing assumptions.

Learning/check aggregation is independent of total arithmetic. Preserve original divisor, conversion, shift and rotation checks, source IDs and policy. Even when a failed guard is subsequently learned and later checks become vacuous, the whole unit must retain the failure. No primitive rewrite may delete a guard or cause partial traversal to be published as Verified.

The fresh B3 proof gate establishes the actual resolver/checker/visitor/library contracts it verifies. It does not turn the placeholder Raw value semantics into the correspondence theorem, prove the pinned SMT solver sound, or prove default Boogie resource parity.

## Coherent checkpoints and smallest defensible first fragment

1. **Checked B3 language and native solver layer.** Add the positive-width type/literal/operation algebra and the full shared visitor, checker, parser/printer, and SMT layers. Keep all existing contracts; prove successful executable checks imply their checked predicates. Add no semantic axiom. Hold rotation exposure pending the contract choice. Record a new source inventory; old Real receipts fail closed.
2. **Checked protocol and host construction.** Extend the typed wire representation with word literals and explicit indexed-operation metadata, and canonical native BV type names. Bump schema to 3 / normalizer experimental-3 in every producer, consumer, fixture and package pin. Validate width/cost/number/arity/signature bounds before BigInteger powers or native Raw construction. Older worker/schema identities must reject unambiguously. A wire node cannot assert it is already checked. No arbitrary SMT string crosses the host.
3. **Native Boogie boundary, initially without the ordinary conversion wrapper or rotations.** Recognize direct native positive-width typed declarations, low-bit source literals, extract/concat, same-width arithmetic/bitwise/unsigned order/shifts and native Int-to-BV/BV-to-Int declarations. Add the narrow BV0 actual constant-Body route separately. This is the smallest useful defensible fragment: simple actual Dafny BV methods and overflow/shift assertions can use it while conversions through the ordinary wrapper and rotations remain explicitly incomplete/Unsupported. Ordinary native-free functions remain UFs under existing declared incompleteness. Claimed unknown builtins fail closed.
4. **Complete emitted conversions and rotations.** Add the exact live-wrapper recognition with reviewed demand-implies-live/G3 argument and adversarial controls. Implement confirmed portable rotations, or the documented tested-pin restriction, with the universal count/width argument. Preserve existing source checks and compose existing Real operations. Complete the source census and declare any remaining unsupported producer form.
5. **Evidence and delivery.** Obtain a fresh verified B3 library build from the exact changed inventory, both C#/Java compile coverage and parser/printer round trips, focused bridge/host/protocol controls, and actual Dafny corpus with unchanged old negative controls. The coordinator runs gates; measured counts/source/toolchain identities are recorded from completed receipts. Required default Boogie verdict/RU compatibility remains a separate applicable acceptance gate. No public-green or full-language claim precedes these results.

The phased fragment is not a request to reduce P7 to machine widths or omit rotations/conversions permanently. Unresolved source/SMT interpretation is a concrete gate to finish, not permission to insert a convenient interpretation.

## Universal proof obligations and executable anti-vacuity controls

English/model obligations are universal over positive width, word values, and relevant counts; the following executable controls validate the implementation boundary and do not replace those obligations.

* Positive widths 1, 3, 5, 7, 8 and 67 exercise multiple representations, high-bit-set words, width-one edges, non-power-of-two rotates, and values/counts beyond host integer ranges. Preserve existing BV0 controls. Include modular overflow/underflow, bitwise complement, unsigned high-bit comparisons, extract/concat ordering, and source literals at/above 2^W whose printed low bits agree.
* Native shift controls cover 0, W-1, W, W+1 and a large representable count; shifts above W yield zero. Native rotate controls cover 0, W-1, W and large counts at non-power-of-two widths, plus inverse/composition identities and false identities. Actual Dafny count-negative/count-above-W cases must fail the original bound check, rather than be treated as valid native calls.
* Conversion controls include native negative-Int wrapping, unsigned BV-to-Int range, guarded legal round trips, rejected narrowing/range conversions, Real-to-BV fractional failure, and composed Real embedding. Default wrapper support is tested without enabling additional axioms. False asserted round trips and false value equalities must still fail in the same conversion-triggering context.
* Preserve `DivisorNonZero` controls for BV division and remainder, including zero and BV0; a later arithmetic assertion becoming vacuous after a failed learned guard cannot make the unit Verified. Include a false assertion in a satisfiable native-BV context with demanded conversions/rotations so new primitive paths cannot make all obligations trivially true.
* Boundary rejection: nonpositive or excessive widths, mixed-width operands, incorrect operator arity/results, noncanonical Raw literals, invalid extract bounds, concat-width overflow, impossible indexed conversion/extension signatures, wrong/native-looking attributes, unknown native attributes, ambiguous semantic metadata, and unsupported zero-width native sorts. Bounds are exercised before allocation-heavy arithmetic.
* Recognition controls bind actual declarations: a misleading function name with a genuine valid builtin remains correctly native; the same name without that semantic declaration does not. A Body disagreeing with its native-looking attribute is not silently interpreted by that attribute. Wrong formal/result widths, reordered actual/formal identities, detached objects and free captures cannot enter a primitive route.
* Wrapper controls: missing/detached axiom, hideable axiom, wrong/extra/missing trigger dependency, guarded equality, existential or type-polymorphic binder, wrong range, wrong-width native conversion, captured variable or reordered mismatched calls all reject native recognition. Positive controls use the actual producer axiom and confirm the same selected source context supplies it. G3 mask controls ensure another check's revealed axiom cannot be borrowed.
* Raw/resolved parser and all three printers round-trip every exposed type/literal/indexed-operation form; malformed forms fail. Shared C# and Java runtime compilation and direct malformed-library admission exercise the executable checker, not only C# protocol validation.
* Keep every existing Int, Real, map, visibility, wrong-signature and negative corpus verdict control. Source inventory/package checks must reject the old Real library with schema 3; new receipts record actual proof/runtime denominators rather than assumed counts.

## Source pointers for implementation/review

Dafny producer: `BoogieGenerator.cs` AddBitvectorFunction / AddBitvectorShiftFunction / AddBitvectorNatConversionFunction / AddBitvectorIntRoundTripAxiom; `BoogieGenerator.BoogieFactory.cs` BplBvType / BplBvLiteralExpr; `BoogieGenerator.ExpressionTranslator.cs` TranslateUnaryOpExpression / TrExprSpecialFunctionCall; `Expressions/TranslateBinaryExpr.cs` unsigned arithmetic, comparisons and shift lowering; `BoogieGenerator.Types.cs` ConvertExpression / IntToBV / CheckResultToBeInType; `BoogieGenerator.ExpressionWellformed.cs:1049-1096,1523-1537` zero and count checks; `BoogieGenerator.cs:3934-3940` width-zero where clause.

Pinned Boogie: `Source/VCExpr/Boogie2VCExpr.cs:436,746-780,1542` word literals, extract/concat, actual Body substitution; `Source/Provers/SMTLib/SMTLibLineariser.cs:237-260,879-920,1012-1042` effective builtin, low-bit literal, indexed printing; `Source/Core/AST/Expression/AbsyExpr.cs:1081-1091,4008-4113` BvConst and extraction bounds; `Source/Core/AST/AbsyType.cs` BvType; `Source/Core/AST/Absy.cs:511-559` CanHide default; `Source/VCGeneration/Prune/{AxiomVisitor,DependencyEvaluator,FunctionVisitor,Pruner}.cs` trigger edges, transitive demands and hideability; `Source/Core/StandardVisitor.cs:1362-1376` confirms the read-only function visitor traverses the actual Body.

B3/bridge: `ThirdParty/B3/src/RawAst/{Types,RawAst,Values,Printer}.dfy`; `src/Ast/{Ast,Resolver,TypeResolver,ExprResolver,TypeChecker,DomainInstantiation,FunctionDesugaring,ResolvedPrinter}.dfy`; `src/Parser/Parser.dfy`; `src/Driver/Library.dfy`; `src/Verifier/Incarnations.dfy`; `src/Solver/{RSolver,SolverExpr,Solvers}.dfy`. Bridge paths: `Source/DafnyCore/Backends/B3/B3Normalizer.cs`, `Source/DafnyB3Protocol/{B3Protocol,ProtocolValidation,WorkerPackage}.cs`, `Source/DafnyB3Host/RawAstBuilder.cs`, and the worker build/package/source-inventory scripts and every identity fixture.
