# Bounded callables that survive storage (#266)

Design from the review at https://github.com/erniecohen/dafny/issues/266#issuecomment-6807660254, recorded verbatim. Opt-in under `--modular-termination`; no product change.

## Findings

- Recommend a primitive, nondependent type `bounded<O,F>`: preserve a checked implementation, an immutable ghost bound, and the exact ordinary arrow flavor.
- An ordinary predicate `Cert(f,b)` cannot satisfy the equality requirement: equality substitution transports any such predicate.
- Evidence can erase to the existing closure representation; zero additional runtime payload requires ghost-only bound/identity observations and no compiled equality or evidence-producing casts.
- Formation must check the body, precondition, and reads computation in every permitted future invocation state; creation-state facts about mutable captures are insufficient.
- Cyclic capture graphs, collections, returns, and ghost fields are admissible when every executed callback edge strictly decreases; evidence is duplicable.
- Mixed-order composition needs globally coherent embeddings; independently valid comparisons chosen afresh at each edge can admit a cycle.
- M3 must change in both resolver and verifier: accepting more expressions while retaining the current verifier early returns would leave calls unchecked.
- The concrete five-field and direct sequence-helper overlay below replaces 26 existing lines and adds 71 lines: 97 additions, 26 deletions, 123 diff lines.
- That overlay does not complete the independent M4b trait/rank migration; its remaining rank obligations are explicit, and no certified Std build is claimed.

## Design

**Proposed repository document:** `docs/dev/design/266-bounded-callables.md`. The complete Markdown document supplied with the response contains the exact overlay diff and expanded implementation/test tables. This single comment contains the design and review; no repository file is added.

**Review basis.** Product source is pinned to `428e348b0b125cb67505f03a0026b3f497f98e48` on `review/4.11.0`. The M4b document is PR #244 at `49aaa27c66d1e0ed9d24a3362ce379bc3fd2fbcd`; the accepted review's replacements for §§6.2 and 7.3 take precedence over the older text. The owner decision on #264 makes its corrected comparator the default. #266 remains opt-in under `--modular-termination`. [Request][request] · [M4b][m4b] · [Accepted review][review] · [#264 decision][order264].

The source pin is the inspection base; it is not evidence that the later #264 repair has landed at that pin. All proposed composition rules require the accepted correction.

**Status.** This is a language/encoding design and source review, not an implementation. No repository files, branches, or labels were changed. The migration is a proposed patch against source snapshots. No Dafny, compiler, or solver execution was performed; **every Dafny example and every proposed syntax spelling below is unchecked**. Static source inventories and patch counts were checked. Prepared with AI assistance and independent reviews of soundness, implementation, and the Std migration.

## 1. Representation decision

### 1.1 The recommended type

Use:

```dafny
bounded<O, F>
```

Here `O` occupies a **kinded static admitted-order position**; `F` must normalize to a supported ordinary arrow flavor, or be an arrow-constrained parameter. Neither position is an arbitrary unconstrained Dafny type parameter. F retains its arity, argument/result types, and flavor:

| Ordinary payload F | Meaning retained by the bounded type |
|---|---|
| `(A ~> R)` | Possibly heap-reading and partial |
| `(A --> R)` | Read-free, possibly partial |
| `(A -> R)` | Read-free and total |

Multiple arguments and zero arguments use the existing arrow syntax. In the examples, `nat` and `ORDINAL` in the order position name their existing corrected order adapters. This adds a way to name an existing order, **not a new order primitive**. A user-defined `O` uses #245's admitted identity and operations. Its final surface declaration syntax belongs to #245.

A value contains, semantically:

```text
c = (realization, environment, instantiation, formation-world, a, proof)
a : O.Carrier
erase(c) : F
```

The realization identifies executable code and its captured environment, not merely its denoted mathematical function. The proof covers the application's body and both descriptor computations. Its allowance `a` is a value snapshot. The proof is persistent and duplicable.

Expose:

```dafny
c.bound                         // ghost, immutable, O.Carrier
c.erase                         // ordinary-arrow view F
certify<O>(a, lambdaOrMember)    // checked formation
widen(c, b)                      // prove c.bound <=O b; same implementation
c(args)                         // certified application
c.requires(args)
c.reads(args)
```

The final parser spelling may change; these operations and their proof rules should not.

Do **not** initially put a term-valued bound directly in the type, as in `A ~>{b} R`. That requires rules for dependent type substitution, scope, mutable `b`, type equality, inference, and artifact signatures. The current arrow infrastructure substitutes type parameters, not arbitrary term indices. Keeping the bound in the value makes ordinary field, collection, and generic typing sufficient. A later indexed abbreviation can mean a checked refinement such as `c.bound <=O b`; it must not make a stored bound follow a mutable variable. [Arrow representation][arrow] · [Type infrastructure][types].

### 1.2 Exact typing and subtyping

For `B_O(F) = bounded<O,F>`:

1. `O` is invariant, including its carrier, strict/weak relations, static parameters, and canonical identity.
2. `B_O(F1) <: B_O(F2)` exactly when the existing sound arrow subtyping permits `F1 <: F2`. Arguments are contravariant and the result covariant. In particular, `->` may weaken to `-->`, then to `~>`; reverse conversions need their ordinary precondition/frame proofs.
3. Such coercions retain the realization and bound; they change the callable view. The bound is not inferred from the result type or from the visible body at a use site.
4. `c.erase:F` is a projection. There is no raw-arrow-to-bounded subtype, dynamic cast, or inverse projection.
5. `widen(c,b):B_O(F)` checks carrier membership and `c.bound <=O b`. It makes a new allowance view of the same realization. It cannot narrow the allowance.
6. A receiving contract can instead require `c.bound <=O b` without widening. No minimum bound, least bound, join, or successor is inferred.
7. The initial implementation requires the **same canonical O** for application, forwarding, and widening. Built-in numeric views normalize coherently through their trusted adapter; fixed-schema native tuple padding uses the one accepted interpretation. General mixed-order support is deferred until #245 supplies one ambient order and fixed coherent embeddings for the entire interacting contract group. Record those embeddings in interfaces/artifacts; do not select a new common embedding per edge. Equal carrier representations are insufficient.
8. Mutable containers retain their existing invariant element types. There is no writable alias conversion from `array<B_O(F)>` to `array<F>`. Immutable containers can explicitly map `.erase`; this loses the element certificates in the resulting container.

Full-package equality is available in ghost reasoning. Copies of a package are equal. Equal erased functions need not be equal packages, and even equal implementations can have different allowance views after widening. Equality of erased views proves ordinary behavioral facts only.

`bounded<O,F>` has no compiled equality, hashing, or built-in decreases order, like ordinary arrows. It cannot satisfy a compiled `(==)` constraint. Sequence elements and map values are supported; compiled set elements/map keys need the usual equality characteristic. Ghost sets/maps can use package equality. Check positivity, allocation, and other semantic type restrictions against both F and the logical rank/capture components. Give the package its own conservative cardinality analysis: `bounded<nat,(() -> bool)>` may have infinitely many distinguishable bounds even though its raw result-function space has only two denotations. Runtime erasure must not erase those logical distinctions.

Initially give this type **no automatic default and no automatic nonemptiness characteristic**. An order may have no available carrier witness, and a total result type may be empty. An explicit certified witness can establish nonemptiness. A source `:|` choice still proves existence; an uninitialized field/array element cannot acquire a certificate by a type assumption. The existing general-arrow auto-initialization path must not be inherited accidentally. [Default/type code][types].

### 1.3 Ghost and compiled status; exact cost claim

The payload is compilable precisely when its source lambda/member and ordinary F are compilable. A ghost bound/proof does not taint a runtime payload. Conversely, a payload obtained from a ghost variable or ghost field remains ghost; erasure cannot move it into runtime code.

The bound, proof, order descriptor, and implementation-sensitive observations are ghost. They cannot select a runtime branch, provide runtime equality/hash behavior, or be recovered by runtime reflection, serialization, `is`, or `as`. Backend type descriptors must not expose distinctions based only on erased O.

The compilation lowering is:

```text
|bounded<O,F>| = |F|
|certify(a,L)| = |L|
|widen(c,b)| = |c|
|c.erase| = |c|
|c(args)| = ordinary closure application
```

Thus there is **zero additional runtime storage, wrapper allocation, bound computation, or indirect call for the evidence**. Existing closure allocation, captures, and backend adaptations needed for ordinary arrow subtyping remain. This is a design target to test per backend, not a measured performance claim.

The verifier retains the full package in generic values, datatypes, fields, and containers while compilation erases it. That relation is intentionally non-injective: several proof views can compile to the same closure. Only the outer certificate is forgotten by source `.erase`; bounded types nested in F's arguments or result remain bounded source types. Recursive backend erasure happens later.

### 1.4 The two alternatives

| Question | (b) Nominal reference trait | (c) Predicate on ordinary arrows |
|---|---|---|
| Exact value shape | `Callable<O,A,R>` object with immutable ghost bound, checked implementation, and Apply/Requires/Reads protocol | Ordinary `f:F` plus ghost `Cert<O,F>(f,b)` |
| Typing | The object must implement every protocol obligation. Merely constructing an object around raw f proves nothing | F keeps existing typing; predicates are ordinary ghost Boolean facts |
| Subtyping/variance | Use nominal inheritance; make O, A, R invariant in this concrete library design. Explicit verified adapters can perform safe contra/co conversions | Existing F variance; allowance weakening and arrow-view changes require preservation lemmas |
| Ghost/compiled status | Runtime object for runtime dispatch; ghost object only for ghost use. Ghost proof members erase | f compiles normally; predicate and proof erase |
| Erasure to ordinary F | A closure capturing the object and calling Apply | Identity |
| Typical extra runtime cost | One wrapper object and, when converted to F, a capture closure plus virtual forwarding; an existing implementing object may avoid the wrapper allocation | None |
| Main difficulty | Requires/Reads are separate virtual calls; their phases or Closed promises must justify those calls. Recognizing them as one closure-entry protocol needs compiler support | Ordinary equality necessarily transports Cert |
| Judgment | Sound with a complete protocol, but heavier and less direct than a new callable value | Cannot meet the fixed equality decision as stated |

For (b), an exact library protocol needs public slot bounds, constructor-established immutable rank data, ordinary local termination, and independently justified lower descriptor phases or Closed descriptor computations. An implementation's Apply body must check its stored callbacks; an invariant that simply asserts their termination is not evidence. Making the wrapper ghost does not provide a compilable dispatch target. A compiler-recognized zero-cost trait is possible, but it would be an alternative surface for essentially the same primitive semantics.

For (c), the obstruction is logical, not a missing trigger:

```text
f == g  and  Cert(f,b)  implies  Cert(g,b).
```

Opacity does not disable equality substitution. Adding a ghost token parameter does not fix a realization link that is still only an ordinary predicate of f. A sound version needs an intensional package or compiler-maintained realization judgment. That ceases to be a predicate-only library solution and introduces sidecar/payload synchronization that (a) avoids.

These are representation tradeoffs. The trait alternative is not rejected as unsound; no fictitious nontermination counterexample is claimed for its fully checked version.

## 2. Orders and the semantic contract

Let an admitted O supply carrier W, strict relation ≺, and nonincrease relation ≼. Require the accepted M4b laws:

- ≺ is well-founded.
- ≼ is a preorder containing ≺.
- `x ≺ y ≼ z ==> x ≺ z` and `x ≼ y ≺ z ==> x ≺ z`.
- Bound evaluation, comparisons, carrier checks, and their supporting proofs belong to the independently established Closed stratum.

A certificate bound is a **proper** W value. TOP remains M4b's external marker for a missing bound; users cannot put TOP in a certificate. Proper values may be infinite ordinals.

For tuple instances use the accepted compatible lift:

```text
L_i = s_i || (w_i && L_(i+1));  L_end = false
N_i = s_i || (w_i && N_(i+1));  N_end = true
```

with consistent TOP padding and component lower-bound conditions. Each first-class order descriptor fixes its carrier and tuple schema. Do not use an unrestricted variable-length TOP-padded tuple as a well-founded carrier: `(0) > (0,0) > (0,0,0) > ...`. The finite-composition arity argument does not justify that new type.

#266 consumes the corrected #264 adapter. Compare option-off output against a baseline that already contains that independent fix. Reject `--modular-termination` combined with a legacy comparator that invalidates these laws; a complete modular certificate must not silently use it. [Accepted order construction][review] · [#264][order264] · [#245][order245].

Write `Cert(c,a,F,H0)` below as **metatheory notation**, not a user predicate on an ordinary arrow. It states that the named realization, including all descriptor computations, satisfies F under allowance a at every permitted invocation world descended from formation world H0.

## 3. Exact operational and proof rules

### 3.1 Lambda formation

To check `certify<O>(a, L)`:

1. Resolve the exact F, O, captured values, and type/order instantiation. Check carrier membership and compute a once. Its computation and proof dependencies obey M4a's Closed requirement.
2. Evaluate/capture ordinary source expressions under the creating activation's effects and ordinary well-formedness rules. Capture references by their source-language value semantics, not snapshots of all reachable fields.
3. Start a separate formation obligation. Fix captured values and a; universally quantify a legal invocation heap H and legal argument tuple x. Retain the source lambda's allocation, old-heap, and formation-world envelope.
4. Check the reads expression and precondition computation under allowance a, **without first assuming the precondition**. Include their calls, type constraints, defaults, and implicit evaluations.
5. Under the checked precondition, check the body, its ordinary termination, read frame, result type, and any returned bounded values under allowance a.
6. Exact General calls satisfy nonincrease plus the ordinary discipline. Open calls and unknown certified applications satisfy strict decrease. Closed calls use their established ordinary proof.
7. Export the package's typing/formation facts only with that complete obligation. Do not use the proposed certificate's own termination theorem while proving it.

Creation needs **no** `a ≺ creatingBound`: creating a deferred value does not invoke it. Applying it later does. This allows a low-bound factory to return a higher-bound closure if all actual factory computations satisfy the factory contract.

The pin already havocs a good invocation heap in lambda well-formedness, normally with `HeapSameOrSucc`. The new formation proof extends that path; it must not replace it with the current heap. The `--heap-free-lambda-facts` variant has a deliberately different allocation/heap envelope; only facts proved in that envelope may be exported. [Lambda checking][wf].

### 3.2 Formation from certified implementations

There are three cases:

- **Already bounded value:** copy it, safely change its arrow view, or widen it after a nonincrease proof. No body recheck is needed.
- **Named admitted exact implementation:** evaluate the receiver once and retain its realization identity. Check a simulation into allowance a. Its body-entry public bound must be ≼ a for every legal future state/argument satisfying its precondition. Generate all-argument formation checks from its public Requires/Reads expressions under a, or consume separately proved public descriptor certificates. The body's bound under P is not a descriptor bound outside P. A public Closed implementation can fit any supplied proper a.
- **Admitted virtual slot reference:** its realization is `(receiver, canonical slot, admitted realization binding)`, not a presumption that the private code body is known. A direct-reference constructor consumes an imported simulation for every inherited contract and all three entry operations, uniformly under future arguments/heaps. Implementation selection is finite administration with implementation-bound ≼ slot-bound ≼ a. Until M4b provides that complete three-entry theorem, reject direct virtual certification; require an explicit lambda wrapper with its strict open call.

A private implementation that happens to be a leaf is not imported Closed evidence. A named function whose rank grows with its future argument generally cannot be certified at one fixed a; a lambda with an appropriate narrower precondition can be.

Retain naked-recursive-function restrictions. `certify(a,f)` does not conceal a same-SCC member selection. Retain the current rejection of first-class references to functions with legacy callback-formal annotations until their full higher-order protocol is represented; use explicit bounded formal/result types for new first-class higher-order APIs. [Reference restriction][references] · [M3 classifier][m3].

### 3.3 Storage, return, copying, and mutation

All value-moving operations transport the whole logical package:

| Operation | Required rule |
|---|---|
| Local/parameter binding | Ordinary subtype check; preserve identity, bound, instantiation, and formation envelope |
| Immutable field or datatype component | Constructor checks the bounded input; store that package |
| Mutable field/array update | Check the new whole package against the declared type; no stale sidecar is retained |
| Field/array/collection load | Recover the package actually stored at that point, including its actual bound |
| Return/out parameter | Preserve bounded result type and any allowance refinement in the public contract |
| Branch join | Join only compatible bounded types; retain the selected value's bound, or explicitly forget evidence |
| Copy/duplicate | Free; neither copy consumes evidence |
| Widen | New proof view; old aliases and their bounds do not change |
| Erase | Drop the outer evidence conclusion, preserving ordinary payload behavior |
| Re-certify raw F | Require a fresh formation proof for actual code or the original certified package; extensional equality is insufficient |

Do not model `(rawField, ghostProofField)` as one persistent certificate unless a checked invariant and every write maintain their realization relationship. The primitive avoids that split.

For raw arrows there is deliberately no general `as bounded` operation. Rechecking an explicit lambda or a known admitted member is a proof route; copying the original package is another. Wrapping unknown raw f as `x => f(x)` does not help: its unknown application still lacks a proper certificate. A proof of equal behavior can justify replacing code with a separately certified implementation, but cannot certify the original raw computation.

### 3.4 Application and forwarding

For current activation's captured entry bound B and selected package c:

```text
typed/allocated c and World(c,H) for its formation envelope
ordinary argument well-formedness and frame obligations
c.bound ≺ B
c.requires(args)
-----------------------------------------------
c(args) is an admitted invocation under c.bound
```

World(c,H) applies to all three entry operations and to exported erasure facts. In particular, old/old@L cannot transport a future-only formation fact to a pre-formation heap; a broader heap-independent formation proof must establish such use. From TOP, every proper c.bound satisfies the strict comparison. A proper-bound caller cannot invoke an uncertified ordinary F. The existing TOP-only ordinary-arrow fragment remains governed by the accepted higher-order lemma; erasure does not create a Closed promise.

Forwarding to a formal whose allowance is q proves `c.bound ≼ q`. The recipient checks `q ≺ B` when it applies that formal. Absorption gives `c.bound ≺ B`. Forwarding is not application and needs no independent strict edge.

Keep M3 source compatibility by elaborating its callback-formal contract to an internal certified formal view. Actuals can be directly checked lambdas, admitted known implementations, an existing callback-formal view, or bounded values with the forwarding proof. The internal view is tied to the actual realization; equality to its ordinary F view cannot manufacture another formal's evidence.

A callback formal captured by a lambda can now retain that internal package. Remove the old inside-lambda restriction only where this persistent representation is available. The same rule covers sequence/array initializers, default expressions, by-method code, and any generated invocation. A syntactic field selection is not inherently unknown once its **type** carries the package.

### 3.5 Requires and Reads

The formation certificate covers **three entry operations**: application, precondition computation, and reads computation. `c.requires(x)` and `c.reads(x)` therefore require the same strict boundary `c.bound ≺ B`, with their ordinary well-formedness/frame conditions.

Do not require `c.requires(x)` in order to evaluate `c.requires(x)`. Do not assume it to prove its own termination. Reads computation is also checked before the application's precondition.

Selecting `c.requires` or `c.reads` as a value produces a **ghost bounded selector view** retaining c.bound and its world/type envelope. If c's entry maps are (A,P,R), use exactly these derived maps:

| View | Apply | Requires | Reads |
|---|---|---|---|
| c.requires | P | true | R |
| c.reads | R | true | R |

Repeated selectors normalize to these maps: c.reads.reads aliases R; the Requires maps of selector views are true. This is a checked derived-constructor schema from an existing certificate, never a wrapper that reenters c at the same bound or an axiom certifying arbitrary raw selector handles. Explicitly erasing a selector forgets its evidence.

`.bound` and `.erase` themselves are intrinsic projections, not dispatches. Evaluating their receiver expression still performs any required field reads or calls. A certified value is not automatically Closed merely because projecting its bound is Closed. [Arrow selector definitions][system].

### 3.6 Mutable captures and future invocation states

A mutable capture's formation-time condition is usable later only if it is immutable, follows from a maintained invariant available independently, or is established by the callable's invocation precondition.

A typical safe pattern is a bounded closure that reads a mutable target only under a precondition establishing both the target's callable precondition and its strictly smaller bound. Its own precondition/reads calculations must themselves be certified. Self-installation may make that precondition false; this is allowed, and the subsequent application must then fail its precondition.

The captured allowance never changes when a captured object's rank field changes. A new value written into a field carries its own immutable bound. The caller's B is also its entry snapshot, not a rank expression reevaluated after mutations.

The certificate does not confer a class invariant. In particular, an inequality needed to evaluate `Valid()` cannot be obtained by first assuming that same `Valid()`. Constructor postconditions alone do not make a fact available for every arbitrary imported object reference.

### 3.7 Generic instantiation and higher-order values

A generic derivation is checked for arbitrary type arguments satisfying its declared characteristics and arbitrary **admitted** static order descriptors satisfying their laws. Substitution preserves the exact O, F, captures, constraints, and world envelope. It does not run a private-body classifier after instantiation.

Generic identity, boxes, tuples, sequences, and map values preserve evidence because they preserve the whole logical value. Type erasure in generated code is not a source cast. Bounded values cannot be smuggled through `(==)`, automatic defaults, erased mutable aliases, reflection, or unchecked extern deserialization.

A higher-order callable can itself take/return bounded values in F. Its formation proof quantifies over all such typed inputs, and its result check preserves their evidence. This does not invent type polymorphism inside a monomorphic Dafny arrow: generic declarations are instantiated by the ordinary language rules.

### 3.8 Positive storage control

Form a bound-zero `x => x+1`; copy it through generic identity, a mutable field, and a two-element sequence; apply it twice under bound 1 and obtain 22 from 20. Replace the field with bound-zero `x => x+2`: its double application gives 24 while the old sequence still gives 22. The full document gives the source. Each companion retains this path then fails a reachable `assert false`.

## 4. Soundness and encoding review

### 4.1 Required model

The pin's ordinary lambda handle is built from body, Requires, and Reads maps. It does not record an independent implementation identity. Attaching certification to that handle would collapse the distinction this design requires. [Handle construction][translator] · [Arrow axioms][btypes].

Introduce a separate logical package domain/tag, its erasure and bound projections, and a guarded type-membership predicate. The underlying Boogie sort may contain junk: do not assume every element is a valid certificate, especially when a source type might be empty.

A formation constructor is indexed by a nominal code site/member realization, type/order instantiation, captured values, the needed heap/layer information, and the bound snapshot. Checked formation supplies guarded membership/projection facts. Projection to ordinary F preserves its application, Requires, and Reads behavior in the same type/allocation/world envelope.

Never add any of these facts:

```text
forall raw:F :: exists c:bounded<O,F> :: c.erase == raw
c.erase == d.erase ==> c == d
Cert(raw,a) follows from raw == someCertified.erase
unconditional certified membership of every package constructor
raw box/unbox inverse that reconstructs erased evidence
```

The runtime realization relation links a logical package to the closure that its payload actually compiles to. There is no public constructor for an arbitrary code identity. Compiler-generated members and substitutions cannot clone a proof-success token into a changed realization.

Each asserted family needs a written model/derivation and a reachable false control. The feature's new semantic facts are gated by the explicitly requested `--modular-termination`; they must not appear in the option-off prelude. Do not require an unrelated `--additional-axioms` setting to make the core soundness rule exist.

### 4.2 Extension of accepted M4b §7.3

The theorem remains relative to a complete accepted total-correctness composition, valid recorded assumptions, the sound base verifier/ordinary termination discipline, and compiler/runtime correspondence. Partial verification or an unchecked extern is not an assumption-free realization.

**Closed stratum.** First establish the independent Closed computations, including order operations and rank-supporting facts, as in the accepted review. They do not acquire permission to invoke General callbacks by reading a ghost bound.

**Proper bounds.** Use well-founded induction together with the ordinary static/module/SCC/local discipline. A precise induction region at a consists of General activations whose current bounds are ≼ a. Exact General steps remain in that region and obey the ordinary discipline. Every dynamic step to d from c with `d ≺ c ≼ a` satisfies `d ≺ a`; its termination is supplied by the lower-bound induction hypothesis. This formulation also covers weak steps that are not literal equality.

A formation proof establishes all three operations of its realization in every permitted future state, under exactly those lower-bound hypotheses. It cannot assume arbitrary same-bound certificates terminate. Implementation selection and forwarding preserve strict progress by absorption.

**Preservation.** Creation checks the realization; copies, fields, returns, and collection constructors preserve it; mutable writes preserve the declared package type; selection recovers the selected package; widening uses nonincrease; erasure only drops conclusions. These facts preserve the typed-heap/realization invariant.

**Cycles.** An object can be reachable from its own callable's captures, and a collection can contain a callable that captures that collection's owner. No heap-acyclicity premise is needed. Reentering the same concrete package/activation bound along a path containing a strict edge would require an impossible strict cycle after absorption. Repeated code locations with decreasing arguments or newly created lower-bound packages are allowed. A callback whose precondition excludes the cyclic state is simply inapplicable there.

**Ghost storage.** The same proof applies to ghost fields and ghost collections. Ghost erasure is a compiler property, not an exemption from logical totality. A ghost-only divergent function can invalidate functional reasoning even though no runtime loop remains.

**Infinite executions.** Infinitely many dynamic boundaries give infinitely many strict decreases across weak segments, contradicting well-foundedness. A tail with only exact calls contradicts the ordinary termination discipline. Infinite sequential reuse is ruled out by ordinary loops/recursion, not by consuming evidence.

**TOP.** Finally use the accepted ordinary higher-order fundamental lemma for TOP-only computations, with Closed and proper-bound computations now established total. Unknown arrow applications are not fictional static call-graph edges. Preserve lambda formation, recursive-reference restrictions, and every implicit-evaluation route needed by that lemma.

This is a substantive relative argument for the proposed rules, not a claim to have mechanized Dafny soundness or completed the implementation coverage proof.

### 4.3 Modularity and proof issuance

An imported `bounded<O,F>` result is a contract carrying persistent evidence. A consumer uses that signature, its exported allowance refinements, O's identity, and ordinary behavior specifications. It does not reopen the producer's private lambda body.

The producer must prove every formation, descriptor, result-membership, override, and realization-binding obligation. Its M4b expected-obligation inventory must include them independently of the tasks actually scheduled. Timeout, cancellation, out-of-resource, bounded checking, skipped verification, stale results, or post-verification semantic mutation cannot issue a completion record.

Private body replacement with the same public interface requires new producer formation proofs, but should preserve the client's semantic solver input and reuse of already established client theorems. Exporting only F intentionally drops the certificate; export projection cannot silently retain a hidden promise.

## 5. Rejected unsound rules: concrete divergence kernels

These are proposed-syntax **semantic regression kernels**, all unchecked. They show the trace admitted by the specified wrong rule. They are not claims that the pinned checker accepts unimplemented syntax. A worker must turn each into an isolated parser-valid fixture and require the named failure; an incidental parse/frame/precondition error is not success.

The safe trait alternative, conservative lack of default inference, and lack of automatic joins are not unsound rules; they need no invented divergence example.

### K1. Equality transfer, arbitrary wrapping, narrowing, or generic re-forging

```dafny
module A {
  trait {:termination false} T extends object {
    function f(): int
      ensures f() == 0
      decreases {:global} 1
  }
  function high(t: T): int
    ensures high(t) == 0
    decreases {:global} 2
  { t.f() }
}
module B {
  import A
  type Zero = h: bounded<nat, (A.T -> int)> | h.bound == 0
    ghost witness certify<nat>(0, (t: A.T) => 0)

  class C extends A.T {
    const cb: Zero
    constructor(h: Zero) { cb := h; }
    function f(): int {
      var ignored := cb(this);
      0
    }
  }

  method Main() {
    var good := certify<nat>(0, (t: A.T) => 0);
    var raw := A.high;
    assert forall t: A.T :: raw(t) == good.erase(t);
    // A wrong equality-transport rule uses this behavioral equality.
    var h: Zero := certify<nat>(0, raw);  // MUST reject.
    var c := new C(h);
    var result := A.high(c);
  }
}
```

The trace is `high(2) -> C.f(1) -> h(0) -> high(2) -> ...`. The result-zero specifications follow from the bodies/contracts; they do not make the operational implementation low-bound.

The correct checker rejects the formation simulation `2 <= 0`, or rejects raw variable certification before that VC. A known-member version `certify<nat>(0,A.high)` must reach the `2 <= 0` obligation. Pointwise behavioral equality is sufficient to state the bad policy; do not require the test to depend on the SMT solver deriving arrow equality automatically.

Variants, each with this same complete kernel:

| Wrong rule | Replace the marked construction by | Intended rejection |
|---|---|---|
| Wrap any raw arrow | Same construction, omit the behavioral equality assertion | No implementation evidence |
| Allow narrowing | `widen(certify<nat>(2,A.high),0)` | Nonincrease `2 <= 0` |
| Generic erasure creates evidence | Pass raw through ordinary `identity<F>`, then reinterpret its compiled representation as Zero | Type/realization mismatch |
| Ghost formation is unchecked | Make f/high/field/use ghost and forge h in a lemma | Same formation obligation; ghost source evaluation has the same infinite trace |
| Order identity ignored | Treat rank 2 in one nominal order as certified rank 0 in another without a common embedding | Order mismatch/simulation |

The ghost variant concerns logical totality; its loop is erased at runtime. It is not advertised as an executable runtime loop after ghost erasure.

### K2. Non-strict callback application

```dafny
type Zero = h: bounded<nat, (C -> int)> | h.bound == 0
  ghost witness certify<nat>(0, (c: C) => 0)

class C {
  const cb: Zero
  constructor(h: Zero) { cb := h; }
  function f(): int
    decreases {:global} 0
  { cb(this) }
}
function high(c: C): int
  decreases {:global} 0
{ c.f() }

method Main() {
  var h: Zero := certify<nat>(0, high);
  var c := new C(h);
  var result := high(c);
}
```

Exact calls are acyclic/local and may keep outer bound 0. The callback closes the cycle. If application accepted `0 <= 0`, execution would be `high -> f -> cb -> high` forever. The intended failure is **strict application `0 < 0` in C.f**.

### K3. Checking only the capture heap

A small executable core is:

```dafny
class Cell {
  var cb: bounded<nat, (() ~> int)>
  constructor() { cb := certify<nat>(0, () => 0); }
}
method Main() {
  var c := new Cell();
  var g := certify<nat>(1, () reads * => c.cb());
  c.cb := g;
  var result := g();
}
```

A creation-heap-only check sees the old total bound-zero callback. After installation, `g -> c.cb -> g` diverges; c is reachable from g's captures and contains g. Both actual installed lambdas have true preconditions. Correct future-state formation rejects it; the small fixture may expose both a future-bound and a future-precondition VC.

Use this more isolated ghost fixture when asserting the exact future-bound failure:

```dafny
class Box {
  var k: bool
  constructor() { k := false; }
}
trait {:termination false} T extends object {
  ghost predicate ready()
    reads *
    decreases {:global} 1
  ghost function f(): int
    requires ready()
    reads *
    decreases {:global} 2
}
ghost function high(t: T): int
  reads *
  decreases {:global} 3
{ if t.ready() then t.f() else 0 }

type Zero = h: bounded<nat, (T ~> int)> | h.bound == 0
  ghost witness certify<nat>(0, (t: T) => 0)

class C extends T {
  ghost const cb: Zero
  constructor(ghost h: Zero) { cb := h; }
  ghost predicate ready() { cb.requires(this) }
  ghost function f(): int { var ignored := cb(this); 0 }
}
method Main() {
  var box := new Box();
  ghost var g := certify<nat>(0,
    (t: T) reads * => if box.k then high(t) else 0);
  var c := new C(g);
  box.k := true;
  ghost var result := high(c);
}
```

Here high has no precondition, every relevant frame permits all reads, and g's precondition is true. The rejected obligation is **`3 <= 0` in the future high branch during formation**. The wrong rule sees box.k=false. After mutation the trace is high(3), f(2), g(0), high(3), forever.

For a collection version of the small runtime core, store the callback in a one-element sequence owned by c and have the lambda call the selected element; a current-state-only proof again loses after replacement. For an isolated collection VC, retain K3's high/ready scaffold, use `seq<Zero>`, let ready guard `|fs| > 0` before querying `fs[0].requires(this)`, and invoke `fs[0]` under ready. Changing storage does not alter the failed formation inequality.

### K4. Omit Requires, or assume P while checking P

Complete descriptor kernel, with ghost functions because these are specification computations:

```dafny
trait {:termination false} T extends object {
  ghost predicate p() decreases {:global} 1
}
ghost predicate high(t: T)
  decreases {:global} 2
{ t.p() }

type Zero = h: bounded<nat, (T --> int)> | h.bound == 0
  ghost witness certify<nat>(0, (t: T) => 0)
class C extends T {
  ghost const cb: Zero
  constructor(ghost h: Zero) { cb := h; }
  ghost predicate p() { cb.requires(this) }
}
method Main() {
  ghost var cb: Zero :=
    certify<nat>(0, (t: T) requires high(t) => 0);
  var c := new C(cb);
  ghost var result := cb.requires(c);
}
```

The application body is constant. Requires evaluation loops `requires -> high -> p -> requires`. Reject **`2 <= 0` while forming the precondition descriptor**. Replacing `high(t)` by `high(t) && false` demonstrates why P cannot be assumed while proving P's own termination: the left operand still diverges.

### K5. Omit Reads

```dafny
trait {:termination false} T extends object {
  ghost function r(): set<object>
    ensures r() == {}
    decreases {:global} 1
}
ghost function highReads(t: T): set<object>
  ensures highReads(t) == {}
  decreases {:global} 2
{ t.r() }

type Zero = h: bounded<nat, (T --> int)> | h.bound == 0
  ghost witness certify<nat>(0, (t: T) => 0)
class C extends T {
  ghost const cb: Zero
  constructor(ghost h: Zero) { cb := h; }
  ghost function r(): set<object> { cb.reads(this) }
}
method Main() {
  ghost var cb: Zero :=
    certify<nat>(0, (t: T) reads highReads(t) => 0);
  var c := new C(cb);
  ghost var footprint := cb.reads(c);
}
```

Reads evaluation loops `reads -> highReads -> r -> reads`; the body is again constant. The intended failure is **`2 <= 0` while checking the frame computation**. The read-free protocol and exported empty-result facts discharge the independent frame obligations: highReads reads nothing and returns {}, and the bounded partial-arrow type makes cb.reads empty. Do not count a missing-read error as this test's success.

### K6. Live bound expressions instead of snapshots

Use K1's classes and replace its construction by:

```dafny
ghost var k: nat := 2;
var h := certify<nat>(k, A.high);
k := 0;
var c := new C(h);
var result := A.high(c);
```

If h.bound tracked the live k, h would falsely become a Zero and reproduce K1's loop. Correctly, h.bound remains 2; the constructor's Zero membership fails. This also tests that widening never changes an existing alias.

### K7. Writable erased aliases or unsound mutable covariance

Use K1. Create an `array<Zero>` initialized with good, then apply the rejected conversion:

```dafny
var xs := new Zero[1](i => good);
var rawAlias: array<A.T -> int> := xs;  // MUST reject.
rawAlias[0] := A.high;
var c := new C(xs[0]);
var result := A.high(c);
```

The raw write would manufacture a bound-zero certificate on the later typed read. The trace is again K1. A per-element copy to a separate immutable erased sequence is safe; a writable shared alias is not.

### K8. Bypass ordinary recursive-reference/local termination checks

A minimal source shape is:

```dafny
function self(x: int): int
  decreases x
{
  var f := certify<nat>(0, self);
  f(x)
}
```

It must retain the existing naked same-SCC reference rejection; a certificate cannot justify invoking the unproved implementation. Independently, an infinite `while true` repeatedly applying a harmless certified callback must still fail its ordinary loop decreases obligation. Neither repeated copying nor a Closed callee establishes loop termination.

K8 is an admission/ordinary-termination test, not an excuse to relax the strict application rule. The preceding kernels isolate strict-bound failures without relying on this rejected recursive reference.



### K9. Choosing a different common embedding at each edge

Even two singleton carriers expose this mistake. Let A={a} and B={b}, each with empty strict relation and identity nonincrease. Both are well-founded. For the callback edge embed a as true and b as false in the Boolean order; for the return edge reverse those placements. Each embedding preserves its own singleton order, yet the two edge comparisons allow a cycle.

An isolated Dafny-shaped kernel is below. A and B in the order positions denote their admitted singleton/datatype order descriptors; the rejected extension is permission to choose those contradictory embeddings per edge, not an ordinary Dafny conversion.

```dafny
datatype A = A0
datatype B = B0

class C {
  const cb: bounded<B, (C -> int)>
  constructor(cb: bounded<B, (C -> int)>) { this.cb := cb; }
  function f(): int
    decreases {:global} A0
  { cb(this) }
}
function high(c: C): int
  decreases {:global} B0
{ c.f() }
method Main() {
  var cb := certify<B>(B0, high);
  var c := new C(cb);
  var result := high(c);
}
```

Under the bad callback comparison B0<A0, and the oppositely chosen exact-call comparison A0<=B0, `high -> f -> cb -> high` loops. The ordinary static edges alone are acyclic. The initial design rejects the descriptor mismatch (or, for native normalization, the nonexistent strict rank decrease). A future mixed-order design must reject the incompatible embedding environment. One coherent ambient comparison cannot validate both edges.


## 6. Exact Std/Actions migration

All five identified fields are immutable at the pin; four are runtime and one is ghost. Mutable-field correctness therefore needs separate tests. The ordinary arrows have different flavors, which must be retained. [Actions][actions] · [Producers][producers] · [Consumers][consumers] · [Frames][frames].

### 6.1 Fields and constructor inputs

| Family | Before field | After field |
|---|---|---|
| FunctionAction | `const f: I --> O` | `const f: bounded<ORDINAL, (I --> O)>` |
| TotalFunctionActionProof | `ghost const f: I -> O` | `ghost const f: bounded<ORDINAL, (I -> O)>` |
| FunctionalIProducer | `const stepFn: S -> (S, T)` | `const stepFn: bounded<ORDINAL, (S -> (S, T))>` |
| FilteredProducer | `const filter: T -> bool` | `const filter: bounded<ORDINAL, (T -> bool)>` |
| FoldingConsumer | `const f: (R, T) -> R` | `const f: bounded<ORDINAL, ((R, T) -> R)>` |

The exact constructor signatures become:

```dafny
// Was: constructor(f: I -> O)
constructor(f: bounded<ORDINAL, (I -> O)>)

// Was: ghost constructor (action: FunctionAction<I, O>, f: I -> O)
ghost constructor (action: FunctionAction<I, O>,
                   f: bounded<ORDINAL, (I -> O)>)

// Was: constructor(state: S, stepFn: S -> (S, T))
constructor(state: S, stepFn: bounded<ORDINAL, (S -> (S, T))>)

// Was: constructor (source: Producer<T>, filter: T -> bool)
constructor (source: Producer<T>, filter: bounded<ORDINAL, (T -> bool)>)

// Was: constructor(init: R, f: (R, T) -> R)
constructor(init: R, f: bounded<ORDINAL, ((R, T) -> R)>)
```

The patch keeps the proof constructor on one line; the display wrap above is not a counted edit.

FunctionAction still accepts total/read-free and stores a partial/read-free view. This coercion keeps identity and bound, so its existing `ensures this.f == f` remains appropriate.

TotalFunctionActionProof has two comparisons, in its constructor precondition and Valid body:

```dafny
// Before:
requires action.f == f
&& action.f == f
// After:
requires action.f.erase == f.erase
&& action.f.erase == f.erase
```

Both fields already have independent certificates. Here **ordinary arrow value equality** equates Requires and Reads as well as application by congruence. It can transfer the total-arrow behavioral property; it cannot transfer implementation evidence. Equality of results only on jointly permitted arguments is insufficient for this proof: an empty-domain partial arrow would satisfy that condition vacuously.

The five field types, five constructor inputs, and two equality lines are **12 replaced lines**, not the entire migration.

### 6.2 Direct sequence helpers and exact source delta

FunctionAction and FoldingConsumer pass their fields into Seq functions and lemmas. Passing only `.erase` would lose the proof. Add five bounded variants, leaving other clients' ordinary helpers intact:

| New helper | Bounded payload | Global allowance | Local recursion |
|---|---|---|---|
| MapBounded | `T ~> R` | `f.bound + 1` | `|xs|` |
| MapPartialFunctionBounded | `T --> R` | `f.bound + 1` | None |
| LemmaMapPartialFunctionDistributesOverConcatBounded | `T --> R` | `f.bound + 1` | None in the copied proof |
| FoldLeftBounded | `(A,T) -> A` | `f.bound + 1` | `|xs|` |
| LemmaFoldLeftDistributesOverConcatBounded | `(A,T) -> A` | `f.bound + 1` | `|xs|` |

These specialize to the existing ordinal order. Every callback/descriptor call has `f.bound < f.bound + 1`; recursive exact calls preserve the outer bound and decrease sequence length. This uses existing ordinal successor, not a successor assumption on arbitrary O. A generic version instead takes a proper allowance b and proves `f.bound ≺ b`.

MapPartialFunctionBounded relies on preservation of the empty-reads property through bounded partial-to-general coercion. Its concatenation lemma uses MapBounded's retained length and pointwise postconditions. The copied proof may need trigger/reveal adjustments after implementation; no solver success is claimed. [Existing Seq helpers][seq].

**Measured count of the concrete proposed overlay:**

| File beneath Std | Added | Deleted |
|---|---:|---:|
| Actions/Actions.dfy | 13 | 13 |
| Actions/Producers.dfy | 4 | 4 |
| Actions/Consumers.dfy | 9 | 9 |
| Collections/Seq.dfy | 71 | 0 |
| **Total** | **97** | **26** |

Thus: **123 added/deleted diff lines**, equivalently **26 existing lines replaced plus 71 new lines**. The replacements comprise 12 core evidence changes and 14 helper-reference/reveal changes. New lines include the five helpers, spacing, and one comment. The complete proposed diff is included in the accompanying Markdown document. The precise field/API changes, helper contracts, proof obligations and count are given here.

This count covers the five-field persistent-evidence change and its direct sequence support. It **excludes** the independent M4b openness/rank overlay, tests, documentation, and release machinery. Claiming that 123 lines certify the entire standard library would be false.

### 6.3 Required per-family obligations

Let B_m denote the **independently justified, captured** M4b entry allowance of source member m. These names specify proof obligations, not unresolved expressions to paste into source.

| Family | Exact new obligations |
|---|---|
| FunctionAction | Valid's Map helper needs `f.bound+1 ≼ B_Valid`; ValidHistory's quantified Requires/application calls need `f.bound ≺ B_ValidHistory`; ValidInput's Requires call needs `f.bound ≺ B_ValidInput`; Invoke's application and Map proof helpers are covered by `f.bound+1 ≼ B_Invoke`. Constructor postcondition Valid and ValidChange/Requires/Ensures paths must obey their separate slot edges. |
| TotalFunctionActionProof | Both stored values are independently certified; full erased-arrow equality proves behavioral totality. The class does not directly apply its f field. Calls through action in Action/AnyInputIsValid/validity protocols retain their independently checked slot allowances. |
| FunctionalIProducer | Each `stepFn(state)` in Invoke needs `stepFn.bound ≺ B_Invoke`; total F covers later states. The constructor preserves the package and establishes its inherited-slot rank compatibility. No new `(!new)` constraint on S or T is required. |
| FilteredProducer | The one explicit filter application at the pin is in Invoke, not a specification body. Every loop iteration needs `filter.bound ≺ B_Invoke`. Source.Next/source-slot obligations and the existing `source.Decreasing()` loop proof remain separate. Repr growth does not change the callback's allowance. |
| FoldingConsumer | Constructor/Valid/Fold proof code need `f.bound+1 ≼ B_constructor/B_Valid`; Invoke needs `f.bound+1 ≼ B_Invoke`. Fold recursion decreases sequence length. The callback applies to every later accumulator value, including reference-valued R. |

Where a slot permits it, a leaf can write `decreases {:global} f.bound + 1`. Otherwise use the M4b public rank expression and prove the table's inequalities. Every explicit implementation allowance must be no greater than **all** inherited slot allowances.

Do not compare scalar callback b to caller tuple `(b,1)` assuming the latter is greater: TOP padding reverses that intuition. The helper patch uses scalar ordinal bounds throughout.

The missing M4b rank contracts cannot be supplied by merely assuming Valid while proving Valid's own termination. This is a genuine separate dependency: the pin has no global/callback clauses in these sources, GenericAction.Invoke's existing virtual Decreases is not an M4a-safe rank provider, and supporting InputsOf/OutputsOf/IsSome contracts also need migration. The accepted review inventories 21 introduced abstract slots and 26 concrete classes. A fully certified Std line count requires that concrete overlay; it cannot honestly be inferred from the five fields. [GenericAction][generic] · [Accepted inventory][review].

## 7. Implementation plan at the pin

All paths below are under Source/DafnyCore.

| Area | Required edits |
|---|---|
| Dafny.atg; new BoundedArrowType/certification/view AST nodes | On-mode contextual syntax, exact arrow flavor, static O, ghost bound; no term-dependent type index. |
| AST/Types/{ArrowType,Types}; SystemModuleManager; Cloner/Substituter and signature cloners; Printer | Own logical type/characteristics/cardinality, safe variance, selectors, capture-avoiding substitution, faithful printing; never clone successful proof state. |
| Resolver/NameResolutionAndTypeInference and PreType | Both inference paths: formation/application, order identity, generic substitution, no evidence inferred from raw F. |
| ExpressionTester, GhostInterestVisitor, type-characteristic visitors | Erased metadata vs ghost payload; reject defaults/equality/reflection/casts and mutable alias forgery. |
| Resolver/ModularTermination.cs | Replace syntax-only Classify/CallClassifier/EdgeCollector paths with typed descriptors; cover implicit applications and M4a effects. |
| DetectUnsoundFunctionReferencesVisitor | New nodes/adapters remain visible to same-SCC checks. |
| Verifier/BoogieGenerator.Decreases.cs | EnterLambda snapshots; RegisterCallbackLambdas, CheckCallbackArguments/Application gain persistent evidence; remove early returns only with checked replacement paths. |
| ExpressionWellformed / ExpressionTranslator / Types | Future-world checks for all three operations, guarded package/projection encoding, own boxing/membership, exact allocation/layer/canCall envelope. |
| Statements/TrAssignment and TrCall; DefiniteAssignment; returns/datatypes | Whole-package transport, field/array/load/out/return checks; no fabricated inhabitedness. |
| SinglePassCodeGenerator and backend type mappers | Erase to ordinary payload/application; validate no extra evidence wrapper or runtime O argument. |
| DooFile / LibraryBackend / M4b inventory and cache | Canonical interfaces, expected formation coverage, fresh realization bindings, strict admission; AfterParsingClone is not proof. |

Update Reference Manual Types/Expressions/Specifications, modular-option help, news/266.feat, review base and regressions. Each new logical fact needs its model and false control. Resolver acceptance and verifier checking must land together. [M3][m3] · [Translator][decreases].


## 8. Regression and validation plan

There are 48 specified families. Every negative must reach the named rule, with expected diagnostic/obligation category and source location; a parse/frame/precondition failure is not a substitute for an intended termination inequality. Every accepted family gets a companion retaining its successful path and then a **reachable assert false**, which must be the only failure. Ghost controls need satisfiable concrete witnesses; partial verification is not a control.

| IDs | Required coverage and intended rejection |
|---|---|
| BC01–04 | Total/partial/heap-reading lambdas; every arity; ordinary precondition/frame failures; immutable storage/return preservation. |
| BC05–08 | Mutable replacement uses new bound; raw returns lack evidence; duplication consumes nothing; nested capture applications strictly descend. |
| BC09–12 | Self-installation; harmless heap cycles; K3 future 3<=0 failure; satisfiable future-state preconditions checked before body. |
| BC13–16 | K4/K5 descriptor cycles; sequences; initialized arrays and larger-bound replacement. |
| BC17–20 | Map values/tuples/datatypes/boxing; ghost fields; ghost-to-runtime rejection; erased calls allowed only through the existing TOP discipline. |
| BC21–24 | No evidence from equality/generic erasure; legal widening/forwarding; rejected narrowing/oversized actual. |
| BC25–28 | Nominal O/coherent embeddings; unrestricted generics; variance/empty-reads preservation; empty carriers/results, defaults, (0), (00), :| existence. |
| BC29–32 | Exact Closed member; uniformly bounded General member; hidden naked recursion; M4a bound/order independence. |
| BC33–36 | Seq/array initializers; first-class/repeated selectors; let/match/statement-expression/forall/trigger routes; old/two-state/heap-free world guards. |
| BC37–40 | Nested higher-order types; legacy M3 bridging; portable .doo evidence; skipped/timeout/OOR/cancelled/bounded proof cannot complete. |
| BC41–44 | Private replacement with stable client obligations; stale/tampered artifacts; four runtime Actions families; forbidden equality/hash/keys/reflection. |
| BC45–48 | Corrected real/mixed/varying-arity comparisons; ordinary recursion/loop termination; own package cardinality; full arrow equality vs common-domain result equality. |

Run both resolvers, source/artifact routes, relevant additional-axioms/consistent-obligation/heap-free interactions, and each advertised backend. Compare payload representation, captures and call path; ordinary arrow coercion cost is separate from evidence overhead. All examples remain unchecked.

With modular termination off, compare against the same prerequisite baseline including #264: old-input diagnostics/verdicts, Boogie/SMT bytes, controlled resource vectors, backend source, ordinary .doo bytes and embedded Std bytes must be unchanged. Control metadata at production; do not normalize away new semantic facts, keys or IDs. Keep the on-only Std overlay. Follow AGENTS/REVIEW's separate justified expected-verdict update commits. No compiler/solver/benchmark gate has been run here. [Rules][agents] · [Review gates][reviewmd].


## 9. One-PR milestones

1. **Type and erasure infrastructure.** Contextual syntax, both resolvers, AST/substitution/printer, ghost/characteristic rules, separate logical type and on-mode erasure. Unsupported formation/use remains rejected. Off-mode identity gate.
2. **Certification kernel.** Lambda future-state formation, exact admitted members, guarded logical model, application and normalized selector views. Preserve naked-reference restrictions. K1/K2/K4/K5 and false controls.
3. **Persistent flow and M3 integration.** Mutable/immutable fields, returns, collections, generic values, captures, initializer routes, and typed higher-order formals. Land each accepted path with its VC/lowering checks. K3/K6/K7 and controls.
4. **Portable contracts and completion.** M4b schema/inventory, fresh realization and slot binding proofs, export/refinement/cache behavior, incomplete/tampered artifact rejection. Direct virtual-slot certification is enabled only with its complete simulation.
5. **Std bounded-evidence overlay and release gates.** The exact proposed overlay, prerequisite hierarchy rank contracts, all five family controls, per-backend erasure validation, manual/help/release notes, full on-mode census and off-mode comparisons.

Split a milestone if its review is too broad; never split acceptance from its checking. Full certified Std remains a gate until **every** independent M4b obligation is discharged.

## 10. Next design question — and answer

**How should a generic combinator choose a bound for its stored/returned result, without assuming joins or successors?**

Make the caller supply a proper allowance and prove the inequalities. For composition of two pure total certified functions f:A->B and g:B->C, formation at c checks `f.bound ≺ c` and `g.bound ≺ c`, then certifies `x => g(f(x))` at c. A client invoking the composite checks `c ≺ Bcaller`. Sequential calls do not consume evidence.

For partial functions, additionally define the composite's precondition by short-circuiting `f.requires(x) && g.requires(f(x))`; the formation check validates every descriptor computation under c before assuming that precondition. Heap-reading composition must also guard any intermediate f(x) evaluation used in the reads expression by f's precondition.

No universal finite ceiling exists merely because O is well-founded, and two values of a partial order need not have a common upper bound. If the supplied order/allowance cannot prove these inequalities, that single-bound combinator instance is not expressible by this contract. A richer admitted order or a stronger public allowance contract is needed. This is an expressiveness limit, not permission to fabricate TOP evidence.

Natural and ordinal convenience APIs can use their existing successor/max operations when proved suitable; the five Seq helpers above are one such specialization. Automatic least-bound inference is unnecessary for the core implementation.

The next integration task after this design is the separate M4b Actions rank overlay: choose public independently readable ranks and safe slot/specification phases, then discharge the table in §6.3. This callback design removes the storage-evidence blocker; it does not make virtual rank evaluation independently Closed.

## References

[request]: https://github.com/erniecohen/dafny/issues/266#issuecomment-6806806961
[m4b]: https://github.com/erniecohen/dafny/blob/49aaa27c66d1e0ed9d24a3362ce379bc3fd2fbcd/docs/dev/design/211-m4b-library-openness.md
[review]: https://github.com/erniecohen/dafny/issues/211#issuecomment-6806579143
[order264]: https://github.com/erniecohen/dafny/issues/264#issuecomment-6806696165
[order245]: https://github.com/erniecohen/dafny/issues/245
[agents]: https://github.com/erniecohen/dafny/blob/428e348/AGENTS.md
[reviewmd]: https://github.com/erniecohen/dafny/blob/428e348/REVIEW.md
[m3]: https://github.com/erniecohen/dafny/blob/428e348/Source/DafnyCore/Resolver/ModularTermination.cs
[wf]: https://github.com/erniecohen/dafny/blob/428e348/Source/DafnyCore/Verifier/BoogieGenerator.ExpressionWellformed.cs
[references]: https://github.com/erniecohen/dafny/blob/428e348/Source/DafnyCore/Resolver/DetectUnsoundFunctionReferencesVisitor.cs
[arrow]: https://github.com/erniecohen/dafny/blob/428e348/Source/DafnyCore/AST/Types/ArrowType.cs
[types]: https://github.com/erniecohen/dafny/blob/428e348/Source/DafnyCore/AST/Types/Types.cs
[system]: https://github.com/erniecohen/dafny/blob/428e348/Source/DafnyCore/AST/SystemModuleManager.cs
[translator]: https://github.com/erniecohen/dafny/blob/428e348/Source/DafnyCore/Verifier/BoogieGenerator.ExpressionTranslator.cs
[btypes]: https://github.com/erniecohen/dafny/blob/428e348/Source/DafnyCore/Verifier/BoogieGenerator.Types.cs
[decreases]: https://github.com/erniecohen/dafny/blob/428e348/Source/DafnyCore/Verifier/BoogieGenerator.Decreases.cs
[actions]: https://github.com/erniecohen/dafny/blob/428e348/Source/DafnyStandardLibraries/src/Std/Actions/Actions.dfy
[producers]: https://github.com/erniecohen/dafny/blob/428e348/Source/DafnyStandardLibraries/src/Std/Actions/Producers.dfy
[consumers]: https://github.com/erniecohen/dafny/blob/428e348/Source/DafnyStandardLibraries/src/Std/Actions/Consumers.dfy
[frames]: https://github.com/erniecohen/dafny/blob/428e348/Source/DafnyStandardLibraries/src/Std/Frames.dfy
[generic]: https://github.com/erniecohen/dafny/blob/428e348/Source/DafnyStandardLibraries/src/Std/Actions/GenericAction.dfy
[seq]: https://github.com/erniecohen/dafny/blob/428e348/Source/DafnyStandardLibraries/src/Std/Collections/Seq.dfy


## 11. Final assessment

1. **Settled enough to implement:** the nondependent bounded type, intensional evidence boundary, three-operation future-state formation, strict application/weak forwarding, whole-value persistence, order abstraction, and erasure restrictions. These free implementation of stored/returned callbacks without waiting for a new primitive order or dependent types.
2. **Expected to change:** parser spelling for order descriptors, local proof/trigger organization, per-backend lowering details, and the independent Actions rank/phase annotations. The core inequalities and prohibition on equality-created evidence should remain.
3. **The next question:** generic combinator allowance selection, answered in §10; next concrete integration work is the M4b Actions rank realization.
4. **Feedback on the request:** the pinned sources and fixed equality/erasure decisions expose the real representation issue. Requiring reads/precondition checks and exact line counts is valuable. A complete Std certification count needs the independently chosen hierarchy-rank overlay; keeping that dependency explicit prevents a misleading five-field estimate.
5. **Stopped because the requested design/review list is finished. First work not performed: implementing and executing the proposed rules/tests, including the separate M4b rank overlay required for a fully certified Std build.**

