# Renaming through refinements, and copying imports: a semantic plan

This is a plan, and only a plan, for two enhancement requests:

- [issue 106](https://github.com/erniecohen/dafny/issues/106), allow renaming
  through refinements;
- [issue 107](https://github.com/erniecohen/dafny/issues/107), copying imports
  that preserve refinability.

Nothing here is implemented.  Every claim about the current code cites
`review/4.11.0` at commit `1920c844b`, and every claim about current behaviour
comes from a small program in section 9.  The E programs were run against a
build whose parser, resolver and refinement sources are identical to that
commit, the others against the fork's release
`4.11.0+fcb2042d.review.bcc0c421`; all with Z3 5.1.0, under default options
and again with `--type-system-refresh`.

**Both features extend refinement, and refinement is not sound today.**  The
first version of this plan found one hole.  A second pass tried to break each
soundness claim of the plan with a program, and found four more.  In each, a
refining module assumes something its base never proved, and a short program
proves `false` under default options with no audit finding, and in four of the
five with no warning (the fifth has only a deprecation warning).
They are filed as issues
[408](https://github.com/erniecohen/dafny/issues/408) to
[412](https://github.com/erniecohen/dafny/issues/412); a sixth,
[404](https://github.com/erniecohen/dafny/issues/404), came from the plan for
issues 88 and 89.  Section 4 lists them, states the one principle they all
break, and says which stage of this plan needs which fix.

The second pass also changed the design in three places:

- The capture check for renaming is no longer a prediction from the base's
  text.  It is a check on the resolved refining module, the same check that
  fixes [issue 410](https://github.com/erniecohen/dafny/issues/410)
  (sections 2.5 and 4.3).
- A copy can be re-bound only by another copy.  Re-binding it by an alias,
  which the first version allowed, is unsound (section 3.4).
- A copy is fresh at every position that a later refinement can change, and
  every refinement of the importer gets its own copy.  Sharing inner copies, as
  the first version did, is unsound once copies can be re-bound (section 3.3).

## 1. The two requests in brief

| | Issue 106: renaming | Issue 107: copying imports |
|---|---|---|
| What is asked | A refinement may choose new names for things the base named. | `import X := Q` gives the importer its own copy of `Q`, whose declarations a refinement of the importer may still refine. |
| What the plan offers | A refinement may rename what only its own declaration can see: type parameters, out-parameters, a function's result, and the local name of an in-parameter.  What clients can see keeps its name: declarations, members, constructors, destructors, iterator parameters, and the label of an in-parameter used by named arguments.  Signature agreement compares types, not their spelling, so a type can be given a new name by a synonym. | `import X := Q` declares a nested module `X` that refines `Q` and adds nothing, keeps `Q`'s default export, and may be re-bound in a refinement of the importer by `import X := Q2`, where `Q2` refines `Q`. |
| What is ill-posed | Removing a name that clients can see.  Every client written against the base is re-resolved against each refinement, so a refinement that removes a visible name is not a substitute for its base.  The closest sound alternative is an added name (a synonym, a label that stays), shown in section 2.4. | Two things.  A copy "as deep as one likes": the depth is forced, a copy is fresh exactly where a refinement can change it (section 3.3).  And a copy that is later merged with another module's types: the importer can prove that a copy's classes differ from all others, so merging them later falsifies its proofs.  The closest sound alternative is an abstract import, whose identity the importer does not know (section 3.4). |
| Option | `--refinement-renaming`, off by default | `--copy-imports`, off by default |
| Soundness argument | Renaming is alpha-conversion of each refined declaration; every obligation is the same proposition before and after, which the resolution-stability check of section 4.3 confirms name by name (section 2.6). | A copy is a refinement that adds nothing, and re-binding it is a refinement step.  So copies are as sound as refinement is: sound once the holes of section 4 are closed, and no sounder (section 3.6). |

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

An override does not see the names of the member it overrides: a class may
override a trait's member with different parameter and result names today, and
calls through the trait bind by position or by the trait's names (S1).  So the
names called internal here are internal for traits too.

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

Four details of the substitution:

- A named argument in inherited text, such as a recursive call
  `F(x := x - 1)`, has a label and an expression.  The expression is renamed,
  the label is not.
- Attributes on the base declaration and on its clauses are part of its text and
  are renamed with it.
- A refining function may introduce a result name where the base has none
  (`function F(n: int): int` refined with `: (r: int)`), since the base's text
  has no occurrence to rename.  If the refinement gives none, the base's stays.
- The modifiers of a parameter (`ghost`, `new`, `nameonly`, `older`) are the
  base's, as today.  `nameonly` belongs to the label.

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
today, so nothing downstream sees the new spelling.  For the same reason this
comparison cannot affect soundness: the refinement's text is resolved against
the base's types whatever the comparison answers, so a wrong answer could only
accept or reject a spelling.

Hiding the old name from clients is not part of this plan.  A refinement's
export set cannot reveal `Node` without `T` today (E20: "This export set is not
consistent"), and making it able to is a question about export consistency,
not about refinement.

### 2.5 Collisions

Alpha-conversion is sound only when it captures nothing.  The rule is to reject,
not to rename further.

The first version of this plan predicted capture from the base's resolved text:
"the new name `y` occurs in the base's text and means something else there".
That is not enough, and it is the wrong place to look.  It is not enough because
the refining module can add the declaration that is captured or that captures:
a field `y`, a local variable in a statement skeleton, a member of a newly added
parent.  And refinement has this problem already without any renaming:
[issue 410](https://github.com/erniecohen/dafny/issues/410) proves `false` with
a refinement that only adds a function whose name the base took from an opened
import (H3).

So the check is made where the names are finally bound, in the resolved
refining module.  It is the **resolution-stability check** of section 4.3:
every name occurrence in inherited text must resolve to what corresponds to its
original target.  Under renaming, "corresponds" for a renamed binder means the
renamed binder itself.  The check covers these cases, each reported at the
rename with the inherited occurrence that changed meaning:

- **New name already used in inherited text.**  The base's text uses `y` for a
  field, a constant, a member, a module, a type or another formal.  After the
  rename that occurrence resolves to the parameter `y`: not its original
  target, so an error.
- **Binder in inherited text.**  The base's text binds `y` (a quantifier, a
  `var`, a `match` pattern, a lambda) around a use of the old name `x`.  After
  the rename that use resolves to the inner binder, not to the parameter: an
  error.
- **Inner type parameters.**  A class type parameter `T` renamed to `U`, where
  a member declares its own `<U>` and mentions `T`, is the previous case for
  types.
- **Names the refining module adds.**  A new local name that equals a
  declaration the refining module itself adds, where inherited text reaches it.
  This is the case that a check against the base alone cannot see.

Other rules:

- **Two binders, one name.**  Two parameters, two type parameters, or an in- and
  an out-parameter renamed to the same name: error, as for any declaration.
- **Permutations are allowed.**  `F(x, y)` refined as `F(y, x)` renames
  simultaneously and is accepted; the labels stay `x` and `y` in their
  original positions, so `F(y := 1)` still sets the *second* parameter.  This
  is sound, and it is the case that rules out letting both the old and the new
  name serve as labels (open question 1).
- **Chains.**  `A` names `x`, `B refines A` renames it `y`, `C refines B`
  renames it `z`.  `C` is built from `B`'s merged declaration, which already
  says `y`, so each step is checked against its own base.  The label stays `x`
  throughout.  Renaming back to an earlier name is an ordinary rename.

### 2.6 Why it is sound

A renamed declaration is alpha-equivalent to the declaration that would have
been built had the base used the new names, provided no occurrence changed its
target.  The stability check establishes exactly that proviso, for the renamed
binders and for every other name in the inherited text.  Then every obligation
the verifier emits for the declaration is the same proposition up to the names
of bound Boogie variables, so:

- an obligation proved in the base and inherited unchanged is still an
  assumption in the refinement ([BoogieGenerator.cs:3713-3718][bg3713]), and it
  is still true, because it is the same proposition;
- an obligation the refinement adds is checked, as today;
- named-argument calls in clients bind the same positions as before, because
  labels do not change, so a client's re-resolution against the refinement
  produces the same calls.

What is trusted is the stability check, and nothing else that is new.  The
check is not specific to renaming: it is the default-on fix of issue 410, so
renaming needs that fix first (stage 0) and then adds one clause to its
definition of "corresponds".  Renaming changes no call graph, no field, no
ancestor and no import, so it does not touch the other holes of section 4.

The rename touches no axiom, no visibility, and no export set.  It changes the
names in the generated Boogie of the refined declaration only, so resource
counts of that declaration may move, as they do for any change of generated
names (compare the note of
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
  transparent (E17).  For a class the difference is a fact the importer can use
  in proofs: `o is Q.C ==> !(o is X.C)` verifies (S2).  But a refinement of `M`
  cannot refine `X` (E03), and `X` does not keep `Q`'s default export
  ([:1700-1712][rt1700]; E23, E24).

What a module knows about the identity of a type differs between these, and
section 3.4 turns on it.  Types reached through an alias are the target's
types.  Types of a nested refinement are new, and known to be new.  Types
reached through an abstract import are neither: the importer cannot prove that
a class of one abstract import is, or is not, a class of another abstract
import or of a concrete module (S3).  To the type checker the types of two
abstract imports are different (S4), but that is a fact about which programs
are accepted, and no proof can use it.

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
   The copied text means what it meant in `Q`: an import in inherited text
   keeps its target even when the refining module sits where another module of
   the same name is in scope (S7), and a nested module cannot see the
   declarations or imports of the module around it (S6).
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

The depth is not a choice.  One requirement fixes it, the **path-identity
invariant**:

> Let `p` and `q` be two paths to type declarations that `M`'s text can write.
> If they denote the same declaration in `M`, they denote the same declaration
> in every refinement of `M`.  If they denote two different concrete
> declarations in `M`, they denote different declarations in every refinement
> of `M`.  Only where `M` knows neither, because a path goes through an
> abstract import or ends at an abstract type, may a refinement decide.

`M`'s proofs are inherited by its refinements as assumptions.  They may have
used that two paths are the same (trivially, the type checker identifies them)
or that two classes differ (S2).  If a refinement could change either, an
assumed proof would be false.  The invariant holds for refinement today,
because the only thing a refinement can re-bind is an abstract import, whose
identity nobody knew.  Copies add a second thing that can be re-bound, so the
rule is: **a copy is fresh at every position that a refinement can change, and
shared at every position that none can.**

1. **`Q`'s own types and members are fresh**, as in any refinement.
   Datatypes, codatatypes, classes, traits, newtypes, subset types, abstract
   types, iterators and the default class get new identities.  Type synonyms
   are transparent, so a copied `type U = int` is still `int` (E17).
2. **A copying import is a declaration of the module that writes it, and is
   cloned like one.**  Every refinement of `M` gets its own copy at an
   inherited `import X := Q`, unless it re-binds `X` (3.4): `M'.X.T` is not
   `M.X.T`, just as `M'.C` is not `M.C` for a class `C` of `M`.  The same holds
   inside a copy: if `Q` has `import Y := R`, then `X.Y` is `X`'s own copy of
   `R`, not `Q.Y`.  A nested refinement behaves differently today: `M` and `M'`
   share it (S10), which is harmless only because nothing can re-bind it.
3. **An abstract import inside `Q` gets its own facade in `X`.**  A nested
   refinement shares the facade today: with `import Y : R` in `Q`, `Q.Y.D` and
   `X.Y.D` are one type (S5).  That is harmless today only because nothing can
   re-bind a nested refinement.
4. **Nested literal modules and alias imports of `Q` are shared.**  Refinement
   points the clone at the same signature ([:1712-1716][rt1700]), so a nested
   module `N` of `Q` is the same module in every refinement and every copy of
   `Q` (E09), and an alias import still names its target.  No refinement can
   change either (E03, E05).  A nested module cannot mention a type, a member
   or an import of the module around it (S6), and a copying import is an
   import, so a shared nested module never names a fresh position.  It can
   name a sibling nested module, which is shared with it.
5. **Through an abstract import, a copy is seen as an abstract import is.**
   With `import I : M`, the types of `I.X` have unknown identity.  That is how
   an abstract import nested in `M` is seen through `I` today (S12), and unlike
   a nested literal module of `M`, which is the same module seen through `I`
   (S11).

Why rules 2 and 3 are forced.  Suppose inner copies were shared, as the first
version of this plan had it, and let `M` have two copies of `Q`:

```dafny
module Q { import Y := R }
module M {
  import X := Q
  import Z := Q
  lemma Same(o: object) ensures o is X.Y.C <==> o is Z.Y.C { }
}
module M' refines M {
  import X := Q2   // Q2 refines Q and re-binds Y := R2
}
```

With sharing, `X.Y.C` and `Z.Y.C` are one class in `M`, and `Same` is trivial.
In `M'`, `X.Y` is a copy of `R2` and `Z.Y` is not, `Same` is inherited and
assumed, and an object of `X.Y.C` makes it false.  With rules 2 and 3 the two
classes differ in `M` and keep differing.

The rest of rule 2, and rule 5, are forced the same way.  A refining module may
import its own base.  If `M'` shared `M`'s copy, `M'` could prove
`o is X.C <==> o is Base.X.C` (it can today for a nested refinement, S10), and
a refinement of `M'` that re-binds `X` would inherit that as a false
assumption.  Rule 5 closes the same gap for a module that reaches `M'` through
an abstract import and binds it later to a refinement of `M'`.

This answers the issue's question "how deep the copy is" without a parameter.
A module that wants more of its dependencies copied writes copying imports for
them.  A copy of `Q`'s nested literal modules would need them to be refinable,
which they are not today.

### 3.4 Re-binding a copy

In `module M' refines M`, an inherited copy `X` may be re-bound by one form:

```
import [opened] X := Q2[`E2]
```

a copy of `Q2`, again re-bindable in refinements of `M'`.  It is accepted only
if:

1. **`Q2` refines the module that `X` copies in `M`**, reflexively and
   transitively, checked by walking `Q2`'s refinement chain.  `X` adds nothing
   to that module, so refining `X` and refining it are the same condition.  For
   a chain `M0`, `M1`, `M2` the condition is against the previous step, not
   against the first.
2. **`M`'s inherited text keeps its meaning**: the resolution-stability check
   of section 4.3, with the declarations of the new `X` corresponding to those
   of the old one along `Q2`'s refinement chain.  It is not implied by
   condition 1: a proper refinement that defines an abstract type as a datatype
   can turn a pattern variable in `M`'s text into a constructor (H3, last
   variant, shown today with an abstract import).
3. **`opened` matches**, as for every import a refinement redeclares
   ([:170-183][rt170]).

The export set needs no condition of its own for soundness.  If `E2` shows less
than `E` did, any inherited text that used what is now hidden fails to resolve
(S8), and what still resolves is covered by condition 2.  A name that `E2` adds
to an opened import either loses to a local declaration or is reported as
ambiguous (S9).  The plan still requires the names of `E2` to include those of
`E`, so that the error is reported at the import and not inside inherited text.
`CheckIsRefinement` has such a comparison ([:427-430][rt414]), but it is by
name, is marked as a TODO, and is not reached today (section 4.2), so it is a
starting point and not something to rely on.

Anything else named `X` in `M'` (an alias, a literal module, an abstract
import, another kind of declaration) is an error.

**Why not an alias.**  The first version allowed `import X = Q2`, to let two
components agree on a type.  It is unsound.  `M` can prove that a class of a
copy differs from every other class (S2).  If `M'` then makes `X` an alias of
`Q2`, the classes of `X` are those of `Q2`, which other modules also use, and
`M`'s inherited proof that they differ is assumed and false.  This breaks the
second half of the path-identity invariant.  No side condition on `M'` repairs
it, because the clash can come from a module that imports `M'` later.

Agreement on a type is what an abstract import is for: `import X : Q` leaves
the identity of `X`'s types unknown, and a refinement may bind two abstract
imports to one module.  A module cannot have both a concrete copy whose types
it uses as new types and the freedom to merge them later.  Open question 3
gives the one alternative that would keep `=`, and its cost.

The plan does not add syntax for refining `X` in place (`module X ... { }`).
`Q2` is written as an ordinary module refining `Q`, where it can be verified,
named and reused.

### 3.5 Type identity, at the edges

| two types | what the importer knows |
|---|---|
| the same declaration in two copies, in one importer or in two | different; for classes and traits provably so |
| a copy's declaration and the original's | different, in the same way (E10, S2) |
| a copy's declaration in `M` and in a refinement of `M` | different (3.3, rule 2) |
| a copy's declaration seen through an abstract import of `M`, and any other | not known either way (3.3, rule 5) |
| a type reached through an abstract import, and any other | not known either way (S3, S4) |
| a type of a nested literal module or alias import of `Q`, reached through `Q` and through a copy of `Q` | the same (E09) |

- After `M'` re-binds a copy, code inherited from `M` that was resolved against
  the old `X` is resolved again against the new one, as inherited code is
  resolved again against any replacement today.
- A copy of a module that declares `{:extern}` names would declare them twice in
  the compiled program.  Copying a module with an extern name is an error.
- A `replaceable` module cannot be refined ([:155-160][rt155]), so it cannot be
  copied.  A module cannot copy a module that encloses it, or itself: that is a
  cycle in the module graph, reported as refinement cycles are.

### 3.6 Why it is sound, and relative to what

The argument is the argument for refinement, applied twice.  It is only as good
as that argument, which has holes today (section 4).

- **The copy.**  `X` refines `Q` and adds nothing.  Every obligation in `X`
  was proved in `Q` and is inherited unchanged, so the translator assumes it
  ([BoogieGenerator.cs:3713-3718][bg3713]).  That is sound when it is the same
  proposition, about the copied declarations, that `Q` proved about its own.
  For a refinement that adds nothing, each hole of section 4 is closed by
  construction: there is no added declaration to capture a name, no new body to
  close a call cycle, no new field, no new parent, no skeleton.  What remains
  is that the copied text is resolved in a new place, and S7 and S6 show that
  the place does not change what it means.  So stage 4 needs none of the
  stage-0 fixes.
- **The re-binding.**  `M` was verified against a copy with exactly `Q`'s
  content.  If refinement is sound, every fact `M` proved about that copy
  holds of a copy of any refinement `Q2` of `Q`, and `M'` may inherit `M`'s
  obligations as assumptions.  This step needs all of: condition 1 of 3.4, the
  analogue of [issue 408](https://github.com/erniecohen/dafny/issues/408);
  condition 2, the stability check; the path-identity invariant of 3.3; and a
  sound `Q2 refines Q`, which is issues
  [404](https://github.com/erniecohen/dafny/issues/404),
  [409](https://github.com/erniecohen/dafny/issues/409),
  [410](https://github.com/erniecohen/dafny/issues/410),
  [411](https://github.com/erniecohen/dafny/issues/411) and
  [412](https://github.com/erniecohen/dafny/issues/412).  So stage 5 needs all
  of stage 0.

The path-identity argument of 3.3 is a paper argument.  The cases of its table
that exist today were tested (S2 to S5, S10 to S12, E09, E10); the cases that
need copies become tests of stages 4 and 5.  Rules 2, 3 and 5 make a copy
differ from today's nested refinement, so stage 4 is new code in the cloner and
in the construction of abstract-import signatures, not a new spelling of what
exists.

The export view adds nothing to trust: `M` sees less than all of `X`, so it
proves less.

## 4. What both features rest on: refinement must be sound

### 4.1 The principle

Refinement copies the base's text into the refining module and resolves it
again there.  The translator then treats what was inherited as already proved:
an inherited assertion, postcondition or precondition check becomes an
assumption ([BoogieGenerator.cs:3713-3718][bg3713]), an inherited call to
another module is free ([BoogieGenerator.TrCall.cs:377-385][trc377]), and an
inherited call is checked for termination only when the surrounding `decreases`
clause is new ([BoogieGenerator.Decreases.cs:114-117][dec114]).  `dafny verify`
goes further and does not run inherited members at all
([TextDocumentLoader.cs:70-74][tdl70], [RefinementOrigin.cs:20][ro20]).

That is sound under one condition, the one the plans for issues 88 and 89
(draft [pull request 400](https://github.com/erniecohen/dafny/pull/400)) and
for cross-module recursion (draft
[pull request 397](https://github.com/erniecohen/dafny/pull/397)) also state:

> An inherited obligation may be assumed only if the base proved **the same
> proposition under the same hypotheses**.

Each hole below is a way for the refining module to change the proposition or
the hypotheses while the obligation still counts as inherited.

### 4.2 The holes

Each row is a program that `dafny verify` accepts and that proves `false`.
All were reproduced under default options and with `--type-system-refresh`.

| | what the refining module does | why the assumption is wrong | issue |
|---|---|---|---|
| H1 | binds an abstract import to a module that does not refine the imported one | the hypotheses changed: the inherited proof used facts of the imported module that the new module does not have | [408](https://github.com/erniecohen/dafny/issues/408) |
| H2 | gives a body that calls back into inherited code, so that inherited calls become recursive | the base proved termination for a call graph without that cycle | [409](https://github.com/erniecohen/dafny/issues/409) |
| H3 | adds a declaration whose name the inherited text uses, so the text resolves to the new declaration | the proposition changed: the same text now says something else | [410](https://github.com/erniecohen/dafny/issues/410) |
| H4 | adds a field to a class whose constructor is inherited | the obligation is new (the field must be assigned) but sits in an inherited member, which `dafny verify` does not run | [411](https://github.com/erniecohen/dafny/issues/411) |
| H5 | adds, in a statement skeleton, a call that writes state the inherited postcondition speaks of | the inherited postcondition was proved for a body without that call | [412](https://github.com/erniecohen/dafny/issues/412) |
| | adds a parent trait to an inherited type, with an override | the override check is skipped as inherited | [404](https://github.com/erniecohen/dafny/issues/404) (found by the plan for issues 88 and 89) |

Notes on each, beyond what the issues say:

- **H1.**  When a refinement redeclares a name that the base declared as an
  abstract import, `RefinementTransformer` records the correspondence and
  checks nothing ([:229-232][rt229]).  `CheckIsRefinement`
  ([:414-435][rt414]) would be the check, but it is reached only through
  `MergeTopLevelDecls` ([:300-307][rt300]) for declarations whose `IsRefining`
  is true, and for an import or a nested module it never is
  ([Declaration.cs:29][decl29]).  So all four forms are accepted: an alias, a literal module, a nested refinement of
  an unrelated module, and another abstract import.  The compiled form fails at
  run time with a division by zero.  The fix is a walk of the new module's
  refinement chain at that branch.
- **H2.**  The cycle can be closed by a lemma, a function or a method body, and
  through a chain of refinements.  The compiled form overflows the stack.  The
  design note of pull request 397 describes the same hole in its section 2.5.
  [Issue 405](https://github.com/erniecohen/dafny/issues/405) is a different
  hole with the same effect, where a `replaces` module closes the cycle.  The
  fix compares call-graph components: an inherited call is exempt from the
  termination check only if its component in the refining module, with every
  member and edge, is inherited unchanged.
- **H3.**  The capturing declaration can be a function, a type, a nested
  module, a class member that hides a module-level constant, a member of an
  added parent trait, a local variable of a statement skeleton, or a constant
  or datatype constructor that turns a pattern variable into a pattern, and it
  works through a chain.  It also works across a replaced abstract import
  whose replacement is a proper refinement: the base matches a value of an
  abstract type `X.T` with `case foo =>`, and the replacement defines
  `datatype T = foo | bar`.  The fix is the check of 4.3.
- **H4.**  The translator does emit the definite-assignment check, and `dafny
  build`, `run` and `translate` report it.  `dafny verify`, which is what a
  proof gate runs, skips it.  The fix is that `dafny verify` runs inherited
  members.  Their inherited obligations stay assumed, so the added cost is
  small.  This fix is a prerequisite for others: the checks that the fixes
  of H2 and of issue 404 add are emitted in inherited members.
- **H5.**  The skeleton rules reject a call before resolution
  ([:1587-1588][rt1587]), but `x := F(c)` and a bare `F(c);` parse as update
  statements, and the check of update statements ([:1606-1633][rt1606]) does
  not look at what they resolve to.  The other ways a skeleton could write
  inherited state are rejected (S14).  Statement skeletons are deprecated, and
  the program gets that warning.

Some attacks failed.  A new body is checked against the inherited
specification (S13).  A cycle that the refinement closes through a constant or
a least predicate is caught by resolution (S15).  A base cannot prove that a
class lacks a trait, so adding a parent does not falsify a type test (S16).

A stack overflow of the tool itself turned up on the way and is filed as
[issue 413](https://github.com/erniecohen/dafny/issues/413).  It is a crash,
not a soundness problem.

### 4.3 The resolution-stability check

This check is the fix of H3, the capture rule of renaming (2.5), and
condition 2 of re-binding a copy (3.4).  It is defined once, here.

Let `B` be the base and `R` the refining module.  An *inherited occurrence* is
a use of a name in text that `R` cloned from `B`: an identifier, a type name, a
segment of a qualified name, a member selection, a pattern identifier, a
statement label.  `B`'s resolution bound the occurrence to a target `t`.
`R`'s resolution binds its clone to a target `u`.  The check requires
`u = corr(t)`, where:

| `t` is | `corr(t)` is |
|---|---|
| a declaration of `B`: a type, a member, a constructor, a nested module, an import | the declaration of `R` that it became: its clone, or the merged declaration if `R` refines it |
| a local binder in the cloned text: a formal, a local variable, a bound variable, a pattern variable, a type parameter, a label | the clone of that binder, under its new name if `R` renamed it (2.3) |
| a declaration reached through an import that `R` keeps | the same declaration |
| a declaration reached through an abstract import or a copy that `R` re-binds | the declaration that refines it, followed along the refinement chain of the new module |
| a built-in type or member | itself |
| a member found by lookup in a type | `corr` of that member, even if the type gained members or parents |

A pattern identifier has a kind as well as a target.  One that bound a variable
in `B` must bind a variable in `R`.  One that named a constructor or a constant
must name the corresponding one.

The requirement is the property: each inherited occurrence means in `R` what
corresponds to its meaning in `B`.  The table is what an implementation must
cover and what its tests must show.  Two things follow from it:

- The check needs `B`'s resolved targets, per occurrence, when `R` is
  resolved.  The cloner can record on each cloned node the node it came from.
  For a base loaded from a library this is the "full readable base" that
  refinement already needs.
- The check only rejects programs.  A refinement that hides a name on purpose
  has to rename one of the two.

The check is about names.  What the resolver derives from a target (a type, a
ghost status, a default `decreases` clause) is a function of the target and of
signatures that refinement already compares.  The plan takes it to follow, and
has not tested it.  Whoever fixes issue 410 should confirm it for the default
`decreases` clauses of inherited members whose parameters have an abstract
type that `R` defines.

A coarser rule is possible: reject any declaration added by `R` whose name is
visible anywhere in `B`'s scope.  It is simpler and rejects more, including
refinements that are harmless today.  The plan prefers the exact check (open
question 2).

### 4.4 Which stage needs which fix

| stage of section 8 | needs |
|---|---|
| renaming (stages 1 to 3) | the fix of issue 410 only.  Renaming adds a clause to `corr` and changes nothing else. |
| copies (stage 4) | none.  A copy adds nothing, so none of the holes can be opened through it (3.6). |
| re-binding a copy (stage 5) | all of them.  Re-binding is sound only if `Q2 refines Q` is. |

The fixes are soundness fixes.  As `AGENTS.md` asks, each is on by default with
a hidden option that restores today's behaviour, and each only rejects programs
or adds checks.  Each is its own issue and pull request, not part of either
feature.

## 5. Interaction with module-level caching and resolution keys

The content-addressed caches of
[issue 261](https://github.com/erniecohen/dafny/issues/261) (a sub-issue of
[issue 241](https://github.com/erniecohen/dafny/issues/241)) key each module's
resolution by its source, its prerequisites' keys, the options and the version,
and derive generated names from that key
([261-content-addressed-caches.md][d261]).  Part of it exists: a resolution key
per module declaration ([ProgramResolver.cs:218-221][pr218]), with each
prerequisite tagged by its role ([:453-459][pr453]), a process-local
translation cache keyed on it (`--translation-cache`), and a verification
cache keyed on the complete exchange with the solver (`--verification-cache`).

**What was tested.**  Three experiments, on a development build of the fork
that has both cache options (the release does not), with both caches on:

- K1.  A base and a refining module are verified.  The base's postcondition is
  then strengthened to one that the refining module's body does not meet, and
  both are verified again against the same cache directory.  The second run
  reports the error.  No stale verdict is served.
- K2.  The capture program of H3 and its harmless twin give the same verdicts
  with and without the caches.  A cache neither hides the hole nor adds to it.
- K3.  Two nested refinements of one module, with the same simple name in two
  importers, verify with the caches as without.

This is what the keys predict.  The verification cache keys on the solver's
input, so an assumption that changes, changes the key.  A refining module's
resolution key includes its base's whole key, under the role "refinement".

**Copies.**

- A copying import is a **refinement edge**, not an import edge.  The design's
  prerequisite table gives a refiner "the actual resolved base root", because
  "a refiner can read/clone the base's implementation"
  ([261 design, prerequisite table][d261-pre]), and its mutation inventory
  keeps "full readable base is a prerequisite" for refinement-base cloning
  (R21, [261 design][d261-r21]; [241-front-end.md:435][d241fe]).  A copy reads
  `Q`'s bodies, so `X`'s key includes `Q`'s whole key, even when the import
  names an export set.
- **Every fresh position is its own vertex** with its own full name: `M.X`,
  an inner copy or facade `M.X.Y` (3.3, rules 2 and 3), and the copy `M'.X` of
  each refinement of `M`.  `M` depends on `X` through the view `E`.  So an
  edit to a body in `Q` invalidates `X`, and the later observable-view cutoff
  ([261 design, observable views][d261-v]) can stop it at `X` when `E`'s view
  is unchanged.
- **Generated names** of two copies of one module must differ.  They do: the
  copies have different full names, and since
  [issue 392](https://github.com/erniecohen/dafny/issues/392) a module's
  generated-name prefix is a hash of its full name.  K3 is the case that exists
  today; a test pins two copies named `X` in different importers, and `M.X`
  against `M'.X`.
- **Re-binding.**  The key of `M'.X` includes `Q2`'s whole key, and `M'`'s key
  includes that of `M`.  The replacement relation is recorded where today's is
  (`CardinalityRefinementBase`, [:232][rt229], [:411][rt411]); the 261 design
  places replacement facts in the global projection `G` only when translation
  reads them (R19).  No new kind of key is needed.

**The stage-0 checks.**  The stability check (4.3) and the call-graph
comparison (H2) read the base's resolved targets and its call graph.  That adds
no dependency edge, since the base's whole key is already a prerequisite.  But
a base whose resolution is restored from a cache must supply the target of each
name occurrence and its call-graph components, or be resolved again.  The 261
work should count both as part of what "full readable base" means.

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

**Options.**  Both feature options change what resolves, and so does each
hidden option of stage 0.  All of them join the options that a resolution key
and a library check must see: they are listed where
`--synonym-refines-abstract-type` is
([DafnyCommands.cs:128][dc128], [ModularTerminationRecord.cs:748][mtr748]) and
get rows in the option audit ([261-option-audit.tsv][d261-audit]).

## 6. Interaction with member invariants and cross-module recursion

### 6.1 Member invariants

The member-invariants plan is in draft
[pull request 384](https://github.com/erniecohen/dafny/pull/384)
(`docs/dev/issue105-member-invariants-plan.md`).  Its section 4.2 adds
invariant conjuncts in refinements, assumes inherited conjuncts at inherited
sites, and asserts added conjuncts there.

- **That plan depends on issue 408.**  Its section 4.2 says that a module with
  `import A : M0` refined by `import A = M1` gets `M1`'s added conjuncts as
  obligations at its inherited conversion sites.  If the replacement need not
  refine `M0` (H1), it may drop `M0`'s invariant, while the inherited sites
  keep assuming it.
- **That plan depends on issue 411.**  The obligations it adds at inherited
  conversion sites are new obligations inside inherited members.  `dafny
  verify` does not run those today (H4), so the obligations would be emitted
  and never checked.  It is the same shape as
  [issue 404](https://github.com/erniecohen/dafny/issues/404).
- **Copies.**  `X.T` copies `Q.T`'s invariant conjuncts and its raw type
  `raw X.T` is new.  The copy adds no conjunct, so it adds no obligation.
  When `M'` re-binds `X` to a copy of `Q2`, and `Q2` adds conjuncts, `M`'s
  inherited conversion sites to `X.T` get them as obligations: the same rule as
  for an abstract import, with a copy in place of `import A : M0`.  The
  member-invariants implementation must treat a re-bound copy as it treats a
  bound abstract import.
- **Renaming.**  An invariant is a member, so renamed type parameters are
  substituted in it (2.3).  An invariant binds no parameters.
- **Renaming a type by a synonym.**  The member-invariants plan rejects a type
  synonym refining an abstract type that has an invariant (its section 4.3).
  Section 2.4 here does not change that.  If that plan later accepts the
  `--synonym-refines-abstract-type` form, the merged target type carries the
  invariant, and type renaming composes with it.

### 6.2 Cross-module recursion

The design is in draft
[pull request 397](https://github.com/erniecohen/dafny/pull/397)
(`docs/dev/design/xmod-recursion.md`).  It lets modules form recursion groups,
and requires three things to lie in a strictly lower group than the module that
names them: a refinement base, the target of an abstract import, and the module
that a refinement binds to an abstract import.  Copies fit those rules without
a new case:

- **A copy edge is a refinement edge.**  `Q` must be in a strictly lower group
  than the module that writes `import X := Q`.  This also excludes copying a
  module of one's own group.
- **A re-binding is a binding.**  The `Q2` of `import X := Q2` in `M'` must be
  in a strictly lower group than `M'`, as the module bound to an abstract
  import must.  `M`'s inherited calls into `X` then reach bodies that `Q2`
  verified, and no call of `Q2` can come back to `M'`.
- **A copy is never recursive with its original.**  A copied type's text
  names what the original's text names.  If `Q.T` reaches itself through `R.U`
  (`Q.T -> R.U -> Q.T`), then `X.T -> R.U -> Q.T`: the copy reaches the
  original's group and is not in it.  The partner `R` keeps naming the
  original.  Whether a member of a recursion group may be refined at all is
  that design's question; a copy of `Q` is allowed exactly when
  `module X refines Q { }` is.  The first version of this plan rejected such a
  copy.  There is no principle behind that, so it is withdrawn.
- **Termination.**  The hole that design describes in its section 2.5 as having
  no issue yet is now
  [issue 409](https://github.com/erniecohen/dafny/issues/409) (H2).  Its rule
  for when an inherited call may skip the termination check is the fix this
  plan assumes.
- **Renaming** changes no edge of the module graph or of the call graph.

## 7. Options

Both features are new, so both are opt-in, and with each option off the output
is byte-identical to today's.

| option | default | what it allows | what changes when off |
|---|---|---|---|
| `--refinement-renaming` | off | 2.3, 2.4 and 2.5 | nothing: the name checks of 2.1 run as today |
| `--copy-imports` | off | 3.2 to 3.5 | nothing: `import X := Q` is the parse error it is today ("rbrace expected") |

The stage-0 fixes are not features.  Each is on by default and has its own
hidden `--legacy-*` option, named by its pull request
(`--legacy-unchecked-import-replacement` is this plan's suggestion for issue
408).

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
| 0a | **[Issue 411](https://github.com/erniecohen/dafny/issues/411)**: `dafny verify` runs inherited members.  First, because the checks of 0c and of issue 404 are emitted there. | `TextDocumentLoader.cs` ([:70-74][tdl70]) |
| 0b | **[Issue 410](https://github.com/erniecohen/dafny/issues/410)**: the resolution-stability check (4.3). | the refinement cloner, both resolvers, `RefinementTransformer.cs` |
| 0c | **[Issue 409](https://github.com/erniecohen/dafny/issues/409)**: termination of inherited calls that a refinement makes recursive.  Needs 0a. | `BoogieGenerator.Decreases.cs` ([:114-117][dec114]), the call graph |
| 0d | **[Issue 408](https://github.com/erniecohen/dafny/issues/408)**: a module bound to an abstract import must refine the imported module. | `RefinementTransformer.cs` (the branch at [:229-232][rt229]) |
| 0e | **[Issue 412](https://github.com/erniecohen/dafny/issues/412)**: a skeleton may not add a call that writes the heap. | `RefinementTransformer.cs` ([:1606-1633][rt1606]) |
| 1 | **Internal renames**: type parameters, out-parameters, result names; the renaming clone; the renamed-binder clause of the stability check.  Needs 0b. | `RefinementTransformer.cs` ([:449-575][rt449], [:856-905][rt866], [:928-964][rt928]), the cloner |
| 2 | **Labels**: a formal's label separate from its local name; named-argument resolution by label in both resolvers; the error for a local name used as a label. | `Formal`, [PreTypeResolve.ActualParameters.cs:70][ap70], [NameResolutionAndTypeInference.cs:3306][nr3306] |
| 3 | **Type agreement by type**, after resolution (2.4). | `RefinementTransformer.cs` ([:967-971][rt967] and its callers) |
| 4 | **Copying imports**: grammar (`:=`, the token `gets` of [Dafny.atg:135][atg135], in [ModuleImport][atg367]); a module declaration for copies; the default export kept; a copy per refinement, a fresh inner facade, and the facade view of a copy (3.3, rules 2, 3 and 5); abstractness; the extern, `replaceable` and cycle errors. | `Dafny.atg`, `AST/Modules/`, `ModuleResolver.cs`, `RefinementTransformer.cs` ([:1696-1717][rt1700]) |
| 5 | **Re-binding copies** (3.4): the chain check of 0d and the stability check of 0b, applied to a copy.  Needs all of stage 0 and issue 404. | `RefinementTransformer.cs` |
| 6 | **Cache keys** (section 5), when the 261 work reaches resolution keys; **member-invariant hooks** (6.1), when that work reaches refinement. | per those plans |

Stages 1 to 4 do not wait for the stage-0 fixes they do not need.

### 8.1 Tests

Each test is a lit test under `git-issues/`, run under both resolvers, with
the option on, and, where the program parses without it, with the option off to
pin today's error.

**Stage 0.**  Each fix carries the tests of its issue: the reproducer and its
variants are rejected by default and accepted with the legacy option.  For
0d: E06 to E08, the four forms of H1, an accepted replacement by a refinement
(E04, module `B2`), and a refinement two steps down a chain.  For 0b: every
variant listed under H3 in 4.2, and for each a harmless twin that stays
accepted.  For 0a: the suite and the standard library are run with the fix, and
every inherited member that now fails is a finding to report, since nobody has
surveyed which obligations sit in inherited members today.  For all: verdicts
of the suite and the standard library are listed from CI.

**Renaming (stages 1 to 3):**

- *Chains*: `A -> B -> C -> D`, renaming one parameter at each step and back to
  its first name at the last; a call with a named argument written in `A`'s
  client, and in each of `B`, `C`, `D`, all using the label; an inherited
  `ensures` that mentions the parameter, proved in `A` and assumed in `D`; a
  method body merged with a skeleton in renamed variables in `C`.
- *Collisions*: a new name equal to a field, a module constant, a member, a
  module name, and a type name used in the base's text; a new name bound by a
  quantifier, a `var`, a `match` case and a lambda around a use of the old name;
  a new name equal to a field, and to a skeleton local, that the refining
  module itself adds; two parameters renamed to one name; an in-parameter and
  an out-parameter renamed to one name; a swap `F(x, y)` to `F(y, x)`,
  accepted, with `F(y := 1)` setting the second parameter and `F(x := 1)` the
  first; a default value `y: int := x + 1` with `x` renamed (E21 extended); an
  inherited recursive call with a named argument, `F(x := x - 1)`, with `x`
  renamed.
- *Generics*: a function and a datatype with renamed type parameters used in
  nested types (`seq<map<T, set<T>>>`), in a member and in an `ensures`; a
  permutation of two type parameters; a type parameter renamed to a type name
  the base mentions (rejected); a class type parameter renamed to the name of a
  member's own type parameter (rejected); characteristics and variance still
  checked.
- *Exports*: a named-argument call through an export set that provides the
  function; a `nameonly` parameter renamed, still passed by its label; the
  abstract-import replacement of E02 with a renaming `B`.
- *Traits*: an override with other parameter names than the trait's, in a
  refinement that renames the trait's parameter (S1 extended).
- *Not renamable*: iterator parameters, members, constructors, destructors:
  each still rejected under the option.
- *Types*: E12 accepted under the option (`F(t: Node)` refines `F(t: T)`);
  a subset type over `T` and a newtype over `T` still rejected as different
  types.

**Copies (stages 4 and 5):**

- *Identity*: E10 with `import X := A` (both errors stay); two copies of one
  module in one importer are different, and `o is X.C ==> !(o is Z.C)`
  verifies; a synonym is transparent (E17); a copy in `M` and the inherited
  copy in `M'` are different (S10 with a copy must fail to resolve); with
  `import I : M`, neither `o is I.X.C ==> o is M.X.C` nor its negation
  verifies; a nested literal module of `Q` is shared between `Q` and the copy.
- *Depth*: `Q` with `import Y : R`, copied twice: `X.Y.D` and `Z.Y.D` are
  different types (S5 with copies must fail to resolve); the two-copies program
  of 3.3, in which `Same` must not verify; `Q` with `import Y := R`: `X.Y` and
  `Q.Y` differ.
- *Exports*: the default export kept (E23 with a copy resolves `X.F` and
  rejects `X.G`); `import X := Q`E`; a re-binding with a larger export set,
  accepted; with a smaller one, rejected at the import.
- *Chains*: `M -> M' -> M''`, re-binding the copy at each step to a copy of a
  further refinement; a copy inside a copy (`Q` with `import Y := R`), with `Y`
  re-bound through `Q2`; a copy of an abstract module that has an abstract
  import, made concrete two refinements down.
- *Re-binding errors*: by a module that does not refine `Q` (the copy form of
  H1); by an alias `import X = Q2` of a proper refinement, with the program
  that would otherwise prove two classes both different and equal; by a literal
  module; by an abstract import; with `opened` changed; by a proper refinement
  that turns a pattern variable of `M` into a constructor (the last variant of
  H3 with a copy).
- *Generics*: a copied generic datatype and class, instantiated in `M`; a copied
  trait extended by a class of `M`, with a member added in `Q2`, so that `M'`
  must give it a body.
- *Edges*: copying a `replaceable` module, an enclosing module, a module with an
  `{:extern}` name, each rejected; a concrete copy compiled and run, with the
  copy's and the original's datatypes kept apart at run time; copying a module
  from a `.doo` library.
- *Costs*: a copy of a module with an expensive lemma costs the importer almost
  no solver work for that lemma.  After 0a the copy's members are run, with
  their obligations assumed, so the test bounds their resource count; it does
  not expect that they are skipped.

**Option off.**  Each feature's suite and the standard library give the same
verdicts and resource counts with its option off as before it, as `AGENTS.md`
requires.

## 9. Experiments

Each program was run with `dafny verify` (default options plus the settings
named at the top), and the plan cites it by number.  "Verifies" means
`0 errors`.

**E: what refinement does today (first pass).**

| | program, in brief | result |
|---|---|---|
| E01 | issue 106's program: `A.F(x)` refined as `B.F(y)` | two errors: name difference at the parameter; `unresolved identifier: y` |
| E02 | client of `import I : A` calls `I.F(y := 1, x := 2)`; refined with `import I = B`, a refinement of `A`; lemma uses it | verifies |
| E03 | `module X refines A {}` nested in `B`; `B2 refines B` redeclares `module X refines A {...}` | error: "you must use the refining (`...`) notation" |
| E04 | `abstract module B { import X : A }`; `B2` replaces `X` with `module X refines A {...}`; `B3` with `module X {...}` that refines nothing | both accepted, verifies |
| E05 | `module B { import X = A }` with `A` abstract; `B2 refines B { import X = A2 }` | errors: compiled module cannot import an abstract module; the refining notation is needed |
| E06 | the program of issue 408 with a literal non-refining module in place of the alias, and an extra lemma stating `X.F() == 0` | verifies, proving `false` |
| E07 | the program of issue 408 | verifies, proving `false` |
| E08 | the program of issue 408 with the extra lemma | verifies, proving `false` |
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

**H: attacks that succeeded.**  Each verifies and proves `false`.  The programs
and their variants are in the issues.

| | program, in brief | issue |
|---|---|---|
| H1 | an abstract import bound to an unrelated module: by alias, literal module, nested refinement of an unrelated module, abstract import | [408](https://github.com/erniecohen/dafny/issues/408) |
| H2 | a refinement's body calls an inherited lemma that calls it; also functions, compiled methods, a chain, a cycle closed by a new override | [409](https://github.com/erniecohen/dafny/issues/409) |
| H3 | a refinement adds `F` where the base used `F` from an opened import; also a type, a nested module, a class member, a trait member, a skeleton local, a constant or constructor capturing a pattern variable, a chain, and a constructor of a replaced abstract import | [410](https://github.com/erniecohen/dafny/issues/410) |
| H4 | a refinement adds a field of an empty type to a class with an inherited constructor; `dafny verify` accepts, `dafny translate` reports the unassigned field | [411](https://github.com/erniecohen/dafny/issues/411) |
| H5 | a skeleton appends a call to a method that writes a field the inherited postcondition fixes; also inside `if`, and writing an array element | [412](https://github.com/erniecohen/dafny/issues/412) |

**S: attacks that failed.**  These are the facts the design leans on.

| | program, in brief | result |
|---|---|---|
| S1 | a class overrides a trait's function and method with other parameter and result names; calls by name through the trait and through the class | verifies: overrides are positional |
| S2 | `import Q` and nested `module X refines Q {}`; `lemma P(o: object?) requires o is Q.C ensures !(o is X.C)` | verifies: a refinement's class is provably a different class |
| S3 | two abstract imports `X : A`, `Y : A`: `o is X.C ==> o is Y.C`, and `o is X.C ==> !(o is Y.C)`; the same for an abstract import against a concrete refinement | none verifies: identity through an abstract import is unknown |
| S4 | `function f(d: X.D): Y.D { d }` for two abstract imports of one module | type error |
| S5 | `Q` has `import Y : R`; nested `module X refines Q {}`; `function f(d: Q.Y.D): X.Y.D { d }`; and the same for a top-level `Q1 refines Q` with class tests | accepted, verifies: a refinement shares its base's inner abstract import |
| S6 | a nested module names an import, an abstract import, a type or a function of the module around it; a nested module names a sibling nested refinement, and the encloser is refined | the first four are errors ("module Y does not exist", undeclared type, unresolved identifier); the sibling is visible and stays shared |
| S7 | inherited text has `import R`; the refining module is nested, or sits at top level, where another module `R` is in scope; an assertion that holds only of the other `R` | fails: the import keeps its target |
| S8 | `import X : A`E` replaced by `import X = A2`E2`, where `E2` provides what `E` reveals; inherited text uses a constructor | error in the inherited text: unresolved identifier |
| S9 | the replacement of an opened abstract import adds a name `H`: the base has its own `H`; the base gets `H` from another opened import | first: the local `H` still wins, nothing false is provable; second: ambiguity error |
| S10 | `M` has nested `module X refines Q {}`; `M1 refines M`; in `M1` with `import Base = M`, and in a third module, `o is M1.X.C <==> o is M.X.C` and `function f(d: M1.X.D): M.X.D { d }` | verifies: a nested module is shared by a refinement of its encloser |
| S11 | `M` has a nested module (literal, or a nested refinement); with `import I : M`, the same two tests between `I.N` and `M.N` | verifies: a nested module is shared through an abstract import |
| S12 | `Q` has `import Y : R`; with `import I : Q` and `import J : Q`: `o is I.Y.C <==> o is J.Y.C`, its negation, and the same between `I.Y.C` and `Q.Y.C`, and through a refinement of `Q` | none verifies: a nested abstract import seen through an abstract import has unknown identity |
| S13 | a refinement gives a function, a method and a lemma bodies that do not meet the inherited postconditions | errors at each body |
| S14 | a skeleton adds `modify c { c.f := 2; }`, a `forall` assignment, `new D(c)` with a constructor that writes `c`, or an assignment to an inherited out-parameter | each rejected at resolution |
| S15 | a refinement closes a cycle through a constant's definition, or through a least predicate | resolution errors |
| S16 | `lemma L(o: object) requires o is C ensures !(o is Tr)` for a class `C` that does not extend `Tr` | does not verify, so a parent added later falsifies nothing |

**K: caches.**  Section 5 describes K1 to K3.

## 10. Open questions for the owner

1. **Labels.**  *Recommend: a label is fixed by the first declaration; only the
   local name changes.*  Letting both the old and the new name serve as labels
   is ambiguous for permutations (2.5).  A later explicit syntax for a label
   different from its local name, in any declaration, would be a separate
   feature.
2. **Capture.**  *Recommend: reject, by the exact check of 4.3, for renaming and
   as the fix of issue 410.*  One alternative is the coarse rule of 4.3, which
   is easier to implement and rejects refinements that work today.  The other
   is to rename the inner binders of the base's text automatically, which
   accepts more programs but changes names the user did not write.
3. **Re-binding a copy.**  *Recommend: by `import X := Q2` only.*  Re-binding by
   an alias is unsound as long as an importer can prove that a copy's classes
   are new (3.4).  The alternative that keeps `=` is to give a copy's types
   unknown identity, as an abstract import's have: the translator would emit no
   distinctness facts for them.  That is a change to the translator's type
   axioms, it removes what separates a concrete copy from an abstract import,
   and the agreement it buys is available today through an abstract import.
4. **Depth.**  *Recommend: accept the forced depth of 3.3.*  It makes a copy
   differ from a nested refinement in three ways: every refinement of the
   importer gets its own copy, an abstract import inside the copied module gets
   its own facade, and a copy seen through an abstract import has unknown
   identity.  The alternative is to keep copies exactly as deep as nested
   refinement and to drop re-binding (stage 5).  That is sound and much
   smaller, but it gives up the refinability the issue asks for: it would add
   only the kept default export to what a nested `module X refines Q { }` does
   today.
5. **Stage 0.**  *Recommend: fix issues 408 to 412 and 404 by default, each with
   a hidden legacy option, in the order of section 8, and let stages 1 to 4
   proceed beside them.*  Issue 411 comes first because other fixes put their
   checks in inherited members.  Its fix makes `dafny verify` do more work in
   every refining module, and may surface failures in existing code.
6. **Statement skeletons.**  *Recommend: fix issue 412 by rejecting the call,
   and keep skeletons.*  They are deprecated, and removing them would close
   that hole and shrink the stability check.  But removing a language feature
   is a larger decision than this plan should make in passing.

[rt155]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L155-L160
[rt170]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L170-L183
[rt217]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L217-L221
[rt219]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L219
[rt224]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L224-L235
[rt229]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L229-L232
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
[rt766]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L766
[rt767]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L767-L769
[rt770]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L770-L772
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
[rt1587]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L1587-L1588
[rt1606]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementTransformer.cs#L1606-L1633
[dec114]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Verifier/BoogieGenerator.Decreases.cs#L114-L117
[trc377]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Verifier/Statements/BoogieGenerator.TrCall.cs#L377-L385
[tdl70]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Pipeline/TextDocumentLoader.cs#L70-L74
[ro20]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Rewriters/RefinementOrigin.cs#L20
[decl29]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/AST/TypeDeclarations/Declaration.cs#L29
[pr218]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Resolver/ProgramResolver.cs#L218-L221
[pr453]: https://github.com/erniecohen/dafny/blob/1920c844bb0d4505d0efc318f27cc724928c04fe/Source/DafnyCore/Resolver/ProgramResolver.cs#L453-L459
