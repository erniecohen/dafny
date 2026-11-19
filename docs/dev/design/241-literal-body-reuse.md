# Literal-body snapshot detachment review

The literal-body extension of [PR 519](https://github.com/erniecohen/dafny/pull/519)
is blocked at the current materialization boundary. The resolved cloner retains
old semantic identities and drops other semantic state. Literal modules therefore
still bypass the persistent cache; the alias profile is unchanged and does not
meet the requested performance acceptance criterion.

This is an implementation prerequisite, not a claim that persistent typed bodies
are impossible. The complete object-table codec proposed by
[PR 282](https://github.com/erniecohen/dafny/pull/282) must address the state below.
Wrapping the current cloner in the alias container does not implement that codec.

## Why the editor cache does not establish detachment

[`CachingResolver.ResolveModuleOutput`](../../../Source/DafnyLanguageServer/Language/CachingResolver.cs#L72-L95)
requires unchanged parse identity and reference-equal prerequisite results. Its
[`PrepareResolutionSystemModule`](../../../Source/DafnyLanguageServer/Language/CachingResolver.cs#L62-L69)
retains builtin identities across resolutions. A fresh process satisfies neither
condition merely because source, options, dependency keys and naming prefixes
match. Those inputs certify semantic identity, not object identity.

The alias profile binds its one target to the current prerequisite signature. A
literal body needs exhaustive rebinding of both owned and external typed edges.

## The blocking state

**Declaration targets and shared inference objects.**
[`FunctionCallExpr` cloning](../../../Source/DafnyCore/AST/Expressions/Applications/FunctionCallExpr.cs#L128-L145)
retains the original `Function`, both type-application lists and `AtLabel`.
[`Expression` cloning](../../../Source/DafnyCore/AST/Expressions/Expression.cs#L102-L107)
retains `Type` and `PreType`.
[`Cloner.CloneType`](../../../Source/DafnyCore/AST/Cloner.cs#L315-L329)
retains an inferred proxy's assigned target and removes refinement wrappers.
[`DPreType`](../../../Source/DafnyCore/Resolver/PreType/PreType.cs#L269-L279)
also holds declaration and argument-list references.
[`UserDefinedType` cloning](../../../Source/DafnyCore/AST/Types/UserDefinedType.cs#L77-L84)
uses [`GetCloneIfAvailable`](../../../Source/DafnyCore/AST/Cloner.cs#L30-L38),
whose fallback is the original declaration; its map is populated for type
parameters, not an exhaustive module declaration graph.
[`Type equality`](../../../Source/DafnyCore/AST/Types/UserDefinedType.cs#L252-L262)
compares resolved declaration identities. Substituting a module root cannot repair
these edges. Owned declarations, variables, labels and generated nodes need object
IDs; external edges need exact current prerequisite selectors. Sharing and cycles
must survive without old objects. Source origins also need rebinding:
[`Cloner.Origin`](../../../Source/DafnyCore/AST/Cloner.cs#L636-L642) returns the original.

**Visibility is a shared logical graph.**
[`VisibilityScope`](../../../Source/DafnyCore/AST/VisibilityScope.cs#L9-L17)
stores private process-allocated numeric tokens; overlap determines visibility.
Its [`constructor`](../../../Source/DafnyCore/AST/VisibilityScope.cs#L94-L98)
allocates distinct tokens even for identical debug names. Replaying old numbers
can associate a declaration with a different scope in another process; rebuilding
by name merges scopes that should be distinct.
[`Declaration`](../../../Source/DafnyCore/AST/TypeDeclarations/Declaration.cs#L32-L43)
has separate opaque and revealed scopes which its clone constructor does not copy.
[`InheritVisibility`](../../../Source/DafnyCore/AST/TypeDeclarations/Declaration.cs#L116-L131)
instead shares scope objects, so subsequent augmentation affects both declarations.
Signatures and export processing join these relationships in
[`RegisterTopLevelDecls`](../../../Source/DafnyCore/AST/Modules/ModuleDefinition.cs#L867-L874)
and [`ResolveModuleExportScopes`](../../../Source/DafnyCore/Resolver/ModuleResolver.cs#L666-L757).
A portable codec needs logical scope IDs, owned/external edges and fresh token
allocation preserving sharing and both visibility modes. Overlap memoization and
synchronization objects must be discarded. No resolved-clone API performs this.

**Builtins must be the current interned objects.**
[`ResolutionBuiltinCatalog`](../../../Source/DafnyCore/Resolver/ResolutionBuiltinCatalog.cs#L11-L80)
interns arrays, arrow families, tuples, bitvectors and pretype declarations within
one resolution lifetime. Worker views share these objects through
[`SystemModuleManager`](../../../Source/DafnyCore/AST/SystemModuleManager.cs#L88-L178).
[`GetBuiltinTypeIdentities`](../../../Source/DafnyCore/AST/SystemModuleManager.cs#L156-L163)
uses reference equality. A resolved clone of an `array2<int>` formal retains the
old non-null declaration and array class; a fresh manager's array class is a
different object. The materializer must replay builtin demands through the current
catalog and bind every type/member/signature edge to its objects, including
non-null companions and arrow variants. Copying prior `Builtins` or
`SystemClassMembers` imports old identities rather than reconstructing them.

**Derived module state affects verification.**
[`ModuleDefinition` cloning](../../../Source/DafnyCore/AST/Modules/ModuleDefinition.cs#L108-L164)
does not restore `CallGraph`, `InterModuleCallGraph`, `AccessibleMembers` or
`SuccessfullyResolved`. Function cloning also loses `IsRecursive`.
[`Function.IsFuelAware`](../../../Source/DafnyCore/AST/Members/Function.cs#L295)
depends on recursion; [`call translation`](../../../Source/DafnyCore/Verifier/BoogieGenerator.ExpressionTranslator.cs#L1128-L1136)
consults fuel awareness and SCC membership. The complete result additionally needs
signatures, default/export views, class-member maps and lowered/rewritten bodies.
The capture boundary follows
[`LiteralModuleDecl.ResolveInPhases`](../../../Source/DafnyCore/AST/Modules/LiteralModuleDecl.cs#L149-L293),
including cyclicity, decreases and post-resolution rewrites, and precedes current
global admission. Re-running these phases to reconstruct omitted state would still
resolve the body and cannot be reported as a body hit.

## Reproduction and evidence boundary

Four focused diagnostic probes exercised the refreshed resolver. Three inspected
the result immediately after `resolve()` inside `ResolveModuleOutput`, before
returning to global passes, using this source:

```dafny
module Body {
  datatype D = D(value: int)
  function Id(d: D): D { d }
  function Use(d: D): D { Id(d) }
  function Count(n: nat): nat decreases n {
    if n == 0 then 0 else Count(n - 1)
  }
  function Read(a: array2<int>): int
    requires a.Length0 > 0 && a.Length1 > 0
    reads a
  { a[0, 0] }
}
```

After `new Cloner(cloneLiteralModuleDefinition: true, cloneResolvedFields: true)`
and `CloneModuleDefinition(original, original.EnclosingModule)`, the probes found:

| Probe | Observed state |
| --- | --- |
| Calls and inference | Cloned `Use` calls original `Id`; resolved type, pretype and function type-argument list are shared with the original call |
| Visibility and recursion | Original `Id` is revealed in its module scope; cloned `Id` is not revealed in the clone's scope. Original `Count.IsRecursive` is true and clone's is false; original call graph is nonempty and clone's is empty; clone is not successfully resolved |
| Builtins | Cloned `Read` retains the original array identity, distinct from a freshly created manager's array class |
| Scope identity and sharing | Two `new VisibilityScope("same")` scopes do not overlap; `InheritVisibility` observes subsequent augmentation of its source's scope |

All four observation probes passed. Three separate negative hypothesis runs
required the cloned call to target cloned `Id`, the cloned declaration to be
visible in the clone's scope, and the cloned array edge to identify the fresh
builtin. Each failed its intended assertion. These are detachment probes, not
proof-verdict regressions, new product checks or loader mutations. An initial
builtin probe was corrected to unwrap the non-null companion: the catalog's
identity set contains the array class rather than that companion.

No body snapshot was enabled, and no new performance or full integration result is
claimed. Prior alias validation does not establish the requested body speedup.
Product code and the option-off, identity, CRC, plugin and naming guarantees remain
unchanged. The existing cloners retain their intended uses; these observations do
not establish a defect in those uses or in the alias loader.

## Prerequisites to resume

A closed typed-graph schema and materializer must account for all four categories,
with unknown fields and constructor kinds causing misses. The existing conservative
source/options/dependency/naming identity, including coexisting context-qualified
files, remains necessary but cannot replace graph reconstruction.

Resume with fresh-process and mixed hit/miss dependency graphs; mutate changed
import/export views, builtin demands, visibility edges, generated and recursive
declarations, source origins, snapshot bytes and every new invalidation check.
Compare Boogie and per-VC resource counts. Acceptance remains exact option-off
identity, zero candidate-only failures and a warm Std resolution median at least
30% below the cold median over five pairs. The PR remains a draft.
