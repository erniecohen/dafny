# Soundness of the arguments type encoding for Dafny

Research review for [issue 284, latest request](https://github.com/erniecohen/dafny/issues/284#issuecomment-6821628153), 2026-11-12. AI assistance: ChatGPT.

## Status and findings

**The complete pinned Dafny prelude is inconsistent under the pinned arguments encoding and cannot provide a sound unrestricted SMT basis for Dafny.** There are two independent inconsistencies: finite raw characters, and Boolean-map extensionality combined with raw `U`-valued stores. The witnesses below isolate encoding-only failures on satisfiable many-sorted fragments; a model of the entire many-sorted prelude is not claimed. Removing Boogie's reverse-cast axioms did not remove either inconsistency.

This is a statement about the logical formulas, not a report that a compact Dafny program was observed falsely verifying under the default solver configuration. That operational reproduction has not been obtained here. Pruning and quantifier scheduling can hide an inconsistent axiom family. The four-step Boolean-map contradiction was independently checked with Z3; the unassisted quantified kernel returned `unknown` at the configured timeout.

| Finding | Evidence |
|---|---|
| **Blocking: raw `char` bounds the common domain to a finite set.** | [Prelude 123–143](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L123-L143); [integer injection 40–41](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L40-L41). |
| **Blocking: decoded Boolean extensionality and raw map stores imply `card(U) <= 2`.** | [ISet equality 888–892](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L888-L892); [Boogie map axioms 298–463](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCExpr/TypeErasure/TypeErasureArguments.cs#L298-L463). |
| **A character repair must retain parent membership for char-based subset/newtypes.** | [Types.cs 1239–1253](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Types.cs#L1239-L1253); [TypeObligations.cs 26–34](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.TypeObligations.cs#L26-L34). |
| **Finite datatypes do not exhaust the ambient `DatatypeType` carrier.** | [Guarded exhaustiveness 28–48](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/Datatypes/BoogieGenerator.DataTypes.cs#L28-L48). |
| **The published arguments-encoding justification depends on a prover discipline; it is not the requested unrestricted model theorem.** | [Leino–Rümmer, §3.1](https://www.microsoft.com/en-us/research/wp-content/uploads/2016/12/krml199.pdf). |
| **A checker must cover negated VCs and encoder support axioms, including map axioms generated after Dafny.** | [Quantifier erasure](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCExpr/TypeErasure/TypeErasureArguments.cs#L509-L576); [map axiom generation](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCExpr/TypeErasure/TypeErasureArguments.cs#L204-L258). |

The report supplies a sufficient conservative condition and proves its implication for encoding soundness. **It does not certify the entire prelude after the proposed local repairs.** Structural families requiring additional model-extension proofs are marked explicitly. This is preferable to treating an axiom-family whitelist as a proof.

### Pins and scope

- Dafny: [`25203d9115f8f104b325c8a058bf41051bdbbe69`](https://github.com/erniecohen/dafny/tree/25203d9115f8f104b325c8a058bf41051bdbbe69).
- Boogie: [`45a8869bb43456f126577f5ba6fd24725ed6e4d9`](https://github.com/erniecohen/boogie/tree/45a8869bb43456f126577f5ba6fd24725ed6e4d9).
- Measurements motivating restraint about a default switch: [issue 281 results](https://github.com/erniecohen/dafny/issues/281#issuecomment-6813786570). Those experiments establish that encoding changes can move outcomes and costs; they are not a soundness proof.

The request's established facts about the six polymorphic prelude declarations, ground type declarations, Dafny type parameters represented by `Ty`/`Box`, and guarded source quantifiers are taken as premises, not re-investigated as a separate inventory.

## 1. The soundness obligation and the actual encoding

### 1.1 Preserve counterexamples, not just one model of the axioms

Let `A` include the applicable axioms and assumptions, `V` be one verification condition, and `F = A && !V`. The required implication is

\[
\operatorname{Sat}_{\mathrm{Boogie}}(F)
\;\Longrightarrow\;
\operatorname{Sat}_{\mathrm{SMT}}(E_a(F)\land K_a),
\]

where `K_a` includes every generated cast, type-constructor, select/store, and other support axiom. Its contrapositive is the desired verification rule. A model of `A` alone, however large, does not establish this implication. Different assertions may have countermodels with different cardinality requirements.

Likewise, this result is relative to source assumptions. `{:axiom}` lemmas, trusted bodyless specifications, `assume`, and `:| assume` can intentionally introduce false assumptions. Encoding soundness means that the encoding does not strengthen them illegitimately; it does not make an inconsistent source theory consistent.

### 1.2 Two distinct type universes

Boogie's target sort of encoded **Boogie types**, called `T` in the erasure implementation, is distinct from `U`. Dafny's source sort `Ty` is ordinary data to Boogie and **is erased to `U`**. A Dafny type parameter represented by a `Ty` argument is therefore not an SMT sort or a Boogie polymorphic type parameter.

All explicit polymorphic function type parameters become `T` arguments. Type constructors have distinct constructor identifiers and projection inverses. Equality between differently instantiated type variables also checks equality of their encoded Boogie types. These measures preserve useful typing information in terms, but do not restrict an erased term quantifier's domain. [Type constructors](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCExpr/TypeErasure/TypeAxiomBuilder.cs#L70-L233), [function conversion](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCExpr/TypeErasure/TypeErasureArguments.cs#L98-L158), [equality](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCExpr/TypeErasure/TypeErasureArguments.cs#L674-L715).

### 1.3 Native types, casts, and normal form

The pin preserves `int`, `real`, `bool`, bitvectors, floats, rounding modes, strings, and regular expressions. Every other source sort, **including map sorts**, becomes `U`. [Exact predicate](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCExpr/TypeErasure/TypeErasure.cs#L283-L303).

For each native sort `B` used at a polymorphic boundary there are functions

\[
e_B:B\to U,\qquad p_B:U\to B,\qquad p_B(e_B(b))=b.
\]

Thus `e_B` is injective. The pin neither asserts `e_B(p_B(u))=u` for arbitrary `u` nor emits a type axiom assigning every `U` value a native type. Native quantified variables remain native; the old trigger-oriented retyping to unguarded `U` has been removed. [Cast creation](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCExpr/TypeErasure/TypeErasure.cs#L175-L192), [removed reverse axioms](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCExpr/TypeErasure/TypeErasureArguments.cs#L50-L79), [quantifiers](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCExpr/TypeErasure/TypeErasureArguments.cs#L509-L576).

If a source expression has native type `B`, but its translation is a raw `U` term `t`, passing it to a `U` parameter produces `e_B(p_B(t))`. This gives consistently represented typed arguments without claiming `t=e_B(p_B(t))`. The normal form applies to source expressions; it does **not** canonicalize arbitrary universally quantified payloads in support axioms constructed directly by the map axiom builder. [Normal form](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCExpr/TypeErasure/TypeErasureArguments.cs#L607-L651).

### 1.4 Maps

Map skeleton abstraction introduces select/store symbols with explicit encoded index/result types. In this path the map, all indexes, and the result are `U`, even for a source result of type `bool`. The raw read-over-write axioms quantify the payload over `U`. [Implementation](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCExpr/TypeErasure/TypeErasureArguments.cs#L204-L463).

| Source map | Target representation | Required compatibility |
|---|---|---|
| `[ref][Field]Box` | Two layers of `U`-valued abstract select/store | Compatible interpretations of heap, inner maps, references, fields, and boxes; nested type arguments remain significant. |
| `[Box]Box` | `U` map/index/result with encoded type arguments | Compatible raw carrier transport and closure under the required stores/lambdas. |
| `[Box]bool` | Raw result `U`; a source Boolean observation decodes it with `p_bool` | Native-result normalization must agree with extensionality and all map support axioms. The pin fails this obligation. |
| Maps with native indexes | Index is also erased in the map signature, then boxed at actual uses | Extra off-image indexes cannot be treated as source indexes without a separate extension argument. |

These are first-order abstract map carriers. One cannot silently replace them with full mathematical function spaces. For example, the source boxing inverse injects `[Box]bool` into `Box`; interpreting the former as the entire powerset of the latter is already impossible by Cantor, before erasure. First-order select/store models, with only admissible lambda/comprehension closure, are the relevant setting.

### 1.5 What the published paper establishes

Leino and Rümmer describe guarded and arguments encodings and compare them experimentally. In §3.1 the arguments encoding's correctness justification expressly depends on e-matching generating well-typed instantiations; it is not an unrestricted satisfiability-preservation theorem for arbitrary target reasoning. Their map account uses first-order, initially non-extensional carriers; additional extensionality may be supplied by clients. That procedural premise cannot discharge this issue's requested model-theoretic obligation, especially after adding Dafny's character and extensionality axioms. This report does not claim their guarded translation automatically proves the implementation of either current encoder correct. [Primary manuscript, §§1.1, 2.1, 3–3.1](https://www.microsoft.com/en-us/research/wp-content/uploads/2016/12/krml199.pdf).

## 2. Two counterexamples in the pinned generated theory

### 2.1 Characters force a finite common carrier

Write `C` for the valid integer codepoints. The prelude says, without a membership guard on raw characters,

```boogie
axiom (forall ch: char ::
  { char#ToInt(ch) }
  char#FromInt(char#ToInt(ch)) == ch &&
  char#IsChar(char#ToInt(ch)));
```

The round trip makes `ToInt` injective; its range lies in `C`. The other round trip supplies every valid codepoint, so the raw character carrier has exactly `|C|` elements: **65,536** in legacy UTF-16 mode or **1,112,064** in Unicode-scalar mode. Under `:a` that is a bound on all of `U`.

Independently, `Inv0_TBitvector(TBitvector(n))=n` injects all integers into `Ty`, hence into `U`. `IndexField` and native integer casts give other injections. Thus the relevant erased axioms have no model. This uses neither user axioms nor a finite user datatype.

The following exact small Boogie witness isolates the prelude pattern. Its many-sorted theory is satisfiable; after the pinned erasure its theory is unsatisfiable. Its `assert false` must fail in the many-sorted semantics. This is **not** a claim about an observed Boogie/Z3 command-line verdict.

```boogie
type Char;
type Ty;
function valid(n: int): bool { 0 <= n && n < 65536 }
function fromInt(int): Char;
function toInt(Char): int;
axiom (forall n: int :: valid(n) ==> toInt(fromInt(n)) == n);
axiom (forall c: Char :: fromInt(toInt(c)) == c && valid(toInt(c)));
function ty(int): Ty;
function inv(Ty): int;
axiom (forall n: int :: inv(ty(n)) == n);
procedure Main() { assert false; }
```

One source model takes `Char={0,...,65535}`, `Ty=Z`, and totalizes invalid `fromInt` arguments to zero. The contradiction arises only when their carriers are identified.

### 2.2 Boolean extensionality forces the decoder to be injective

Fix the map type arguments to `[Box]bool`. Let `s(m,k):U` and `w(m,k,v):U` be the raw selector/store, and `p:U->bool` the decoder. The pinned support axioms and prelude imply:

\[
\begin{aligned}
s(w(m,k,v),k)&=v,\\
k\ne j&\Rightarrow s(w(m,k,v),j)=s(m,j),\\
(\forall k:U.\ p(s(a,k))=p(s(b,k)))&\Rightarrow a=b.
\end{aligned}
\]

The third formula follows from the two `ISet#Equal` axioms. It really does compare **decoded** values: Boogie lowers Boolean equivalence to equality, and its equality eraser casts both operands back to their original native Boolean sort. In contrast, its directly generated same-index map axiom equates **raw** `U` values. [Boolean equivalence lowering](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCExpr/Boogie2VCExpr.cs#L1459-L1467), [native operand decoding](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCExpr/TypeErasure/OpTypeEraser.cs#L73-L110).

Suppose `p(u)=p(v)`. Fix `m,k`, and compare `a=w(m,k,u)` with `b=w(m,k,v)`. Their decoded reads agree at `k` by the supposition and at every other index by the second row. Extensionality gives `a=b`; reading at `k` with the first row gives `u=v`. Consequently `p` is injective, so `|U|<=2`. Three distinct prelude `Ty` constants already contradict that conclusion. No character axiom, reverse-cast axiom, integer infinitude, or full powerset semantics is needed.

An exact source-level Boogie witness is:

```boogie
type Box;
type ISet = [Box]bool;
const unique a, b, c: Box;
axiom (forall m, n: ISet ::
  (forall x: Box :: m[x] == n[x]) ==> m == n);
procedure Main() { assert false; }
```

Its source model may have three keys and eight Boolean maps. Its pinned erased theory has no model. Again the semantic result is established, while default end-to-end false acceptance has not been reproduced. This distinction is essential because a verifier may prune or never instantiate the troublesome formulas.

**Deleting only ISet extensionality is insufficient.** `IMap#Glue` returns its raw Boolean domain by equality. For any two raw domain maps `a,a'` with identical decoded membership, fix the same element map `b` and type argument `t`. `Glue(a,b,t)` and `Glue(a',b,t)` have identical decoded domains and equal elements on their common domain, because both read `b` there. IMap extensionality equates the glued maps; applying their raw `Domain` projections gives `a=a'`. Thus IMap recovers the full unsafe Boolean-map extensionality, not only a fact about empty maps. [Glue](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L1598-L1605), [IMap equality](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L1628-L1635).

## 3. The cardinality route and a sufficient condition

### 3.1 The useful part of the proposed argument

For ordinary many-sorted first-order logic, let a counterexample theory have a model in which every sort designated for merging is infinite. For any sufficiently large infinite cardinal `kappa`, add `kappa` pairwise distinct constants to each such sort. Every finite subset of those requirements is satisfiable. Compactness supplies a model; downward Löwenheim–Skolem in the expanded language supplies one of size at most `kappa`. Each designated carrier therefore has exactly size `kappa`. Transporting an ordinary ground-sorted signature through bijections to a common carrier preserves all formulas by induction.

This is a valid restricted argument. It uses the **whole counterexample theory**, not only the axioms; it does not hold native theories fixed; and it presupposes that the target is just the transported signature, with no stronger map/polymorphism support axioms.

The proposed application fails at several distinct points:

1. The pinned raw character sort is finite.
2. One infinite model of `A` does not suffice for every satisfiable `A && !V`.
3. Saying no individual sort is forced finite is weaker than joint infinitude. For example, “S is a singleton or T is a singleton” permits each separately to be infinite but never both together.
4. Standard native integers/reals cannot be enlarged by this model-theoretic construction. A raw carrier with an injection into integers and another with an injection from reals can both be infinite while admitting no common cardinality compatible with those injections. In particular, deleting only the character range conjunct leaves `U -> Z` injective, still incompatible with an active `R -> U` embedding under standard real semantics.
5. Native instances of a polymorphic quantifier range over arbitrary `U` after erasure. Ground-sort bijections do not construct their off-image behavior.
6. Map support axioms impose operations at raw values outside native images. The Boolean-map contradiction is an actual failure of that extension obligation.

Full mathematical function-space semantics is another wrong shortcut: it clashes with the source boxing injection before this argument even starts. The correct theorem must specify a first-order map universe and its closure under the particular source comprehensions.

### 3.2 A precise semantic criterion

Call a class of counterexample formulas **extension-admissible** if, for every satisfiable member `F`, there is a model of the actual target formula `E_a(F) && K_a` that preserves all source observations used by `F`. A sufficient construction supplies:

- compatible interpretations of all raw carriers, constants and functions;
- embeddings/retractions for fixed native sorts;
- interpretations for Boogie type codes and all generic function instances, including their behavior on values outside native images;
- interpretations of every select/store instance, with native index/result compatibility;
- every emitted lambda/comprehension equation, declaration/where-clause, assumption, and negated goal.

Structural induction then preserves expression observations and quantifier truth in the allowed grammar, giving the implication in §1.1. This states the correct proof obligation; it is deliberately not advertised as a decision procedure. Equal carrier cardinalities are one ingredient in some constructions, not the criterion itself.

### 3.3 A conservative, mechanically checkable sufficient condition

A useful safe baseline is a **checked comparison with explicit representation guards**. It is stricter than a rule that searches for the name `$Is`.

Construct a reference translation `G(F)` as follows. Its type-code sort denotes Boogie's actual ground types. Give each such type `s` a representation region `D(s,u)` in `U`; at native types this is exactly the image of the native embedding. Interpret `U` as the tagged disjoint union of source value domains, adding junk if desired. Dafny's `Ty` and `$Is` remain ordinary source symbols inside this translation; they do not replace `D`.

Retain every polymorphic type argument, including arguments appearing only in a function's result type. Canonicalize type aliases and bound type-variable names before comparing codes. Dispatch select/store with the full source map-type code and its instantiation; do not reuse a skeleton key in a way that identifies distinct source map types. Guards, function result typing, and heterogeneous equality must all refer to those same canonical codes.

For an erased-sort quantifier, translate `forall x:s.P` to `forall x:U. D(s,x) ==> G(P)` and translate `exists x:s.P` to `exists x:U. D(s,x) && G(P)`. Treat a type-variable-typed binder identically with its type-code variable. Native binders retain their native sorts. Add nonemptiness of represented domains and typing of constants and function results **under valid argument guards**. Use injective native embeddings with their left inverses and reverse inverses only on their image. Keep native arithmetic interpreted.

Map values are tagged members of the source map domains, not all mathematical functions. Guard select/store typing and both read-over-write rows by membership of the map, indexes, and stored value in their actual source domains. Translate lambda definitions and map extensionality with the same rule. Do not append the pinned unguarded map rows to this reference theory.

**Reference-model lemma.** Every source model of `F` has a model of `G(F)`. Embed each source value with its type tag; interpret type codes by the source type universe; interpret operations on valid tuples by the corresponding source operation and extend them arbitrarily elsewhere. Nonempty source sorts supply total projections/defaults. Native domains stay fixed, and their tagged embeddings have the required inverses. Map rows hold on valid arguments because they held in the source map model. Type-quantified formulas range over the same type codes. Lambda equations are transported source equations. An induction on terms and formulas proves the claim, with universal guards and existential conjunctions supplying the two quantifier steps. This proof is relative to the source map and source type-quantification semantics; it assumes neither unrestricted powersets nor correctness of Dafny's own axioms.

Now compute the actual target counterexample formula `E = E_a(F) && K_a`. Accept only when a small certificate checker establishes

\[
G(F)\Longrightarrow E.
\]

The implication has this direction because a source countermodel must give an **actual target countermodel**. Together with the reference-model lemma it proves encoding soundness. This comparison also detects lost small-countermodel queries, not merely inconsistent global axioms.

The initial decidable certificate language should allow only:

- alpha-renaming and type-correct syntactic normalization;
- Boolean identities and finite, capture-avoiding expansion of fresh acyclic definitions;
- checked ground instances of a quantified premise;
- deleting top-level conjuncts (weakening the counterexample formula);
- inclusion of every actual target conjunct among the normalized reference conjuncts.

With shared ASTs and hashing, simple normalization/conjunct inclusion is linear or near-linear in the represented formula size. Do not distribute to CNF, enumerate all type instances, invoke an unrestricted SMT implication oracle, erase guards under arbitrary polarity, or trust user-supplied “safe family” attributes. Any extra rewrite needs its own proof and bounded certificate check. Fresh auxiliary symbols require an explicit conservative-extension rule, not silent comparison of different signatures.

This is a precise, cheap sufficient condition on the complete emitted theory/VC, rather than a falsely complete axiom-only test. It intentionally rejects the pinned bad families and many unsupported but potentially sound cases. An early Dafny AST check can recognize the templates and dependencies; final acceptance must include Boogie's later-generated axioms.

### 3.4 The condition is nonempty, but it does not certify this repository

For example, this exact Boogie fragment has an erased carrier but no quantifiers over it:

```boogie
type Item;
const missing: Item;
function at(int): Item;
axiom (forall i: int :: at(i) != missing);

procedure Read(i: int) returns (v: Item)
  ensures v != missing;
{
  v := at(i);
}

procedure Negative(i: int) {
  assert at(i) == missing;
}
```

The reference adds representation-typing conjuncts; the substantive erased constraints are already its conjuncts. The comparison accepts. `Read` is semantically correct and `Negative` must fail. Those are logical expectations, not a run of a newly implemented checker.

Conversely, checking only an axiom list cannot justify arbitrary raw-sort assertions: a sort `S` with no axioms may be a singleton, while its erased `U` may be forced infinite by an otherwise unrelated integer embedding. An assertion of two distinct `S` values would lose that countermodel. Client restrictions or the complete negated VC are indispensable.

Here is an exact witness: `S` may be a singleton and `Other` may be the integers in the source, so the assertion is false there. Its erased assertion follows from the integer injection. The axioms themselves remain satisfiable after erasure.

```boogie
type S;
type Other;
function embed(int): Other;
function project(Other): int;
axiom (forall n: int :: project(embed(n)) == n);
procedure LostCountermodel() {
  assert (exists x, y: S :: x != y);
}
```

This also shows why even an infinite-carrier model of the *axioms* is insufficient: the same source axiom theory has models with infinite `S`, but its countermodels of this assertion have singleton `S`.

## 4. Prelude audit

The following tables classify every explicit prelude axiom. `N` identifies local native/definitional forms suitable for the baseline's simple rules; `G`, `S`, and `P` identify required guard, structural, and polymorphic/map certificates, **not completed certificates**. `F` marks a demonstrated incompatible family. None of these status letters is accepted solely from a name or source location.

### 4.1 Classification and complete axiom partition

The table is deliberately conservative. It does not claim that “nothing visibly finite” proves the whole theory sound.

- **N:** native arithmetic or a logical/definitional identity, with no erased-carrier cardinality restriction of its own.
- **S:** structural family admitting the indicated common-cardinality model in isolation or in the stated standard structure; requires a countermodel-extension certificate when combined with other families. This is not an unconditional PASS.
- **G:** a semantic typing/allocation guard family. Guards must have checked introduction/elimination/closure rules. The mere occurrence of `$Is` does not establish soundness.
- **P:** polymorphic or map machinery requiring the pinned encoder's representation theorem. Map rows use abstract select/store semantics, not full-function-set semantics.
- **F-char:** concrete finite-carrier failure under `:a`.
- **F-boolmap:** concrete noncanonical-Boolean-map failure under the pinned raw store schema.

The counts concern explicit `axiom` declarations, including axioms inside `uses`; all six conditional arithmetic axioms are counted, even though not all preprocessing configurations enable them. Comments and commented-out axioms are excluded. The file has **291** such declarations. Function bodies that Boogie unfolds/axiomatizes are accounted for separately below.

| Source range | Explicit axioms | Family | Classification | Reason / required certificate |
|---|---:|---|---|---|
| [10–12](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L10-L12) | 1 | Language marker | **N** | An unconstrained Boolean constant fixed to true. |
| [21–67](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L21-L67) | 21 | Ty constructors, projection inverses, and tags | **S** | Injective constructor families impose lower bounds (including int -> Ty), no exhaustion or upper bound. Need injective disjoint type-constructor interpretations. |
| [106–116](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L106-L116) | 3 | Literal/boxing commutation | **N/P** | Identity wrappers; preserves meanings when boxing/native casts are already certified. |
| [133–153](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L133-L153) | 4 | Character conversions and arithmetic | **F-char** | 140–143 bound the entire raw char carrier to the finite valid-code set; Plus/Minus are definitions and are not independently the cause. |
| [172–179](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L172-L179) | 1 | Local-field depth/family projections | **S** | An injection FieldFamily × int -> Field; imposes infinitude, not finiteness. |
| [188–194](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L188-L194) | 1 | Polymorphic boxing left inverse | **P** | Injects each represented type into Box. No unguarded reverse-cast axiom is present. Native and map instances require representation compatibility. |
| [202–244](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L202-L244) | 13 | Type-guarded box/unbox inverses | **G/P** | Only semantic type members receive the reverse inverse; finite types are legitimate subsets. TChar inherits the separate char defect. |
| [246–251](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L246-L251) | 2 | Boxed/unboxed membership and allocation equivalence | **G/P** | Transport of semantic predicates across polymorphic boxing; preserve parameterized instances and cast normal form. |
| [260–269](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L260-L269) | 7 | Primitive membership facts | **G; F-char at 263** | Native int/real/bool facts are on native domains; Field/Ordinal are intentionally universal. Universal char membership prevents simply adding a vacuous char guard. |
| [271–319](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L271-L319) | 9 | Collection membership definitions and closure | **G/P** | Elements, keys, or valid sequence indexes are constrained by semantic membership. Does not by itself establish representation validity of raw bool-map payloads. |
| [322–328](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L322-L328) | 6 | Primitive allocatedness facts | **G** | Primitive values need no heap allocation. Universal raw char allocatedness does not imply raw char membership. |
| [330–364](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L330-L364) | 6 | Collection allocatedness definitions | **G/P** | Pointwise allocatedness of members/keys/values; depends on structural representation and monotone heaps. |
| [367–370](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L367-L370) | 1 | AlwaysAllocated implication | **G** | An implication guarded by an unconstrained type predicate; can be instantiated only where justified. |
| [390–396](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L390-L396) | 1 | ClassName pair projections | **S** | An injective pairing; with distinct class constants this forces infinite ClassName and admits every infinite cardinal. |
| [416–419](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L416-L419) | 1 | Datatype/Box rank coherence | **S/P** | Links the rank of a datatype and its box; no finite raw-carrier bound. Needs joint rank/boxing interpretation. |
| [432–458](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L432-L458) | 6 | Ordinal offset, naturals, strict linear order | **S** | Natural injection and abstract ordinal order; no assertion that all set-theoretic ordinals are represented. |
| [461–510](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L461-L510) | 10 | Ordinal comparison synonym and arithmetic | **S** | Satisfiable on ordinals below an infinite initial cardinal; preserve finite suffix operations. Requires whole-VC extension proof. |
| [521–523](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L521-L523) | 2 | AtLayer select and layer invariance | **P/S** | For arbitrary separating maps this collapses LS to identity, not LayerType to a finite sort; no strict semantic layer axiom is given. |
| [531–561](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L531-L561) | 8 | Field dimensions, indexes, declared names, ghost marker | **S** | Projection inverses/integer indexes force large Field; no upper bound or enumeration. The three ground uses axioms are included. |
| [562–566](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L562-L566) | 1 | Ghost heap succession/frame | **G/P** | Conditional heap relation; all nonghost fields preserved. Requires typed heap observations. |
| [575–580](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L575-L580) | 2 | Monotonic allocatedness | **G/P** | Conditional monotonicity under HeapSucc; no raw domain bound. Includes polymorphic v. |
| [592–593](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L592-L593) | 1 | Array length nonnegativity | **S** | Only a nonnegative observer; no bounded injection from references. |
| [600–604](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L600-L604) | 1 | Real/int roundtrip | **N** | A native arithmetic identity, not a U/native bijection. |
| [620–622](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L620-L622) | 1 | OneHeap goodness | **G/P** | Asserts a witness good heap. The structural heap model must provide one. |
| [629–638](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L629-L638) | 4 | Heap updates/succession/transitivity/allocation | **G/P** | Guarded update edges and allocation monotonicity; allocator-cell special case avoids unconditional deallocation edges. |
| [688–728](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L688-L728) | 8 | Finite Set empty/cardinality/single insertion | **S** | Finite-subset structure over Box; card bounds each value, not the raw Set carrier. |
| [730–795](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L730-L795) | 13 | Finite Set union/intersection/difference/cardinality | **S** | Finite-set algebra; closure preserves cardinal κ for infinite Box. |
| [797–824](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L797-L824) | 4 | Finite Set subset/equality/extensionality/disjointness | **S** | Extensionality uses Bool-valued membership of an opaque Set carrier, not decoded read of arbitrary U payloads; hence it is not the same defect as ISet. |
| [834–896](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L834-L896) | 20 | ISet empty/conversion/operations/algebra/extensionality | **F-boolmap** | 889–892 directly expose the raw-store/bool-decode mismatch; removing only these two axioms does not certify the rest of this algebra. |
| [903–917](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L903-L917) | 5 | Math min/clip | **N** | Native-integer specifications with standard models. |
| [920–999](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L920-L999) | 12 | MultiSet update/goodness/empty/singleton/insertion | **S** | Finite bags; positivity plus increment excludes negative raw multiplicities, and finite cardinality bounds support. |
| [1001–1083](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L1001-L1083) | 13 | MultiSet algebra/cardinality/extensionality | **S** | Finite-bag algebra over Box; no finite cap on number of bags. |
| [1085–1129](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L1085-L1129) | 9 | Set/sequence-to-multiset conversions | **S** | Preserve finite supports/counts, share collection and Box representation. |
| [1136–1191](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L1136-L1191) | 10 | Seq length/empty/build/index/append/update | **S** | Finite-list structure over Box; integer length observer is not injective and hence is no cardinal upper bound. |
| [1195–1250](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L1195-L1250) | 9 | Seq contains/equality/extensionality/prefix equality | **S** | Extensionality restricted to indexes in the finite list bounds; no full-function interpretation is required. |
| [1252–1318](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L1252-L1318) | 14 | Seq take/drop/update/subsequence identities | **S** | True in finite-list model with totalized out-of-bounds functions; no upper bound on all sequences. |
| [1324–1335](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L1324-L1335) | 3 | Seq typing/Create via Apply1 | **G/S/P** | Typing closure and finite prefix tabulation of a function handle; requires handle/heap representation. |
| [1337–1365](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L1337-L1365) | 5 | Array-to-sequence conversion/frame/extension | **G/S/P** | Converts finite prefix given by nonnegative array length; requires heap map observations and finite-list representation. |
| [1367–1379](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L1367-L1379) | 4 | Sequence and datatype rank inequalities | **S** | Requires joint well-founded rank model; cannot certify solely by absence of an explicit finite bound. |
| [1385–1459](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L1385-L1459) | 11 | Finite Map cardinal/domain/value/items/empty | **S/P** | Finite partial-map structure; repaired tuple-image guard at 1450 avoids identifying arbitrary boxes by accidental projections. |
| [1461–1475](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L1461-L1475) | 3 | Finite Map Glue and typing | **G/S/P** | Only preserves values within finite domain; no global raw element-map inverse. |
| [1478–1530](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L1478-L1530) | 10 | Finite Map build/merge/subtract/equality/disjointness | **S/P** | Finite-map algebra with extensionality on finite-domain observations; element-map values are raw Box, not decoded bool. |
| [1536–1596](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L1536-L1596) | 9 | IMap empty/domain/value/items | **F-boolmap inherited** | Raw Boolean domains plus empty iff domain equality/nonemptiness can collapse noncanonical all-false domains once Glue is active. |
| [1598–1611](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L1598-L1611) | 3 | IMap Glue and typing | **F-boolmap inherited** | Preserves raw Boolean-map domain by equality, while its clients observe only decoded booleans. Ordinary semantic element typing does not force canonical payloads. |
| [1613–1655](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L1613-L1655) | 7 | IMap build/equality/merge/subtract | **F-boolmap inherited** | Observational IMap extensionality plus domain retraction recovers unsafe raw Boolean-map extensionality. |
| [1677–1709](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/DafnyPrelude.bpl#L1677-L1709) | 6 | Conditional integer arithmetic lemmas | **N** | Six active-text declarations: distributivity (2), quotient/remainder, product sign, commutativity, associativity. No erased sort occurs. |
| **Total** | **291** | **46 family rows** | | Every explicit axiom is assigned once. |

### Function bodies, unique constants, and procedure contracts

These do not contain an `axiom` keyword but belong in an AST checker:

- `Lit<T>`, `LitInt`, `LitReal` (106, 112, 115) are identities; any body-generated equality is N/P and must respect native cast normal form.
- `char#IsChar` (123–130) is the finite range definition. It creates no finite carrier by itself; the failure comes from requiring it of every raw character through 140–143.
- `ORD#IsLimit`/`ORD#IsSucc` (436–437), `Int`/`Real`/Floor (600–604), and arithmetic wrappers (1661–1675) are definitions. Native numeric operations remain interpreted.
- `read` and `update` (610–611) unfold into nested map selections/stores and belong to P.
- `const unique` emits pairwise inequalities *within the original Boogie sort*. It supplies lower bounds, never a finite upper bound. The encoder must not merge declarations merely because raw carriers coincide.
- `$Heap`'s `where` clause (615), and `$YieldHavoc`, `$IterHavoc0`, `$IterHavoc1`, `$IterCollectNewObjects` contracts (647–677), are assumptions/transition specifications relevant to VC countermodels. They are not covered just by checking top-level `Axiom` nodes. Their frame formulas require the heap/Box/set representation certificate, and collection-of-new-objects additionally relies on the allocated-heap finiteness invariant when its result is a finite Set.

### 4.2 Erased-sort cardinalities

These are component-wise statements about the intended or isolated family. The **entire unmodified erased prelude is inconsistent**, so in classical logic it has no nontrivial global cardinality facts; interpreting every row as a theorem of that inconsistent whole would be meaningless.

| Raw sort / synonym | What the prelude actually requires or allows | Consequence for common U |
|---|---|---|
| `Ty` (18) | `TBitvector` with inverse (41) injects all integers. Type constructors have projection inverses (44–67); their tags separate families. No exhaustion axiom lists all types. | Infinite; can be enlarged. Equality transport needs type-argument compatibility, not merely an arbitrary bijection. |
| `TyTag` (83) | Thirteen distinct declared tags (86–98), no exhaustive enumeration and no injectivity of `Tag : Ty -> TyTag`. | Finite lower bound only; may be arbitrarily infinite. |
| `TyTagFamily` (100) | Only an unconstrained tagging function in the prelude. | No upper bound. |
| `char` (133) | `ToInt` is injective, has valid-code range, and `FromInt` is its inverse on every valid code (135–143). | Exactly 65,536 in legacy mode or 1,112,064 in Unicode-scalar mode; **F-char**. |
| `ref` (159) | `null`, `locals`, array length nonnegativity; no enumeration or universal allocation finiteness axiom. | No upper bound; an infinite carrier is possible. |
| `FieldFamily` (164) | One unique constant and injective pairing into Field through `local_field`. | No upper bound; Field must accommodate it. |
| `Box` (185), `ORDINAL = Box` (425) | Polymorphic boxing injects each represented source type, including int/real when those instances are active; ORD#FromNat injects the naturals independently (440–443). | Infinite; at least continuum if the built-in real sort is standard R and its boxing/cast injection is active. No finite upper bound. Full powersets cannot also inject here. |
| `ClassName` (378) | Five distinct constants and an injective binary pairing (394–396). | Infinite: no finite n >= 2 satisfies n² <= n. No upper bound; any infinite cardinal can support a pairing. |
| `HandleType` (400) | Apply1 is declared, not axiomatically surjective onto all mathematical functions. | No upper bound in prelude; translator handle families need their own audit. |
| `DatatypeType` (411) | A ctor-id observer, rank observer, and BoxRank coherence (419); no raw datatype exhaustion in prelude. | No finite upper bound. Generated constructor/rank families may impose further restrictions and require their own proof. |
| `DtCtorId` (413) | Only a result sort for DatatypeCtorId in prelude. | No upper bound; generated distinct ctor IDs are lower bounds. |
| `LayerType` (516) | A constant, `$LS`, `AsFuelBottom`; AtLayer equates every map's observations at ly and LS(ly) (522–523). With adequate select/store separation this forces LS(ly)=ly, not a finite LayerType. | No carrier bound. A model can take LS to be identity. Fuel's role is controlling solver unfolding, not an axiomatically strict semantic successor. |
| `Field` (529) | `IndexField` injects all integers (538). MultiIndexField and FieldOfDecl also have projection inverses (544–557); field dimensions separate relevant forms. | Infinite; no finite upper bound. |
| `NameFamily` (550) | One unique constant, and injective pairing into Field via FieldOfDecl. | No upper bound. |
| `Set` (688) | Nonnegative finite cardinality of each set, membership extensionality, finite insertion/deletion/union operations. Under standard integers these describe finite subsets of Box; the axioms do not assert a raw Set carrier is finite. | For infinite card(Box)=κ, the finite-subset model has card(Set)=κ. Every finite support can be formed. The current `Set#FromBoogieMap` has **no unconditional membership preservation** (826–828), which avoids importing arbitrary infinite sets into this carrier. |
| `ISet = [Box]bool` (834) | A Boolean-map carrier with decoded Boolean membership and equality extensionality (889–892). It need not be the full powerset in a first-order map model. | **F-boolmap** with the pinned raw store schema; it forces card(U)<=2. Without that schema defect, a suitably closed Henkin family is possible; this still needs a comprehension theorem. |
| `MultiSet` (920) | Finite multisets/bags with cardinality and extensionality. Raw negativity is excluded collectively: UnionOne positivity plus increment at the added element forces each old multiplicity >=0. Update-to-zero and nonnegative cardinality bound the support. | Finite bags over infinite Box have cardinal κ, not a finite bound on the carrier. |
| `Seq` (1136) | Every sequence has finite nonnegative standard-integer length; extensionality observes valid indexes; Empty/Build construct all finite lists. Rank constraints are extra structural conditions. | Finite sequences over infinite Box have cardinal κ. Even a one-element Box yields infinitely many sequences, so Seq alone cannot share a finite U. |
| `Map` (1385) | Finite partial maps, finite Set domain/cardinality, observational equality; Map#Glue ignores element-map values outside its domain. | Finite maps over κ have cardinal κ; `[Box]Box` element maps must use the map representation theorem. |
| `IMap` (1536) | General partial maps over the chosen Boolean-map domain family; Glue retracts every raw `[Box]bool` domain into IMap. | No finite bound in a correct Henkin model, but **F-boolmap** is inherited even without explicit ISet extensionality. |
| `Heap = [ref][Field]Box` (609), inner `[Field]Box`, and `[Box]Box` | Map sorts are erased by the pinned arguments encoder too. They are not native SMT arrays retained outside U in this mode. | No automatic powerset cardinalities may be assumed. Row/store and any extensionality/comprehension facts must be certified together. |
| `Bv0 = int` (19) | A synonym for a native sort; the actual Dafny zero-width bitvector subset is restricted by translator constraints, not by a singleton Boogie carrier. | Not an additional erased carrier. |

#### The ordinal comment

`type ORDINAL = Box;  // :| There are more big ordinals than boxes` is a comment, not a cardinality axiom. The collection of **all** set-theoretic ordinals is a proper class, so it cannot literally be the domain of a first-order set-sized sort at all. The listed axioms demand only an abstract ordinal algebra: naturals, a strict linear order, offset into a finite successor suffix, and the stated addition/subtraction facts. They do not assert an injection from the power set of Box into ORDINAL, nor that ORDINAL contains every mathematical ordinal.

For any infinite cardinal κ, take the ordinal domain to be all ordinals below the initial ordinal of cardinal κ. It is closed under ordinal addition (a finite sum of ordinals of cardinality <κ still has cardinality <κ), natural embedding, and subtraction of an available finite successor suffix. Interpret IsNat/Offset as stated in the comments and interpret the weakly constrained other subtraction cases arbitrarily. The domain has cardinal κ. This demonstrates that these prelude ordinal axioms do not themselves block equicardinality with Box. It does **not** prove all translated Dafny ordinal principles sound; in particular a uniform model for additional user/program requirements must still be provided.


## 5. Translator audit


These classifications are deliberately not a claim that a finished checker already accepts all families. `G` means constraints are confined to a Dafny membership/valid-call region and can be admitted by a *proved* guard-closure certificate. `D` means conservative definition/ground identity. `C` means unguarded carrier algebra requiring a separately proved template and its interactions. `X` means a demonstrated repair obligation. `T` means a user trust boundary. Names alone are not certificates: arbitrary replacements of a predicate called `$Is` are not accepted.

| Family | Relevant emitted form and cardinality effect | Classification | Pin source |
|---|---|---|---|
| Dafny type-name constructors and tags | `Inv_i(T(args))=arg_i`; `Tag(T(args))=TagT`; unique tag constants. These impose lower bounds/disjoint images, not finite coverage. Abstract/imported type constructors deliberately omit uniqueness/injectivity. | C: free constructor algebra, no tag-exhaustion rule | [Types.cs 470–564](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Types.cs#L470-L564) |
| Per-representation boxing | `$IsBox(b,T(args)) ==> $Box($Unbox(b):R)=b && $Is($Unbox(b):R,T(args))`. Right inverse is only on the typed image. | G plus the prelude cast certificate | [Types.cs 599–625](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Types.cs#L599-L625) |
| Native bitvector typing and conversions | `$Is(v,TBitvector(w))` and `$IsAlloc` are total on native `bvW`; conversion axioms retain native sorts. `bv0` uses `int` with a where-clause equating it to 0. | D/C, native finite carriers are not erased | [Types.cs 568–596](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Types.cs#L568-L596), [BoogieGenerator.cs 4190–4195](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.cs#L4190-L4195), [BoogieGenerator.cs 1176–1260](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.cs#L1176-L1260) |
| Reference class/trait typing | Class membership iff null or `dtype(o)=T(args)`; trait membership iff null or `implements(dtype(o),args)`; object membership total. Allocation refers to a good heap/allocation bit. No declaration says every ref belongs to a finite list of classes. | G/D; object region equals ambient ref carrier | [Methods.cs 95–149](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Methods.cs#L95-L149) |
| Trait inheritance and implemented interfaces | Child `$Is`/`$IsBox` and allocation imply parent predicates; `implements` facts are generated on the named type-constructor image. | G | [BoogieGenerator.cs 1335–1389](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.cs#L1335-L1389), [Methods.cs 486–501](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Methods.cs#L486-L501) |
| Datatype constructor identity, queries and reconstruction | `CtorId(C(args))=idC`; `C?(d) <==> CtorId(d)=idC`; `C?(d) ==> exists args :: d=C(args)`. No unguarded disjunction covers `DatatypeType`. | C plus guarded image inversion | [DataTypes.cs 402–450](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/Datatypes/BoogieGenerator.DataTypes.cs#L402-L450), [DataTypes.cs 786–795](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/Datatypes/BoogieGenerator.DataTypes.cs#L786-L795) |
| Datatype constructor projection/injectivity | `dtor_i(C(args))=arg_i`, with raw representation-typed constructor arguments. This forces lower cardinality bounds, and needs compatible extension of each argument/result sort. | C; not admitted by an all-quantifiers-guarded test | [DataTypes.cs 472–492](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/Datatypes/BoogieGenerator.DataTypes.cs#L472-L492) |
| Datatype membership, allocation and destructors | `$Is(C(args),T(types)) <==> conjunction argument memberships`; destructor allocation is guarded by `C?`, allocatedness and a good heap. | G, including finite constructor ranges | [DataTypes.cs 626–783](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/Datatypes/BoogieGenerator.DataTypes.cs#L626-L783) |
| Datatype exhaustiveness/case-split helpers | `$Is(d,T(types)) ==> C0?(d)\|\|...`; `$IsA#D(d) ==> C0?(d)\|\|...`. The helper is introduced only at typed source uses. | G; the helper requires a provenance/closure certificate too | [DataTypes.cs 28–48](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/Datatypes/BoogieGenerator.DataTypes.cs#L28-L48), [DataTypes.cs 349–375](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/Datatypes/BoogieGenerator.DataTypes.cs#L349-L375) |
| Datatype equality and extensionality | `D#Equal(a,b) <==> a=b`; constructor-specific projection comparison implies equality only when both query predicates hold. The single-constructor branch already retains those guards. | D plus G; no Unit collapse of all `DatatypeType` | [DataTypes.cs 63–150](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/Datatypes/BoogieGenerator.DataTypes.cs#L63-L150) |
| Datatype rank | Raw constructor arguments rank below the result; sequence/set/multiset/map element ranks use index or membership bounds. | C: rank/finite-collection model certificate, not simple cardinality syntax | [DataTypes.cs 494–617](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/Datatypes/BoogieGenerator.DataTypes.cs#L494-L617) |
| Codatatype equality/prefix equality | Unfolding guarded by both type memberships; equality synonym and prefix/all-depth connection also have unguarded administrative components. | G/D/C jointly; coinductive semantics plus layer/ordinal model needed | [DataTypes.cs 155–344](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/Datatypes/BoogieGenerator.DataTypes.cs#L155-L344) |
| Subset/newtype definitions | `$Is(o,Tsubset) <==> parent(o)&&constraint(o)` (or split implications with can-call); allocation follows parent. Finite source types constrain this region, not the carrier. | G normally; X for char-parent optimization after char repair | [Types.cs 1138–1273](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Types.cs#L1138-L1273) |
| Abstract type characteristics and witnesses | Abstract types use Ty names/Box values, not fresh Boogie sorts; `GetTyWhereClause` currently adds `$AlwaysAllocated` for no-reference types. Redirecting-type witness expressions are checked against the base type and constraint; witness `*` introduces no proof of arbitrary membership. | G/T; inhabitation cannot be generalized to all Box | [BoogieGenerator.cs 2920–2939](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.cs#L2920-L2939), [BoogieGenerator.cs 5219–5227](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.cs#L5219-L5227), [Types.cs 1745–1798](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Types.cs#L1745-L1798) |
| Mutable/readonly/static field typing and allocation | Type/allocation facts for heap reads or field functions are guarded by receiver type, nullness, heap validity and array bounds as relevant. Static constants introduce typed image values. | G | [Methods.cs 234–482](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Methods.cs#L234-L482) |
| Field identities, constants, array lengths | Ground FDim/FieldOfDecl/ghost facts; definitional constant RHS; raw `0<=Length(o)` places no finite carrier bound. | D/C | [Fields.cs 13–58](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Fields.cs#L13-L58), [Fields.cs 126–155](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Fields.cs#L126-L155) |
| Function definitions, preconditions and result/postcondition consequences | Definitions and consequences guarded by `f#canCall`; `f#requires` linked to translated preconditions under parameter where-clauses; callers establish validity. Bodyless functions still have consequence axioms. | G; T if bodyless/trusted; the checker must certify can-call introduction paths | [Functions.cs 60–253](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Functions.cs#L60-L253), [Functions.cs 375–568](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Functions.cs#L375-L568) |
| Function frames and overrides | Same results under equal reads/frame regions; overrides linked on actual receiver type and permitted call assumptions. | G plus native heap/map certificate | [Functions.cs 872–992](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Functions.cs#L872-L992), [Methods.cs 1296–1492](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Methods.cs#L1296-L1492) |
| Fuel/layer/reveal and literal identities | Administrative equations identify fuel-shifted function values; literal identities do not constrain a value carrier; reveal implications activate definitions. | D/C, require a layer model not merely guard syntax | [BoogieGenerator.cs 1290–1314](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.cs#L1290-L1314), [BoogieGenerator.cs 1857–1978](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.cs#L1857-L1978), [DataTypes.cs 457–468](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/Datatypes/BoogieGenerator.DataTypes.cs#L457-L468) |
| Arrow Handle construction and selectors | `Apply(Handle(h,r,rd),args)=h[args]`, `r[args] ==> Requires(...)`, reads membership equality; raw map arguments and raw Box arguments are quantified. | C: actual map encoding/Henkin model essential; cannot infer a full function-space model | [Types.cs 30–125](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Types.cs#L30-L125) |
| Arrow membership, variance, allocation, frame and empty-read facts; named function handles | Membership constrains Apply on typed valid arguments; allocation consequences use captures/reads; frame properties compare good heaps. | G plus preceding handle/map certificate | [Types.cs 156–465](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Types.cs#L156-L465), [Functions.cs 575–846](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Functions.cs#L575-L846) |
| Least/greatest/prefix predicate axioms | Predicates connected to quantified approximants; parameter memberships are included, with separate ordinal/layer administrative laws. | G/C; extremal semantics and ordinal extension required | [Extremes.cs 88–230](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Extremes.cs#L88-L230) |
| Let-such-that choice | `let#canCall(env) ==> where(choice(env)) && canCall(P) && P(choice(env))`; existence is a checked VC. | G, with choice/Skolem model expansion | [LetExpr.cs 243–284](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.LetExpr.cs#L243-L284) |
| Local assumptions, axiomatic/bodyless lemma contracts and assign-such-that | `assume` translates an ordinary typed expression; bodyless lemma specifications are callable procedures without proof bodies; `:\|` checks existence before havoc/assume whereas `:\| assume` omits it. These enter VCs, not necessarily top-level axioms. | T/G; axiom-only audit is insufficient | [TrPredicateStatement.cs 33–40](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrPredicateStatement.cs#L33-L40), [Methods.cs 170–225](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Methods.cs#L170-L225), [TrStatement.cs 172–270](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrStatement.cs#L172-L270) |
| Source quantifiers, lambdas and finite-comprehension facts | Source quantifiers are guarded by where clauses; lambdas/comprehensions also introduce maps, local can-call conditions and finite-image witnesses. Lambda lowering can create axioms later in Boogie. | G/C; inspect post-lambda-lifting obligations too | [ExpressionTranslator.cs 875–923](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.ExpressionTranslator.cs#L875-L923), [ExpressionTranslator.cs 1962–2025](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.ExpressionTranslator.cs#L1962-L2025) |


## 6. Repairs, their exact scope, and expected behavior

### 6.1 Repair the character representation as a guarded subset

The smallest local change keeps `type char;` but permits raw junk outside the finite source character region. Replace the unguarded 140–143 axiom and the universally true membership axiom at 263 with:

```boogie
axiom (forall ch: char ::
  { char#ToInt(ch) }
  $Is(ch,TChar) ==>
    char#FromInt(char#ToInt(ch)) == ch &&
    char#IsChar(char#ToInt(ch)));

axiom (forall ch: char ::
  { $Is(ch,TChar) }
  $Is(ch,TChar) <==>
    char#IsChar(char#ToInt(ch)) &&
    char#FromInt(char#ToInt(ch)) == ch);
```

Retain the valid-code integer round trip:

```boogie
axiom (forall n: int ::
  { char#FromInt(n) }
  char#IsChar(n) ==> char#ToInt(char#FromInt(n)) == n);
```

The first replacement is logically redundant given the membership definition, but preserves the useful `char#ToInt` trigger. Valid `FromInt(n)` values satisfy the definition: their codepoint is `n` and their round trip follows by congruence. An equally sound local alternative replaces the total membership axiom by the one-way closure `IsChar(n) ==> $Is(FromInt(n),TChar)` and keeps the guarded first axiom. The displayed biconditional gives a direct representation definition.

**Local model.** Choose a canonical element for every legal codepoint inside an arbitrarily large raw character domain. Map those elements to their codepoints and map junk, for example, to zero. Let `FromInt(0)` be the canonical zero character, not any junk element. The biconditional then excludes every junk value. This preserves the finite source character semantics while removing the raw-carrier upper bound.

Both conjuncts of the old reverse round trip must be guarded. Removing only the range still bounds `U` by the integers and conflicts with standard-real embeddings. Adding a guard while leaving `forall ch :: $Is(ch,TChar)` unchanged is vacuous. Universal `$IsAlloc<char>` can remain: allocation independence does not imply value membership. The total `Plus`/`Minus` equations can also remain; valid source arithmetic already checks result codepoints.

#### Required translator changes

1. In `AddRedirectingTypeDeclAxioms`, remove `baseType.IsCharType` from the semantic-membership optimization that sets `parentConstraint = true`; retain `MkIs(o,baseType)` and the existing explicit constraint/can-call treatment. Leave the separate allocation optimization alone. [Types.cs 1210–1273](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Types.cs#L1210-L1273).
2. In `CheckVisibleTypeObligations`, remove char from the analogous implied-constraint-only branch, so it checks base membership before deriving the target membership. Preserve the existing guarded checks and `forget` behavior. This applies when the consistent-obligation option is enabled as well as to its migration path. [TypeObligations.cs 10–59](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.TypeObligations.cs#L10-L59).

For a transparent char subset with total constraint `P`, the exact required generated shape is:

```boogie
axiom (forall c: char ::
  { $Is(c,TSubset()) }
  $Is(c,TSubset()) <==> $Is(c,TChar) && P(c));
```

Use the generator's existing split implications when `P` needs a can-call condition; do not replace that machinery with the simplified unconditional biconditional. For `type AnyChar = c: char | true`, the membership must be precisely `$Is(c,TChar)`, not `true`.

No new general quantifier rewrite is required for plain characters: `GetWhereClause` already emits character membership, and its primitive skip list omits char. The closure paths to retain are:

| Producer/consumer | Existing path |
|---|---|
| Parameters, local/havoc variables, procedure calls and loop havoc | [GetWhereClause 4150–4209](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.cs#L4150-L4209), [locals 3880–3950](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.cs#L3880-L3950). |
| Universal/existential binders | [TrBoundVariables 1962–1987](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.ExpressionTranslator.cs#L1962-L1987), [polarity of guards 903–919](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.ExpressionTranslator.cs#L903-L919). |
| Function arguments and results | [Functions.cs 207–214](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Functions.cs#L207-L214), [430–435](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Functions.cs#L430-L435). |
| Literals and integer conversions | [literal construction 1154–1172](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.ExpressionTranslator.cs#L1154-L1172), [conversion checks 1548–1572](https://github.com/erniecohen/dafny/blob/25203d9115f8f104b325c8a058bf41051bdbbe69/Source/DafnyCore/Verifier/BoogieGenerator.Types.cs#L1548-L1572). |
| Datatype fields and generic/collection values | Constructor/destructor membership in the translator table and the prelude's `$IsBox` guarded unboxing laws. |

Representative exact Dafny preservation controls are:

```dafny
type AnyChar = c: char | true witness '\0'
datatype Cell = Cell(c: char)

lemma charRange(c: char)
  ensures 0 <= c as int < 0x110000
{}

lemma subsetCharRange(c: AnyChar)
  ensures 0 <= c as int < 0x110000
{}

lemma forallCharRange()
  ensures forall c: char :: 0 <= c as int < 0x110000
{}

lemma fieldCharRange(x: Cell)
  ensures 0 <= x.c as int < 0x110000
{}

lemma charNegative(c: char) {
  assert 0 <= c as int < 0x110000;
  assert false;
}
```

Expected after a complete repair: the four positive lemmas verify and the last assertion fails. The range is true in both character modes; Unicode-specific tests should also exclude surrogate codepoints. These are preservation/vacuity controls, **not observed old-version exploits**. A worker must run them on the pinned and repaired binaries.

### 6.2 Minimum logical repair to the Boolean-map support axiom

Let `e:bool->U`, `p:U->bool`, with `p(e(b))=b`. Replace the `[Box]bool` instance of the raw same-index row by a row restricted to canonical Boolean payloads:

```boogie
type U;
function e(bool): U;
function p(U): bool;
function s(U, U): U;
function w(U, U, U): U;

axiom (forall b: bool :: p(e(b)) == b);
axiom (forall m, x, v: U ::
  e(p(v)) == v ==> s(w(m,x,v),x) == v);
axiom (forall m, x, y, v: U ::
  x != y ==> s(w(m,x,v),y) == s(m,y));
```

This is exact Boogie text for the relevant **target schema**, with the fixed map type arguments suppressed. It replaces a generated Boogie support axiom; there is no equivalent one-line edit to ordinary Dafny source axioms that restricts the raw Boolean store payloads.

The change is a literal weakening. Every store whose source payload is statically Boogie `bool` passes a value `e(b)` after the pinned cast normalization; its premise follows from the native left inverse. Thus the correct same-index law on those terms is retained. A payload typed by a type variable is not covered by that syntactic argument merely because its type code later denotes bool; its off-image cases need the separate polymorphic proof below. Stores of noncanonical `U` payloads no longer force their payload to be an observable Boolean representation.

No additional canonical-range axiom is necessary for the ISet family. Store `e(p(s(m,x)))` at `x`: this changes no decoded read, so extensionality equates the result with `m`. The guarded row then proves `s(m,x)=e(p(s(m,x)))`. This derivation is on reads, not an unrestricted reverse cast on all `U`.

**Local model.** Choose infinite `U` of cardinality `kappa` and a bijection between `U` and its finite/cofinite subsets. Interpret `s` as canonical Boolean membership and `w` as changing membership at one key according to `p(v)`. There are `kappa` such subsets, closed under the elementary Boolean set operations and finite updates. This satisfies the repaired rows, decoded extensionality and Boolean casts while allowing arbitrarily many distinct `U` values. It does not prove closure under all translated comprehensions, all IMap image operations, or all other prelude families.

#### Where to implement it

Change `MapTypeAbstractionBuilderArguments.GenMapAxiom0`, with an explicit result-representation argument or a checked interpretation of the skeleton's result type parameters. For each relevant native result sort `B`, the guard must express

```boogie
resultType == BType ==> e_B(p_B(v)) == v
```

using the actual Boogie type-code terms and cast functions. For a non-native result the condition is `true`. Construct this expression, rather than declaring an unconstrained predicate whose name suggests canonicality. Keep the pinned left inverses and cast normal form. [Map signature/row generation](https://github.com/erniecohen/boogie/blob/45a8869bb43456f126577f5ba6fd24725ed6e4d9/Source/VCExpr/TypeErasure/TypeErasureArguments.cs#L204-L357).

That is the narrowest repair of the demonstrated Boolean-payload contradiction. A general map theorem must additionally handle native **indexes**, where quantifying raw off-image indexes can also be too strong. It must handle the generic `AtLayer<A>` map axioms; the existing concrete handle-valued call sites do not by themselves erase the universal prelude axioms. Do not label this local edit a complete Boogie soundness fix.

#### More structural alternatives

| Route | Exact essential change | Assessment |
|---|---|---|
| Preserve native map components | At `[Box]bool`, use `s(U,U):bool`, `w(U,U,bool):U` with ordinary typed rows; preserve native index sorts too. | Clearer representation theorem, larger signature/cache change. Specialize by native sort shape before skeleton abstraction, or prove a generic dispatch scheme. |
| Opaque ISet | Replace the map synonym by `type ISet;` and membership `ISet#IsMember(ISet,Box):bool`; raw Boolean maps enter through a noninjective adapter. | Keeps extensionality on source observations, but changes many prelude and translator uses. |
| Full representation guards | Use the reference construction in §3.3 for all carriers, maps and generic instances. | Broadest direct model proof, largest change to formulas. |
| Monomorphic encoding | Preserve the original ground sorts and native map component sorts. | Soundness of the erasure step becomes straightforward, but source axioms and other translation steps still need their own soundness; measurements already show outcome changes. |

The exact Boolean-map laws for the native-component alternative are:

```boogie
function sb(U,U): bool;
function wb(U,U,bool): U;
axiom (forall m, x: U, b: bool :: sb(wb(m,x,b),x) == b);
axiom (forall m, x, y: U, b: bool ::
  x != y ==> sb(wb(m,x,b),y) == sb(m,y));
```

The exact core of the opaque-ISet alternative is:

```boogie
type ISet;
function ISet#IsMember(ISet,Box): bool;
function ISet#FromBoogieMap([Box]bool): ISet;
function ISet#Equal(ISet,ISet): bool;
axiom (forall a: [Box]bool, x: Box ::
  { ISet#IsMember(ISet#FromBoogieMap(a),x) }
  ISet#IsMember(ISet#FromBoogieMap(a),x) <==> a[x]);
axiom (forall a, b: ISet ::
  { ISet#Equal(a,b) }
  ISet#Equal(a,b) <==>
    (forall x: Box :: ISet#IsMember(a,x) <==> ISet#IsMember(b,x)));
axiom (forall a, b: ISet ::
  { ISet#Equal(a,b) }
  ISet#Equal(a,b) ==> a == b);
```

All source ISet observations must then use the membership function. IMap's domain becomes opaque ISet too: Glue either takes that ISet directly or converts its raw map argument with the adapter. In the latter case replace `Domain(Glue(a,b,t)) == a` by `Domain(Glue(a,b,t)) == ISet#FromBoogieMap(a)`. No inverse from the adapter to raw map identity may be asserted. This is a coherent alternative design, not a complete patch in the displayed excerpt.

An existing `$Is(s,TISet(t))` guard is insufficient: all-false decoded maps satisfy its element-type condition vacuously. A new primitive *representation* predicate could select canonical maps, but would need certified introduction at every display, comprehension, function result, IMap domain/value/item operation and map lambda. That is considerably more than adding a guard to extensionality.

An exact source-level negative control for the ISet context is:

```dafny
lemma isetNegative() {
  var s: iset<int> := iset { 0, 1, 2 };
  var t: iset<int> := iset { 2, 1, 0 };
  assert s == t;
  assert false;
}
```

The equality should verify and the final assertion must fail under a sound encoding. Its pinned default verdict has not been measured here; it is a trigger/context control, not a claimed default false-acceptance exploit.

### 6.3 Expected changes and recommendation

**Recommend the guarded character repair plus the canonical-payload weakening as the smallest first repairs of the demonstrated defects, and a certificate gate that does not claim whole-theory soundness until its remaining obligations are proved.** Retain the native-component map design as the preferred structural fallback if the generic guarded support axiom becomes difficult to certify. Keep monomorphic or fully guarded verification as the already-understood fallback direction.

These local repairs preserve the intended operations on well-typed source values. They necessarily change the logical consequences of the inconsistent erased theories: `false` must cease to follow. They do not prove that every resource-limited solver verdict will stay unchanged. A proof-schedule-dependent success can change when a guard or target signature changes, even if the asserted source theorem remains true.

No language surface syntax change is proposed. Plain char and iset meanings should remain unchanged. Generated formulas change at the specified prelude/Boogie locations. A future strict checker also needs a documented uncertified/fallback result; it cannot silently return “verified” on unsupported formulas. The repository's required comparison/migration gates remain necessary. This research patch changes documentation only and makes no default-encoding change.

## 7. Checker specification

### 7.1 Placement and input

Use two stages, with the final stage authoritative:

1. **After Dafny translation:** inspect the resolved Boogie AST, including function definitions, `uses` axioms, constants/uniqueness, procedure specifications, where-clauses, local assumptions, and lambda expressions. Preserve source spans and generator-family provenance. Recognize the exact guard-closure obligations and report known blockers early.
2. **At Boogie's erasure boundary:** after lambda lowering and VC construction, retain the original typed counterexample formula. Collect the complete actual erased formula plus every support axiom added by type erasure. Run the certificate comparison of §3.3 after those additions and before giving a verification verdict. A check that runs only before map axiom generation misses the Boolean-map defect.

Configured native sorts, arithmetic mode, character mode, Boogie/Dafny source identities, preprocessing switches and pruning decisions are part of the certificate identity. A proof for one retained fragment does not certify a different pruned VC. In incremental use, cache only normalized formulas plus their full dependency/support-axiom fingerprints.

### 7.2 Checks and diagnostics

| Check | Required inspection | Report |
|---|---|---|
| Finite raw carrier | Unconditional inverse/projection plus finite native range, together with a lower-bound witness in the merged component | `incompatible carrier`: show the bounding axioms and injection/distinct constants. This is a sufficient detected conflict, not a complete finiteness decision procedure. |
| Native map payload | Result type tag, raw payload binder sort, row axiom, native decoder, relevant extensionality/retraction | `incompatible native map`: show the three-law contradiction or require canonical-store/native-component certificate. |
| Representation guards | Positive/negative quantifier polarity, inputs/results, nonempty regions, producers and consumer paths | `uncertified representation`: identify the missing guard or closure rule. Do not accept `$Is` by its name. |
| Character refinements | Parent membership retained in every redirecting-type axiom and checked-introduction path | `missing parent membership`: report source type and emitted axiom. |
| Generic and map machinery | Explicit type codes, native instances, AtLayer, cast normalization, map lambdas and support schemas | `unsupported encoding schema`: identify the exact unmatched AST. |
| Trust boundaries | User axioms, bodyless specifications, `assume`, `:\| assume` | Record as source assumptions. Do not call source inconsistency an erasure bug. |
| Whole-VC comparison | Every actual target conjunct and its reference/certificate derivation | Accept only after all target constraints are accounted for; report unmatched constraints otherwise. |

Minimal certificate record:

```text
source VC and dependency fingerprint
target VC and support-axiom fingerprint
native-sort/character/preprocessing configuration
representation-symbol correspondence
ordered derivation DAG: reference conjunct, normalization, instance, definition, weakening
for each target conjunct: checked derivation node
```

The checker kernel typechecks every certificate term, verifies capture-avoiding substitutions, checks the direction of weakening, and compares type-code identities structurally. Provenance helps diagnostics but supplies no logical authority. No certificate node may simply say “Dafny prelude”, “rank axiom”, or “generated by trusted family”.

### 7.3 How conservative is it?

The completed static coverage is **291 explicit axioms in 46 prelude rows** and **25 translator/VC family rows**. The source-range partition was independently checked to assign each explicit prelude axiom exactly once, including `uses` axioms and all conditional arithmetic branches. This is audit coverage, not a solver benchmark or a count of complete proofs.

There are **two demonstrated erasure failures**, plus the character-refinement closure obligation created by the character repair. There are **zero complete whole-family certificates for the 25 translator families supplied by an implemented checker in this work**. The checker is specified, not implemented; an empirical rejection rate would therefore be invented.

The tables make the expected conservatism concrete. A simplistic “every erased universal must have `$Is`” rule would reject at least six ordinary families: type-constructor projections, datatype constructor projections, rank laws, fuel equations, raw handle selectors and codatatype administrative laws. That is at least 6/25 family rows outside that simplistic rule, **not a measured 24% false-positive rate**. Each row includes many formulas and dependencies, and some uncaught interactions could be genuine bugs. Native-only VCs, definition instances eliminated by the checked rules, and examples such as §3.4 are eligible; acceptance remains a check of the complete VC and support theory. An acyclic definition containing erased-domain quantifiers is not automatically accepted merely because it is a definition.

The intended failure status is **uncertified**, not **unsound**, unless the checker actually supplies an inconsistency/countermodel witness. A family-specific model proof can later justify a new small certificate rule and reduce false rejections. Its soundness argument must cover interactions with the other active schemas; proving each family separately satisfiable is insufficient.

### 7.4 Checks actually performed

The supplied `proofs/iset-proof-obligations.smt2` decomposes the map contradiction into four obligations:

1. Equal decoded payloads imply equal decoded reads at an arbitrary index of the two same-key stores.
2. Universal closure of those read equalities plus extensionality equates the maps.
3. Equality of the maps plus raw same-index rows equates the raw payloads.
4. The resulting decoder injectivity contradicts three distinct `U` values.

Each query negates its claimed consequence and checks unsatisfiability. Arbitrary constants in the first obligation justify universal closure; later queries explicitly take only the previously established consequence as an additional premise. With **Z3 4.12.1**, the observed answers were **`unsat`, `unsat`, `unsat`, `unsat`**. This validates the logical derivation, not the entire Dafny/Boogie pipeline or its translation into the kernel.

The unassisted quantified kernel `proofs/iset-erased-core.smt2` gave **`unknown`** with a 15,000 ms timeout under both Z3 4.12.1 and Z3 5.1.0. It is not an observed default verifier exploit. No pinned end-to-end Boogie/Dafny verification run or repaired regression campaign is claimed. The standalone source witnesses, repair schemas and controls are exact text for the toolchain worker to execute.

## 8. The next question, answered as far as the evidence permits

**Can the two defects be fixed while keeping the arguments encoding? Yes, locally.** Guarding the character representation and weakening raw native-result map rows removes the two demonstrated obstructions; neither requires a wholesale switch to monomorphic encoding. Explicit local models establish that those repaired families can coexist with an infinite `U`.

**Does that certify all repaired Dafny output? No.** The next substantive proof is a common model-extension or certificate theorem for the repaired collection/boxing/handle/comprehension/rank families together, for every admitted negated VC, with fixed native theories. ISet image sets and generated comprehensions are particularly important: the finite/cofinite local witness is not closed under arbitrary function images or arbitrary definitions. The source restrictions on typed comprehension and recursive type cardinalities must supply the missing closure argument. An absence of further obvious contradictions would not settle it.

To preserve the exact existing solver input, a different route is to validate the solver's proof as a typed proof of the original VC. That could keep every proof whose typed reconstruction succeeds, while refusing conclusions that depend on ill-typed instances. It is not an axiom-only AST check and would require a proof-format reconstruction project. It should not be confused with merely disabling MBQI or trusting trigger annotations.

### Required closing assessment

1. **What is settled:** the complete pinned `:a` theory is semantically inconsistent for two independent reasons. The reference-guard comparison condition is sufficient for encoding soundness; the pinned output does not satisfy it. This report does not establish a complete soundness theorem for the locally repaired output.
2. **What is expected to change:** character axioms and two char-refinement optimizations; at least the native-result map support axiom, or the alternative map representation. Well-typed source meanings are intended to stay fixed. Exact solver verdict/resource preservation remains a measured gate, not a theorem delivered here.
3. **The next question:** can the jointly repaired collection/boxing/handle/comprehension theory preserve every admitted countermodel? The local repairs alone do not answer it; the specific remaining closure obligations are identified above.
4. **Feedback on the request:** the source pins, priority on proof, and demand for a checkable condition were useful. The crucial additions are to include the negated VC and all encoder-generated axioms, to distinguish semantic soundness from a restricted prover protocol, and to separate unchanged finite-corpus verdicts from unchanged logical consequences. “No axiom forces a finite sort” is too weak even without the concrete bugs.
5. **Why stopped:** the requested review, audits, local counterexamples/repairs and checker specification are delivered; the positive blanket theorem is refuted. The first unresolved substantive item is item 1's whole-theory countermodel proof after repairs. A compact pinned-default Dafny false-acceptance reproducer and a measured verdict-preservation campaign are also not completed.
