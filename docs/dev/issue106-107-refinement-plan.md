# Renaming through refinements, and copying imports: a semantic plan

This is a plan, and only a plan, for two enhancement requests:

- [issue 106](https://github.com/erniecohen/dafny/issues/106), allow renaming
  through refinements;
- [issue 107](https://github.com/erniecohen/dafny/issues/107), copying imports
  that preserve refinability.

Nothing here is implemented.  Every claim about the current code cites
`review/4.11.0` at commit `1920c844b`, and every claim about current behaviour
comes from a small program in section 9, run against a build whose parser,
resolver and refinement sources are identical to that commit, with Z3 5.1.0,
one core and a resource limit of 20,000,000.

The plan found one existing soundness hole that both features depend on, also open upstream: a
refinement may replace an abstract import with a module that does not refine it,
and it then inherits, without checking them again, proofs that no longer hold.
Program E08 in section 9 proves `false` this way under default options.  Section 4
treats it, and stage 0 of section 8 fixes it, as a soundness fix of its own.

## 1. The two requests in brief

| | Issue 106: renaming | Issue 107: copying imports |
|---|---|---|
| What is asked | A refinement may choose new names for things the base named. | `import X := Q` gives the importer its own copy of `Q`, whose declarations a refinement of the importer may still refine. |
| What the plan offers | A refinement may rename what only its own declaration can see: type parameters, out-parameters, a function's result, and the local name of an in-parameter.  What clients can see keeps its name: declarations, members, constructors, destructors, iterator parameters, and the label of an in-parameter used by named arguments.  Signature agreement compares types, not their spelling, so a type can be given a new name by a synonym. | `import X := Q` declares a nested module `X` that refines `Q` and adds nothing, keeps `Q`'s default export, and may be replaced in a refinement of the importer by `import X := Q2` or `import X = Q2`, where `Q2` refines `Q`. |
| What is ill-posed | Removing a name that clients can see.  Every client written against the base is re-resolved against each refinement, so a refinement that removes a visible name is not a substitute for its base.  The closest sound alternative is an added name (a synonym, a label that stays), shown in section 2.4. | A copy "as deep as one likes".  The plan copies exactly what refinement copies; a module that wants a deeper copy says so with copying imports of its own (section 3.3). |
| Option | `--refinement-renaming`, off by default | `--copy-imports`, off by default |
| Soundness argument | Renaming is alpha-conversion of each refined declaration; every obligation is the same proposition before and after (section 2.6). | A copy is a refinement, and replacing it is a refinement step, so the existing argument for refinement applies, provided the replacement is checked to be a refinement (section 3.6), which today it is not for abstract imports (section 4). |

## 2. Issue 106: renaming through refinements

### 2.1 What a refinement does with names today

A refining module is built syntactically.  `RefinementTransformer` matches each
declaration of the base with a declaration of the refinement by name
([RefinementTransformer.cs:219][rt219], and for members
[:668-681][rt668]), and the merged declaration takes the *base's* signature: a
refined function is rebuilt from `previousFunction.Ins`, its type parameters and
its result ([:449-458][rt449]), a refined method from `previousMethod.Ins` and
its outs ([:518-524][rt518]).  The text the refinement adds (a body, more
`ensures`) is then resolved in the scope of the base's formals.  That is why the
issue's program reports two errors: the name check of
[:945][rt945] (`there is a difference in name of parameter 0`), and
`unresolved identifier: y`, because the merged function has a formal `x` and a
body that says `y` (E01).

The same rule is applied to every kind of name a declaration binds:

| name | where it is checked | program |
|---|---|---|
| in-parameter of a function, method, lemma or constructor | [:945][rt945], called from [:766][rt766] and [:831][rt831] | E01 |
| out-parameter of a method or lemma | [:945][rt945], called from [:832][rt832] | E11 |
| result name of a function | [:767-769][rt767] | E11 |
| type parameter | [:866][rt866] | E11 |
| type of a parameter or result | `TypesAreSyntacticallyEqual`, which compares printed strings ([:967-971][rt967]), at [:959][rt959] and [:770][rt770] | E12 |
| iterator in- and yield-parameters | [:599-600][rt599] | — |

### 2.2 Who can see each name

The question that decides what may be renamed is who can observe a name.  A
refinement must be a substitute for its base in every client, and clients are
re-resolved against refinements: a client with `import I : A` that is refined by
one with `import I = B` gets its inherited bodies cloned and resolved again
against `B`.  E02 shows a named-argument call `I.F(y := 1, x := 2)`, written
against `A`, verified against `B` after that replacement.

| name | seen by | kind |
|---|---|---|
| top-level declarations, members, datatype constructors and destructors | every client | external |
| an in-parameter's name | the declaration's own text, *and* every named-argument call, since the resolver keys named arguments by `Formal.Name` ([PreTypeResolve.ActualParameters.cs:70][ap70], [NameResolutionAndTypeInference.cs:3306][nr3306]), and `nameonly` parameters can be passed only by name ([PreTypeResolve.ActualParameters.cs:95][ap95]) | both |
| iterator in- and yield-parameters | clients, as fields of the iterator object (`it.n`, `it.k`, E22) | external |
| an out-parameter's name, a function's result name | the declaration's own specification and body | internal |
| a type parameter's name | the declaration's own text; type arguments are positional | internal |

### 2.3 Semantics

Under `--refinement-renaming`, a refining declaration may give new names to the
**internal** names of the declaration it refines, and only to those:

1. **Type parameters** of a function, method, lemma, iterator, class, trait,
   datatype, newtype or abstract type, renamed positionally: the *i*-th type
   parameter of the refinement is the *i*-th of the base.  Every other check of
   [:866-905][rt866] (characteristics, variance) is unchanged.
2. **Out-parameters** of a method or lemma, and the **result name** of a
   function.
3. The **local name** of an in-parameter of a function, method, lemma or
   constructor.

The meaning is alpha-conversion.  The merged declaration is the base's
declaration with each renamed binder replaced by its new name, everywhere the
base's text uses it: `requires`, `ensures`, `reads`, `modifies`, `decreases`,
default-value expressions of later parameters (E21 shows these are inherited),
the body, a `by method` body, and, for a type, every member that mentions a
renamed type parameter.  The refinement's own text is then resolved in the new
names, as today it is resolved in the old ones.

Method bodies are merged statement by statement against a skeleton
([:1037-1212][rt1037]).  The skeleton is written in the new names, so the base's
body is renamed first and merged second.

**Labels stay.**  An in-parameter has a *label*, the name a named argument uses,
and a *local name*, the name its declaration's text uses.  Today they are one
name.  Under the option the label of a parameter is fixed by the first
declaration in its refinement chain and never changes; only the local name may.
A call `F(x := 1)` written against the base keeps working against every
refinement, as substitution requires.  A call `F(y := 1)` in the refinement,
where `y` is only the local name, is an error that says so: "the parameter
labelled 'x' is called 'y' inside `B.F`; a named argument uses its label 'x'".

**Iterators** are excluded: their parameters are fields, so they are external.
**Constructors** of classes follow methods: `new C(x := 1)` uses the label.

### 2.4 Renaming a type, and why removing a name is ill-posed

A visible name cannot be removed by a refinement, because clients written
against the base use it (section 2.2).  What a refinement can do is add a name,
and make the new name usable everywhere the old one was:

- An **abstract type** `type T` without members can already be refined by a
  synonym to a new type, `datatype Node = ...` with `type T = Node`
  ([:346-352][rt358]); one with members can be, under the fork's
  `--synonym-refines-abstract-type` ([:326-345][rt326]; issue
  [91](https://github.com/erniecohen/dafny/issues/91)).
- A **concrete type** `T` can be given a second name today, `type Node = T`
  (E12, module `B2`).
- What blocks using the new name is signature agreement, which compares the
  printed spelling of types ([:967-971][rt967]): `function F(t: Node)` does not
  refine `function F(t: T)` even when `T = Node` (E12, module `B`).

So the plan's type renaming is one change: under the option, the parameter and
result types of a refining signature are compared after resolution, as types,
with synonyms expanded and subset types and newtypes kept
(`NormalizeExpandKeepConstraints`), modulo the type-parameter renaming of
section 2.3.  The check moves to the post-resolution tasks that already exist
for the characteristic check of abstract types ([:322-332][rt322],
[:437-447][rt437]).  The merged declaration keeps the base's types, as it does
today, so nothing downstream sees the new spelling.

Hiding the old name from clients is not part of this plan.  A refinement's
export set cannot reveal `Node` without `T` today (E20: "This export set is not
consistent"), and making it able to is a question about export consistency,
not about refinement.

### 2.5 Collisions

Alpha-conversion is sound only when it captures nothing.  The rule is to reject,
not to rename further:

- **New name already used in inherited text.**  If a new local name `y` occurs
  in the base's text (in any clause listed in 2.3) and is resolved there to
  something other than the renamed binder (a field, a constant, a member, a
  module, another formal, a type), the rename is an error that names that
  occurrence.  The base is resolved before the refinement is built, so each
  occurrence's resolution is known; the check is exact, not syntactic.
- **Binder in inherited text.**  If the base's text binds the new name `y`
  (a quantifier, a `var`, a `match` pattern, a lambda) around a use of the old
  name `x`, renaming would capture it; error.
- **Two binders, one name.**  Two parameters, two type parameters, or an in- and
  an out-parameter renamed to the same name: error, as for any declaration.
- **Permutations are allowed.**  `F(x, y)` refined as `F(y, x)` renames
  simultaneously and is accepted; the labels stay `x` and `y` in their
  original positions, so `F(y := 1)` still sets the *second* parameter.  This
  is the case that rules out letting both the old and the new name serve as
  labels (open question 1).
- **Type parameters.**  A type parameter renamed to the name of a type that the
  base's text uses (`G<T>` renamed `G<Node>` where the body mentions the
  module's `Node`) is the first bullet for types.
- **Chains.**  `A` names `x`, `B refines A` renames it `y`, `C refines B`
  renames it `z`.  `C` is built from `B`'s merged declaration, which already
  says `y`, so each step checks collisions only against its base's text.  The
  label stays `x` throughout.  Renaming back to an earlier name is an ordinary
  rename.

### 2.6 Why it is sound

A renamed declaration is alpha-equivalent to the declaration that would have
been built had the base used the new names.  Every obligation the verifier emits
for it is the same proposition up to the names of bound Boogie variables, so:

- an obligation proved in the base and inherited unchanged is still an
  assumption in the refinement ([BoogieGenerator.cs:3713-3718][bg3713]), and it
  is still true, because it is the same proposition;
- an obligation the refinement adds is checked, as today;
- named-argument calls in clients bind the same positions as before, because
  labels do not change, so a client's re-resolution against the refinement
  produces the same calls.

There is nothing to trust beyond the capture check.  The rename touches no
axiom, no visibility, and no export set.  It changes the names in the generated
Boogie of the refined declaration only, so resource counts of that declaration
may move, as they do for any change of generated names (compare the note of
[issue 392](https://github.com/erniecohen/dafny/issues/392)).  With the option
off, the code path is not taken.

## 3. Issue 107: copying imports

### 3.1 What exists today

Dafny has three ways for a module `M` to name another module `Q`
([Dafny.atg:367-393][atg367]):

- `import X = Q` (and `import Q`): an **alias**
  ([AliasModuleDecl.cs:13][alias13]).  `X.T` is `Q.T`.  A refinement of `M`
  cannot replace it (E05: "you must use the refining notation"; the path is
  [RefinementTransformer.cs:224-235][rt224] and [:300-302][rt300]).
- `import X : Q`: an **abstract import**
  ([AbstractModuleDecl.cs:11][abs11]).  `M` then must be abstract
  ([ModuleResolver.cs:1205][mr1205]; E05), and a refinement of `M` replaces
  it, by any module named `X` ([:229-232][rt229]; E04).
- `module X refines Q {}` nested in `M`: a **nested refinement**.  Its types are
  new: `X.T` is not `Q.T`, and `X.D` is not `Q.D` (E10), while synonyms stay
  transparent (E17).  But a refinement of `M` cannot refine `X` (E03), and `X`
  does not keep `Q`'s default export ([:1700-1712][rt1700]; E23, E24).

Issue 107 asks for a fourth: a concrete, usable copy of `Q`, like a nested
refinement, that a refinement of `M` can still refine, like an abstract import.

### 3.2 Syntax and meaning

Under `--copy-imports`:

```
import [opened] X := Q[`E]
```

declares in `M` a module `X` such that:

1. **`X` refines `Q` and adds nothing.**  `X` contains a fresh copy of each of
   `Q`'s own type and member declarations, made by the refinement cloner
   ([:217-221][rt217]).  Each copied type is a new type: `X.T` is not `Q.T`.
2. **`X` keeps `Q`'s export sets, with `Q`'s default export as its own
   default.**  This differs from the clone rule for an ordinary refinement,
   which renames the default away ([:1700-1712][rt1700]; E24).  A copy is meant
   to be imported as `Q` is.
3. **`M` sees `X` through the export set `E`** if one is given, and through
   `X`'s default export otherwise.  The copy itself is always all of `Q`: the
   functions `E` only provides still have their bodies, so `X` compiles and
   verifies as `Q` does.
4. **Abstractness follows.**  If `Q` is abstract, `X` is abstract, and `M` must
   be declared abstract, with the same message as an abstract import of `Q`
   ([ModuleResolver.cs:1205][mr1205]).  A refinement of `M` makes it concrete
   by replacing `X` (3.4).  If `Q` is concrete, so are `X` and `M`, and `X` is
   compiled as its own module, as a nested refinement is (E19).
5. **`X` is refinable in refinements of `M`** (3.4).

`Q` is named and resolved exactly as the target of `import X = Q` is.  `opened`
means what it means for other imports.

### 3.3 How deep the copy is

A copy is a refinement, so it is exactly as deep as refinement:

- **`Q`'s own types and members are copied.**  Datatypes, codatatypes, classes,
  traits, newtypes, subset types, abstract types, iterators and the default
  class get new identities.  Type synonyms are transparent, so a copied
  `type U = int` is still `int` (E17).
- **`Q`'s module declarations are shared, not copied.**  Refinement clones a
  module declaration by pointing the clone at the same signature
  ([:1712-1716][rt1700]): a nested module `N` of `Q` is the same module in every
  refinement of `Q` (E09, `B.N.D` is `A.N.D`).  The same holds for `Q`'s
  imports, which refinement merges ([:241-247][rt241]).  So an alias import
  in `Q` stays shared, an abstract import in `Q` stays abstract in `X` (so `X`
  and `M` are abstract), and a copying import in `Q` is shared between `Q` and
  `X`.
- **A deeper copy is requested where it is wanted.**  If `Q` copies its own
  dependencies (`import Y := R`), a refinement `Q2` of `Q` can replace `Y`, and
  `M`'s refinement gets that by replacing `X` with a copy of `Q2`.

The rule needs no new notion of depth: what a refinement may change in `Q`, a
refinement of `M` may change in `X`, through `Q`'s refinements.  This answers the
issue's question "how deep the copy is" without a parameter.  A deep copy of
`Q`'s nested modules would be a second notion of copying, with its own identity
rules for the shared modules that a nested module imports; open question 4.

Nested modules cannot see the declarations of their enclosing module (the
resolver says so: "names in outer modules are not visible in contained
modules", E10), so a shared nested module never mentions a copied type, and
sharing it is consistent.

### 3.4 Refining a copy

In `module M' refines M`, the inherited `X` may be replaced by a declaration
named `X` of one of two forms:

- `import X := Q2[`E2]`: a copy of `Q2`, still refinable in refinements of `M'`;
- `import X = Q2[`E2]`: `Q2` itself, final.  `X`'s types are then `Q2`'s, and
  equal to those of any other module that uses `Q2`.  This is how two
  components are made to agree on a type.

Each is accepted only if:

1. **`Q2` refines the module that `X` copies**, reflexively and transitively,
   checked by walking `Q2`'s refinement chain as `CheckIsRefinement` does
   ([:414-435][rt414]).  `X` adds nothing to `Q`, so refining `X` and refining
   `Q` are the same condition.
2. **The view does not shrink**: the names of `E2` include those of `E`, as for
   abstract imports today ([:427-430][rt414]).
3. **`opened` matches**, as for every import a refinement redeclares
   ([:170-183][rt170]).

Anything else named `X` in `M'` (a literal module, an abstract import, another
kind of declaration) is an error.  An inherited copy that `M'` does not
redeclare stays the same copy: `M'.X` is `M.X`, by the sharing rule of 3.3.

The plan does not add syntax for refining `X` in place (`module X ... { }`).
`Q2` is written as an ordinary module refining `Q`, where it can be verified,
named and reused (open question 3).

### 3.5 Type identity, at the edges

- Two copies of `Q`, in one importer or in two, are different modules with
  different types.
- A copy and its original have different types; a value of `Q.T` is not an
  `X.T` (E10).
- After `M'` replaces a copy by an alias, `M'.X.T` is `Q2.T`.  Code inherited
  from `M` that was resolved against `X.T` is re-resolved against `Q2.T`, as
  inherited code is re-resolved against any replacement today.
- A copy of a module that declares `{:extern}` names would declare them twice in
  the compiled program.  Copying a module with an extern name is an error.
- A `replaceable` module cannot be refined ([:155-160][rt155]), so it cannot be
  copied.  A module cannot copy a module that encloses it, or itself: that is a
  cycle in the module graph, reported as refinement cycles are.

### 3.6 Why it is sound

The argument is the existing argument for refinement, applied twice.

- **The copy.**  `X` refines `Q` and adds nothing.  Every obligation in `X`
  was proved in `Q` and is inherited unchanged, so the translator assumes it
  ([BoogieGenerator.cs:3713-3718][bg3713]).  That is sound because it is the
  same proposition, about the copied declarations, that `Q` proved about its
  own.
- **The replacement.**  `M` was verified against `X`, which has exactly `Q`'s
  content.  A refinement can only make abstract things concrete: give bodies to
  bodiless functions and methods, define abstract types, add members and
  declarations, add postconditions that it proves
  ([:736-783][rt736], [:786-845][rt786]).  So every fact `M` proved about `X`
  holds of every refinement of `Q`, in particular of `Q2`.  `M'` inherits
  `M`'s obligations as assumptions, which is sound exactly when the
  replacement is a refinement.

The whole argument rests on condition 1 of 3.4.  Today that condition is not
checked for abstract imports, and the hole is real (section 4).  For copies the
check is new code and is required from the first stage that accepts a
replacement.

The export view adds nothing to trust: `M` sees less than all of `X`, so it
proves less, and condition 2 of 3.4 keeps what it saw available.

## 4. The existing hole: replacing an abstract import with a non-refinement

When a refinement redeclares a name that the base declared as an abstract
import, `RefinementTransformer` records the correspondence and checks nothing
else ([:229-232][rt229]).  The check that the new module refines the old one,
`CheckIsRefinement` ([:414-435][rt414]), is reached only through
`MergeTopLevelDecls` ([:300-307][rt300]), which runs only for declarations
marked refining ([:224-225][rt224]), and an import cannot be.  Meanwhile the
inherited proofs that used the abstract import are assumed, not re-proved
([BoogieGenerator.cs:3713-3718][bg3713]).

So this verifies under default options (E08), both resolvers, and in the
released 4.11.0 build of this fork:

```dafny
abstract module A {
  function F(): int
  lemma L() ensures F() > 0
}
module Other {
  function F(): int { 0 }
  lemma L() ensures true { }
}
abstract module B {
  import X : A
  lemma P() ensures X.F() > 0 { X.L(); }
}
module B3 refines B {
  import X = Other
  lemma Zero() ensures X.F() == 0 { }
  lemma Bad() ensures false { P(); Zero(); }
}
```

E06 does the same with a literal module in place of the alias.  Upstream has the
compiled form of the same hole open as an issue (a division by zero at run
time).

This is a soundness bug, not part of either enhancement.  It should be filed
here, and fixed by default with a hidden `--legacy-unchecked-import-replacement`
that restores today's behaviour, as `AGENTS.md` asks of a soundness fix.  The
fix only rejects programs: a replacement of an abstract import by a module that
does not refine the imported module (with the export-name condition of 3.4,
item 2) becomes an error.  It is stage 0 of section 8 because copies are
replaced through the same path, and because the member-invariants plan leans on
it too (section 6).

## 5. Interaction with module-level caching and resolution keys

The content-addressed caches of
[issue 261](https://github.com/erniecohen/dafny/issues/261) (a sub-issue of
[issue 241](https://github.com/erniecohen/dafny/issues/241)) key each module's
resolution by its source, its prerequisites' keys, the options and the version,
and derive generated names from that key
([261-content-addressed-caches.md][d261]).

**Copies.**

- A copying import is a **refinement edge**, not an import edge.  The design's
  prerequisite table gives a refiner "the actual resolved base root", because
  "a refiner can read/clone the base's implementation"
  ([261 design, prerequisite table][d261-pre]), and its mutation inventory
  keeps "full readable base is a prerequisite" for refinement-base cloning
  (R21, [261 design][d261-r21]; [241-front-end.md:435][d241fe]).  A copy reads
  `Q`'s bodies, so `X`'s key includes `Q`'s whole key, not `Q`'s export view.
- `X` is its **own vertex**, a nested literal module of `M` in the table's
  terms.  `M` depends on `X` through the view `E`.  So an edit to a body in `Q`
  invalidates `X`, and the later observable-view cutoff
  ([261 design, observable views][d261-v]) can stop it at `X` when `E`'s view
  is unchanged.
- **Generated names** of two copies of one module must differ.  They do: the
  copies have different full names (`M.X`, `N.X`), and since
  [issue 392](https://github.com/erniecohen/dafny/issues/392) a module's
  generated-name prefix is a hash of its full name.  A test pins two copies
  named `X` in different importers.
- **Replacement facts.**  The replacement relation is recorded where today's is
  (`CardinalityRefinementBase`, [:232][rt229], [:411][rt411]); the 261 design
  places replacement facts in the global projection `G` only when translation
  reads them (R19).  No new kind of key is needed.

**Renaming.**

- A rename is in the refining module's own source, which its key covers.
- Labels belong to the observable view of a declaration (the view table lists
  "formal default values" and signatures).  Under renaming, the view must hash
  labels, which do not change, and local names only where a revealed body or
  specification shows them.  An internal rename then changes a dependent's
  view only through revealed text.  Hashing revealed text up to alpha-equivalence
  would remove even that; it is an optimisation for the 261 work, not a
  requirement of soundness.
- A rename changes the generated names of the renamed declaration only.

**Options.**  Both options change what resolves, so both join the list of
resolver options that a cache key and a library check must see: they are listed
where `--synonym-refines-abstract-type` is
([DafnyCommands.cs:128][dc128], [ModularTerminationRecord.cs:748][mtr748]) and
get rows in the option audit ([261-option-audit.tsv][d261-audit]).

## 6. Interaction with member invariants and cross-module recursion

### 6.1 Member invariants

The member-invariants plan is in draft
[pull request 384](https://github.com/erniecohen/dafny/pull/384)
(`docs/dev/issue105-member-invariants-plan.md`).  Its section 4.2 adds
invariant conjuncts in refinements, assumes inherited conjuncts at inherited
sites, and asserts added conjuncts there.

- **That plan depends on section 4 of this one.**  Its section 4.2 says that a
  module with `import A : M0` refined by `import A = M1` gets `M1`'s added
  conjuncts as obligations at its inherited conversion sites.  If the
  replacement need not refine `M0` (section 4), it may drop `M0`'s invariant,
  while the inherited sites keep assuming it.  The stage-0 fix is a
  prerequisite of that plan's stage 5 as well as of this plan's stage 5.
- **Copies.**  `X.T` copies `Q.T`'s invariant conjuncts and its raw type
  `raw X.T` is new.  The copy adds no conjunct, so it adds no obligation.
  When `M'` replaces `X` by a copy of `Q2`, and `Q2` adds conjuncts, `M`'s
  inherited conversion sites to `X.T` get them as obligations: the same rule as
  for an abstract import, with a copy in place of `import A : M0`.  The
  member-invariants implementation must treat a replaced copy as it treats a
  replaced abstract import.
- **Renaming.**  An invariant is a member, so renamed type parameters are
  substituted in it (2.3).  An invariant binds no parameters.
- **Renaming a type by a synonym.**  The member-invariants plan rejects a type
  synonym refining an abstract type that has an invariant (its section 4.3).
  Section 2.4 here does not change that.  If that plan later accepts the
  `--synonym-refines-abstract-type` form, the merged target type carries the
  invariant, and type renaming composes with it.

### 6.2 Cross-module recursion

The design for cross-module recursion between types with invariants is being
written now, and has no document yet.  This plan fixes what it must keep, in
terms of the module graph:

- **A copy is never recursive with its original.**  A copied type's text
  names what the original's text names.  If `Q.T` reaches itself through `R.U`
  (`Q.T -> R.U -> Q.T`), then `X.T -> R.U -> Q.T`: the copy reaches the
  original's group, not itself.  A copy's well-foundedness follows from that of
  the group it points into, and the recursion check must treat a copy as a new
  declaration whose edges are its cloned text.
- **A copy of a module in a module-level cycle is ill-posed.**  If that design
  admits cycles between modules (each naming the other's types), then copying
  one member of a cycle gives a module whose partner still names the original.
  The closest sound alternative is to reject `import X := Q` when `Q` lies on
  such a cycle; copying a whole cycle at once would need a syntax for it, and is
  not proposed.
- **Renaming** changes no edge of the module graph.

## 7. Options

Both features are new, so both are opt-in, and with each option off the output
is byte-identical to today's.

| option | default | what it allows | what changes when off |
|---|---|---|---|
| `--refinement-renaming` | off | 2.3, 2.4 and 2.5 | nothing: the name checks of 2.1 run as today |
| `--copy-imports` | off | 3.2 to 3.5 | nothing: `import X := Q` is the parse error it is today ("rbrace expected") |
| `--legacy-unchecked-import-replacement` (hidden) | off | today's unchecked replacement of an abstract import | the soundness fix of section 4 is on |

The two features are independent: neither option needs the other.  A program
that uses both renames inside a copy's refinement, which is an ordinary
refinement, so nothing more is needed.  The friendlier diagnostic "use
`--copy-imports`" for the parse error would change an existing message, and can
be a separate change.

## 8. Implementation, in stages

Each stage is one pull request, with its tests, a release-note fragment, and the
Reference Manual and `--help` text when it adds an option.

| stage | what | main files |
|---|---|---|
| 0 | **Soundness fix (section 4)**: a replacement of an abstract import must refine it; the hidden legacy option. Its own issue. | `RefinementTransformer.cs` (the branch at [:229-232][rt229]), `CommonOptionBag.cs` |
| 1 | **Internal renames**: type parameters, out-parameters, result names; the renaming clone; the capture check. | `RefinementTransformer.cs` ([:449-575][rt449], [:856-905][rt866], [:928-964][rt928]), the cloner |
| 2 | **Labels**: a formal's label separate from its local name; named-argument resolution by label in both resolvers; the error for a local name used as a label. | `Formal`, [PreTypeResolve.ActualParameters.cs:70][ap70], [NameResolutionAndTypeInference.cs:3306][nr3306] |
| 3 | **Type agreement by type**, after resolution (2.4). | `RefinementTransformer.cs` ([:967-971][rt967] and its callers) |
| 4 | **Copying imports**: grammar (`:=`, the token `gets` of [Dafny.atg:135][atg135], in [ModuleImport][atg367]); a module declaration for copies; the nested refinement with the default export kept; abstractness; the extern, `replaceable` and cycle errors. | `Dafny.atg`, `AST/Modules/`, `ModuleResolver.cs`, `RefinementTransformer.cs` ([:1696-1717][rt1700]) |
| 5 | **Replacing copies** (3.4), through the check of stage 0. | `RefinementTransformer.cs` |
| 6 | **Cache keys** (section 5), when the 261 work reaches resolution keys; **member-invariant hooks** (6.1), when that work reaches refinement. | per those plans |

### 8.1 Tests

Each test is a lit test under `git-issues/`, run under both resolvers, with
the option on, and, where the program parses without it, with the option off to
pin today's error.

**Stage 0.**  E06, E07 and E08 are rejected by default and accepted with the
legacy option.  An accepted replacement by a refinement (E04, module `B2`), a
refinement two steps down a chain, and one with a larger export set.  A
replacement with a smaller export set is rejected.  The suite and standard
library verdicts are listed from CI; none is expected to move.

**Renaming (stages 1 to 3):**

- *Chains*: `A -> B -> C -> D`, renaming one parameter at each step and back to
  its first name at the last; a call with a named argument written in `A`'s
  client, and in each of `B`, `C`, `D`, all using the label; an inherited
  `ensures` that mentions the parameter, proved in `A` and assumed in `D`; a
  method body merged with a skeleton in renamed variables in `C`.
- *Collisions*: a new name equal to a field, a module constant, a member, a
  module name, and a type name used in the base's text; a new name bound by a
  quantifier, a `var`, a `match` case and a lambda around a use of the old name;
  two parameters renamed to one name; an in-parameter and an out-parameter
  renamed to one name; a swap `F(x, y)` to `F(y, x)`, accepted, with
  `F(y := 1)` setting the second parameter and `F(x := 1)` the first; a
  default value `y: int := x + 1` with `x` renamed (E21 extended).
- *Generics*: a function and a datatype with renamed type parameters used in
  nested types (`seq<map<T, set<T>>>`), in a member and in an `ensures`; a
  permutation of two type parameters; a type parameter renamed to a type name
  the base mentions (rejected); characteristics and variance still checked.
- *Exports*: a named-argument call through an export set that provides the
  function; a `nameonly` parameter renamed, still passed by its label; the
  abstract-import replacement of E02 with a renaming `B`.
- *Not renamable*: iterator parameters, members, constructors, destructors:
  each still rejected under the option.
- *Types*: E12 accepted under the option (`F(t: Node)` refines `F(t: T)`);
  a subset type over `T` and a newtype over `T` still rejected as different
  types.

**Copies (stages 4 and 5):**

- *Identity*: E10 with `import X := A` (both errors stay); two copies of one
  module in one importer are different; a synonym is transparent (E17).
- *Exports*: the default export kept (E23 with a copy resolves `X.F` and
  rejects `X.G`); `import X := Q`E`; a replacement with a larger export set,
  accepted; with a smaller one, rejected.
- *Chains*: `M -> M' -> M''`, replacing the copy at each step by a copy of a
  further refinement, and by an alias at the last; a copy inside a copy (`Q`
  with `import Y := R`), with `Y` replaced through `Q2`; a copy of an abstract
  module that has an abstract import, made concrete two refinements down.
- *Replacement errors*: a replacement by a module that does not refine `Q`
  (the copy form of E08, must fail); by a literal module; by an abstract import;
  with `opened` changed.
- *Generics*: a copied generic datatype and class, instantiated in `M`; a copied
  trait extended by a class of `M`, with a member added in `Q2`, so that `M'`
  must give it a body.
- *Edges*: copying a `replaceable` module, an enclosing module, a module with an
  `{:extern}` name, each rejected; a concrete copy compiled and run, with the
  copy's and the original's datatypes kept apart at run time; copying a module
  from a `.doo` library.
- *Costs*: a copy of a module with an expensive lemma costs that lemma no
  solver work in the importer (its obligations are assumed).

**Option off.**  Each feature's suite and the standard library give the same
verdicts and resource counts with its option off as before it, as `AGENTS.md`
requires.

## 9. Experiments

Each program was run with `dafny verify` (default options plus the settings
named at the top), and the plan cites it by number.  "Verifies" means
`0 errors`.

| | program, in brief | result |
|---|---|---|
| E01 | issue 106's program: `A.F(x)` refined as `B.F(y)` | two errors: name difference at the parameter; `unresolved identifier: y` |
| E02 | client of `import I : A` calls `I.F(y := 1, x := 2)`; refined with `import I = B`, a refinement of `A`; lemma uses it | verifies |
| E03 | `module X refines A {}` nested in `B`; `B2 refines B` redeclares `module X refines A {...}` | error: "you must use the refining (`...`) notation" |
| E04 | `abstract module B { import X : A }`; `B2` replaces `X` with `module X refines A {...}`; `B3` with `module X {...}` that refines nothing | both accepted, verifies |
| E05 | `module B { import X = A }` with `A` abstract; `B2 refines B { import X = A2 }` | errors: compiled module cannot import an abstract module; the refining notation is needed |
| E06 | section 4's program with a literal non-refining module in place of the alias | verifies, proving `false` |
| E07 | section 4's program without `Zero` | verifies, proving `false` |
| E08 | section 4's program | verifies, proving `false`; also with `--type-system-refresh` on and off, and with the released build |
| E09 | `module A { module N { datatype D = K } }`, `module B refines A {}`, `function f(d: A.N.D): B.N.D { d }` | verifies: nested modules are shared by refinement |
| E10 | `module X refines A {}` nested; `function g(t: A.T): X.T { t }` and the same for a datatype | errors: `expected X.T, got A.T`, `expected X.D, got A.D` |
| E11 | refinement renames an out-parameter, a result name, a type parameter | errors at each, as listed in 2.1 |
| E12 | `type T = Node` refining an abstract `T`, with `F(t: Node)`; and `type Node = T` added to a refinement of a concrete `T` | first: error, parameter type differs (`Node` instead of `T`); second: accepted |
| E14 | nested `module X refines A { type T = int }` whose base has a lemma with a proof, used by a lemma of the importer | verifies |
| E16 | `import X : A`E` replaced by `import X = A2`E` and by `import X = A2`Full` | accepted |
| E17 | `module A { type T = int }`, nested `module X refines A {}`, `function k(t: int): X.T { t }` | verifies: synonyms are transparent in a refinement |
| E19 | concrete module with nested `module X refines A {}` of an abstract `A`, translated to C# | errors: bodiless function and abstract type cannot be compiled |
| E20 | refinement exports `reveals Node, Size` where `Node = T`, without `T` | error: the export set is not consistent |
| E21 | default value `y: int := x + 1` in the base, inherited by the refinement, used by a client | verifies |
| E22 | iterator parameters read as fields `it.k`, `it.n` | verifies |
| E23 | `module Q { export provides F ... }`, nested `module X refines Q {}`, uses of `X.F` and `X.G` | errors: both unresolved, so `X` has no default export |
| E24 | the same with `X` at top level and `import X` | error: "no default export set declared in module: X" |

## 10. Open questions for the owner

1. **Labels.**  *Recommend: a label is fixed by the first declaration; only the
   local name changes.*  Letting both the old and the new name serve as labels
   is ambiguous for permutations (2.5).  A later explicit syntax for a label
   different from its local name, in any declaration, would be a separate
   feature.
2. **Capture.**  *Recommend: reject a capturing rename.*  The alternative,
   renaming the inner binders of the base's text automatically, accepts more
   programs, but changes names the user did not write and makes error messages
   about inherited text harder to read.
3. **Ways to replace a copy.**  *Recommend: `:=` and `=` only.*  A literal
   `module X refines Q2 { }` in place, and refining a copy in place without
   naming `Q2`, are left out; they would make nested literal modules refinable,
   a wider change than the issue asks for.
4. **Depth.**  *Recommend: a copy is exactly as deep as refinement.*  The
   alternative, copying nested modules too, needs identity rules for the
   modules those nested modules share.
5. **Stage 0.**  *Recommend: file the soundness bug of section 4 now, as its own
   issue, and fix it by default ahead of both features and of stage 5 of the
   member-invariants plan.*

[rt155]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L155-L160
[rt170]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L170-L183
[rt217]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L217-L221
[rt219]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L219
[rt224]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L224-L235
[rt229]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L229-L232
[rt241]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L241-L247
[rt300]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L300-L307
[rt322]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L322-L332
[rt326]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L326-L345
[rt358]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L346-L352
[rt411]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L411
[rt414]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L414-L435
[rt437]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L437-L447
[rt449]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L449-L462
[rt518]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L518-L527
[rt599]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L598-L600
[rt668]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L668-L687
[rt736]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L736-L783
[rt766]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L766
[rt767]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L767-L769
[rt770]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L770-L772
[rt786]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L786-L845
[rt831]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L831
[rt832]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L832
[rt866]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L856-L905
[rt928]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L928-L964
[rt945]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L945
[rt959]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L958-L959
[rt967]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L967-L971
[rt1037]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L1037-L1212
[rt1700]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L1696-L1717
[bg3713]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Verifier/BoogieGenerator.cs#L3713-L3718
[ap70]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Resolver/PreType/PreTypeResolve.ActualParameters.cs#L70
[ap95]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Resolver/PreType/PreTypeResolve.ActualParameters.cs#L95
[nr3306]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Resolver/NameResolutionAndTypeInference/NameResolutionAndTypeInference.cs#L3306
[atg135]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Dafny.atg#L135
[atg367]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Dafny.atg#L367-L393
[alias13]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/AST/Modules/AliasModuleDecl.cs#L13
[abs11]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/AST/Modules/AbstractModuleDecl.cs#L11
[mr1205]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Resolver/ModuleResolver.cs#L1205
[dc128]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Options/DafnyCommands.cs#L128
[mtr748]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/ModularTerminationRecord.cs#L748
[d261]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/docs/dev/design/261-content-addressed-caches.md
[d261-pre]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/docs/dev/design/261-content-addressed-caches.md#L376-L386
[d261-r21]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/docs/dev/design/261-content-addressed-caches.md#L488-L490
[d261-v]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/docs/dev/design/261-content-addressed-caches.md#L501-L512
[d261-audit]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/docs/dev/design/261/261-option-audit.tsv
[d241fe]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/docs/dev/design/241-front-end.md#L435
