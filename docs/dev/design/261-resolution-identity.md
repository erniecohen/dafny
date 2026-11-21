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

The persistent resolution cache of issue 261 does all four. A consumer of the
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
