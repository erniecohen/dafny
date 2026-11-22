# The per-module resolution identity (issue 261)

This is the stable key for one module's resolution result, for the persistent
caches of [issue 261](https://github.com/erniecohen/dafny/issues/261) (part of
[241](https://github.com/erniecohen/dafny/issues/241)): the resolution cache,
and the translation table, which maps it to the hash of the module's Boogie.

## What it covers

`ModuleResolutionKey.Compute` (L) is the semantic key from the owner's design
decision 1: the module's source text, its options, the producer version and the
keys of its prerequisites. A resolved module holds one more thing that L does not
fix: **generated names.** A module's generated names carry a prefix from
`ModuleNamePrefixes`, which depends on the set of module names in the program.
Adding an unrelated module whose name digest starts like this one's lengthens the
prefix without changing L. The coordinator decision of 2026-11-18 on 241 made the
effective prefixes of the module and of each prerequisite part of the identity.

```text
context(m)  = H("dafny-module-resolution-context-v2", prefix(m),
                [(role, name, identity(p)) for each direct prerequisite p])
identity(m) = H("dafny-module-resolution-identity-v2", L(m), context(m))
```

`H` is SHA-256 over a length-delimited encoding, written as lower-case hex.
Prerequisites are sorted by role, name and identity. Taking the prerequisites'
identities, not their keys, makes a change in the context of a prerequisite a
change in the identity of every module that depends on it.

## Where the module is

The identity leaves out where the module's source is, so that one store serves
every checkout of a tree:

- **Not the path.** No URI, path or project root is in L or in the context. Two
  checkouts of the same tree, under different directories, have the same
  identities.
- **Not the start.** L hashes the module's text (the original text of each node
  whose text is in it: the module, or each member of the default module, or each
  declaration of a recursive module group), not where that text starts. Inserting
  lines above a module, or editing another module of its file, keeps its identity.

Source coordinates still reach a module's result: every token of an AST node,
and every diagnostic, has a file, an offset, a line and a column. They are the
only part of a result that depends on where the source is. Generated names do
not: they come from the prefix, the declarations' names and counters within the
module. The text of a few messages names a line; a cache must not reuse a result
with such a message, and the resolution cache declines to store one. So a
coordinate is a function of the identity and of where the module's text starts:

- A token of the module's own source is at a fixed offset from the start of the
  text it is in, which L fixes. A cache can store it as that offset and bind it to
  the token of the current parse there, which has the current path, line and
  column.
- A token of a prerequisite's source can be stored by its rank among the tokens
  that the prerequisite's own result refers to, and bound to the prerequisite's
  token of that rank in the current run.
- A token that resolution created by copying a source token's coordinates can be
  stored relative to the start of its text: the offset, the line difference, and
  the column difference on that text's first line, rebased on load.
- A URI of the module's own file can be stored as a reference to that file.

Three cases refine this:

- **Export sets.** A module resolves its export sets and its result refers to their
  tokens, so an export set's text is part of its module's text in L, though the
  text of nested modules and imports is not (they are prerequisites, keyed by
  their results). The export set itself is a dependent of its module, and its own
  result refers to those tokens as its module's.
- **Tokens without an offset.** Resolution makes some tokens from another token's
  line and column only, leaving the offset 0 (the argument names of the
  constructor call a datatype update becomes, for example). Such a token is placed
  by its line and column and keeps offset 0 when it is rebased.
- **Copies of a prerequisite's text.** A refinement resolves a copy of the refined
  module, so resolving it makes tokens at the refined module's coordinates. Such a
  token is stored relative to the prerequisite's token at the same place, by that
  token's rank, when the prerequisite's result refers to it; otherwise the module
  is not cached.

The persistent resolution cache of issue 261 does all of these. A consumer of the
identity that keeps absolute coordinates, such as Boogie text with source
positions, must rebase them the same way or add where the module's text starts
to its own key.

## The interface

- `ModuleResolutionIdentity.Context` and `.Compute` compute it.
- `Program.ResolutionIdentities` (internal) has one entry per module declaration,
  next to `Program.ResolutionKeys`.
- `ModuleDefinition.ResolutionIdentity` holds the identity of the result that
  produced the definition, including the generated abstract signatures that share
  their owner's result.
- `ModuleResolutionResult.Identity` holds it on a worker's result.

It is computed on every resolution, before any module is resolved; it changes no
output. A change to what it covers changes the domain strings.

## Contract

With the same binary, two modules with the same identity resolve to the same
result, generated names included, up to source coordinates, which follow where
the module's pieces of source are (see above). That is what lets a
persistent cache reuse a result in another process. It does not cover the program
outside the module's prerequisites: whole-program admission (replaceable modules,
cardinality, the global rewriters) runs again on every resolution.

## Mutation controls

The persistent cache's entry key (`PersistentResolutionCache.Key`) adds to L the
build (`Producer`), the module's name, the naming prefix, its direct prerequisites
by role and name, and the snapshot structure of every module of its closure. A
mutation run removed one component at a time, and the controls,
`.github/review/resolution-cache.py` and the reuse-equals-fresh oracle over a lit
subset, had to fail for each:

| removed | caught by |
|---|---|
| L (source, options) | the comment and option scenarios |
| the options in L | the option scenario's output |
| the prefix | the prefix scenario |
| the structures, or the whole closure | the signature scenario (the snapshot no longer applies) |
| the build: `Producer`, the MVID in L's version, and the entry's own producer check | a cache warmed by one build and read by another hits instead of missing |
| the name: in the key, in L, and the entry's own name check | the cold run of the main scenario (two modules share an entry) |

The build and the name are each held three times (in the key, in L and in the
entry's manifest), so removing any one copy leaves them guarded and no control
fails; only removing every copy is caught. The direct prerequisites are
defensive: they follow from the module's own source (in L) and from the names of
its closure, and no mutation of them alone is caught.

## What a warm run costs

A hit must cost less than resolving the module. What a hit does, and what it is
kept from doing:

- **Builtin regions.** An entry names the builtin objects it uses as anchors, by
  region (a breadth-first walk from one builtin root) and index. Each region is
  computed once per run and view, with its guards (its size and a digest of the
  types in it), and every module of the run uses that copy. A region must hold
  builtins only, so a store (and `--resolution-cache-check`) scans each completed
  module's objects against the regions once, incrementally; a hit does not. Under
  `--resolution-cache-check` every hit also recomputes its regions afresh and
  reports a region that differs from the remembered one.
- **Owners.** A store must name, for each object of a prerequisite that its
  module reaches, that prerequisite and the object's index. The table from objects
  to owners is built only when a store or the check first asks, from the objects
  of the modules completed by then, sized once for all of them, and held as an
  open-addressing array of references and packed (module, index) numbers. A run
  in which every module hits never builds it.
- **Materialization.** Reading an entry fills its objects from one pooled array of
  entries and one of values per thread, with no record per object. Collections are
  created and filled through delegates made once per collection type, not by
  reflection per element, and a field's name is computed once.
- **Capture.** A store asks where an object outside its module is (a builtin, a
  named context object, an anchor or a prerequisite's) once per object, not once
  per reference, and an object already in the snapshot is recognized before that
  question is asked.

Fields are still read and written by reflection, in a capture and in a
materialization; accessors compiled per type would cut that, at the price of
compiling them in every process.

Each phase of a module's hit or store is reported in the `resolve.module` events
of `--pipeline-profile`, among them `cache.read`, `cache.decode`,
`cache.anchors`, `cache.guards`, `cache.materialize` and `cache.register` for a
hit; `store.anchors`, `store.capture`, `store.ownership` and `store.publish` for
a store.
