# Separate source-only unavailable metadata contract

This private draft implements the separately reviewed English proposal. It is not
runtime acceptance, proof enablement, or a successful strict-availability gate.
The strict 32-file checkpoint remains unchanged at source-manifest SHA256
`068a94b8112f0770f94760ce53548af47ac327301483d9fde7594d2284805491`.
All original 19 qualified lifecycle source bytes remain unchanged here too.

The new contract reports complete common-library AssemblyRef inventory and
`completeMetadataAvailability=false`. Precisely one edge is classified unavailable:
Reactive owner bytes `19f0112c...`, WindowsRuntime version 4.0.0.0/token
b03f5f7f11d50a3a, metadata flags zero, and its one EventRegistrationToken TypeRef.
Every actual common file still has the exact identities, bytes, ordinary resolver
selections and Default Assembly objects required by the strict design. Source and
package origin observations are separately pinned; publisher source declarations
are not signed build-origin attestations.

Preflight inspects PE signatures in every package file, regardless of extension,
and builds bounded managed-file catalogs for both complete packages before
classifying absence. Cultures and runtime-specific assets can repeat names; every
file stays in the catalog. Both ordinary resolver queries must return null, every
managed catalog entry must have a different simple name, and actual TPA must lack
that name. No other missing edge is allowed. Full resolved edges plus the unavailable
record must exactly account for every common owner's AssemblyRef count. Nothing
unavailable is assigned a resolved identity, loaded Assembly, or object slot.

Both Default.Resolving and the actual private Load override check that simple name
before fallback. Any actual request, including another version or identity spelling,
poisons the scope and writes a bounded detached demand record under the acceptance
lock before throwing the exact denial. The record names the known metadata owner;
it does not pretend to reconstruct the requester's managed call stack. A caught
exception cannot restore acceptance. Native proof acceptance would require no
poison and zero demands, but both the fixed smoke Run API and RunVerification are explicitly disabled in this draft.

Default audits still check identity/path/hash and stable reference identity for
all admitted objects. Private audits check only exact package files and forbid
shared, TPA and unavailable copies. The ALC set admits exactly Default plus the
current owned private context, then only Default after actual weak collection.
A bounded AppDomain.AssemblyLoad ledger also observes transient context loads:
unknown context/origin/copy poisons the scope even if an assembly later unloads.
Only Default objects are retained by identity maps. The runtime-load ledger is
strings and values, so it cannot itself pin private assemblies. Its callback does
not depend on a notification exception escaping; poison is authoritative.

Final audit, snapshot and retirement of the two owned callbacks share the same
lock. A runtime callback captured before unsubscription that arrives after
retirement exits the disposable nonproof host with failure 126. No host may be
reused. This termination rule is confined to this new scope; original argument
controls and lifecycle implementation are unchanged. The future proof-mode and
coordinator lifecycle argument still require separate source review and controls.

Attribute receipts read bounded assembly metadata without instantiating product
attributes. The private nonproof control loads exact DafnyCore metadata, rejects a
module initializer, never enters DafnyMain or MainWithWriters, and never initializes
a scheduler. Its core identity/version/hash must pass before the intended denial.
The context is retired/unloaded in finally; a noinline frame returns only detached
facts and a long weak reference. Collection is checked for at most ten seconds,
including a failure path. Default/Reactive controls create no private ALC and report
that distinction. All controls use a process watchdog of 45 seconds: uncancellable
loader/JIT timeout exits 124 and cannot produce an accepted host exit. No solver or
cgroup is involved in these three controls.

Exactly one fixed control runs in each independent disposable host:

1. `default-unavailable-demand`: supported Default LoadFromAssemblyName request.
2. `private-unavailable-demand`: request through the actual owned product Load
   override, after pinned nonempty private DafnyCore metadata/audit.
3. `reactive-event-unavailable-demand`: begin invoking the unchanged exact public
   Reactive Observable.FromEventPattern(Type,string) against a static event with
   explicit add/remove accessors and no subscriber storage. A denial while selecting
   the method is insufficient. Only an actual matching denial after invocation
   starts passes; if the real runtime never demands the reference, the control fails.

Each requires the exact exception chain and one matching denial fact, immutable
poison even after catch, no unexpected failure, exact Default/private/runtime/ALC
audits, no children, unchanged framework witness, and actual private weak collection
where a private context existed. Failed setup, unrelated I/O, generic exception or
audit failure cannot satisfy an expected denial. Receipts require final demand state,
not an earlier snapshot, and every runtime load must be validated. The host exits
after the control; none proceeds to native proof.

The separate UnavailableMetadataProgram accepts only `--inputs ABSOLUTE_JSON_PATH`.
Its JSON has a fixed control selector, baseline/candidate pins, source manifest pin
and new receipt path. No Dafny argument array, solver, flags, budgets or verification
input is accepted. Existing Program.Main and its StartupObject remain unchanged;
a reviewed CI build-time override would be required to invoke the new controls.
Source-manifest and compiler product origin must also be inspected by that future
coordinator. The helper unavailable-metadata-receipt-validation.py is a proposed
read-only receipt validator, not an executable coordinator. The old strict helper
continues to reject this new contract and is retained for strict checkpoint review.

Source review and actual three-control qualification are still required. Boogie
common state remains persistent, fresh Boogie state is not established, no native
query/resource parity is claimed, and repeat/interleaved/reversed-order controls
remain mandatory later. The supported caller-owned factory/API alternative is
unimplemented. No binaries/packages, solver flags, query bytes, hashes, seeds or
resource values have been rewritten.
