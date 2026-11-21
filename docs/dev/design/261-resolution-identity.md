# The per-module resolution identity (issue 261)

This is the stable key for one module's resolution result, for the persistent
caches of [issue 261](https://github.com/erniecohen/dafny/issues/261) (part of
[241](https://github.com/erniecohen/dafny/issues/241)): the resolution cache,
and the translation table, which maps it to the hash of the module's Boogie.

## What it covers

`ModuleResolutionKey.Compute` (L) is the semantic key from the owner's design
decision 1: the module's source text, its options, the producer version and the
keys of its prerequisites. A resolved module holds two more things that L does
not fix:

- **Generated names.** A module's generated names carry a prefix from
  `ModuleNamePrefixes`, which depends on the set of module names in the program.
  Adding an unrelated module whose name digest starts like this one's lengthens
  the prefix without changing L. The coordinator decision of 2026-11-18 on 241
  made the effective prefixes of the module and of each prerequisite part of the
  identity.
- **Source coordinates.** L hashes the module's text, not where it starts, so
  inserting a line above it moves every token without changing L.

```text
context(m)  = H("dafny-module-resolution-context-v1", prefix(m), location(m),
                [(role, name, identity(p)) for each direct prerequisite p])
identity(m) = H("dafny-module-resolution-identity-v1", L(m), context(m))
```

`location(m)` lists, for every node whose text is in L (the module, or each member
of the default module, or each declaration of a recursive module group), the URI,
line, column and offset of its first token. `H` is SHA-256 over a length-delimited
encoding, written as lower-case hex. Prerequisites are sorted by role, name and
identity. Taking the prerequisites' identities, not their keys, makes a change in
the context of a prerequisite a change in the identity of every module that
depends on it.

## The interface

- `ModuleResolutionIdentity.Context`, `.Compute` and `.Location` compute it.
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
result, generated names and source coordinates included. That is what lets a
persistent cache reuse a result in another process. It does not cover the program
outside the module's prerequisites: whole-program admission (replaceable modules,
cardinality, the global rewriters) runs again on every resolution.
