# Producer-owned unsigned conversion in If guards (issue 124)

This is an English implementation and correspondence plan, not an implementation,
verification theorem or acceptance receipt. Its exact starting source is
`7b60f93c2cf81486178eb7fcd6fdc35f7e868c71`, independently of the diagnostic branch.
The existing [direct-wrapper plan](unsigned-wrapper-context-plan.md) and
[source checkpoint](unsigned-wrapper-source-checkpoint.md) remain the premises
for the exact F/N/A source shape, ordinary roots and two-phase substitution.
The proposed extension licenses only an actual producer-owned If guard through
its exact positive raw partition assumption. The original backend scope remains
open; this is a bounded next checkpoint.

## Actual diagnostic evidence

[The first diagnostic](https://github.com/erniecohen/dafny/actions/runs/37240827629)
on source `3fddfba72a07462a2db2886e7650987699775672` compiled and ran its tiny
runner but produced no unit receipts: its inventory rejected a LambdaExpr.
That failure is preserved. Job success was expected-failure delivery.

[The repaired diagnostic](https://github.com/erniecohen/dafny/actions/runs/37241390767)
on source `4c6ddda320f26c4d8e119f451ec47dbe4d30aeb8` captured all three original
units from one module. Its exact receipts are:

| Object | Bytes | SHA256 |
| --- | ---: | --- |
| Public artifact 11316872943 | 24577309 | `ccd45926086205a8358f0542d3e16c43fd0e0ccdc904f3f733ddb62214250a0a` |
| summary.json | 30289 | `4440c4e58097ce13538007332751ee74f1d0f2bcada05b7dcd3a77c0d295c949` |
| typed-source.json | 610305 | `9e508cb27cb5d482e379bd9b5fa6ed0a52f1207e6d89faac68776e5d0e79ff37` |
| Printed module-0.bpl | 103118 | `bf5c18d8be8211f86b25638663f353b44b6ae10800e866d2cd509e2a074c7a4e` |
| Runner DLL | 75776 | `0c2e3408746a0a6cf7aa66791d0803a04021a663bb8edc234c3ab18951be82d9` |
| Runner deps.json | 33073 | `3b9f138146ccd67485505fdd5858aab0a82e3ed78f374294d107fbb141631c36` |
| Runner runtimeconfig.json | 328 | `97c9700542b659150b230c3578b29530fd76ab01ec66a92cd16945e0245713df` |

The diagnostic source seal is
`b3d23bd84d948a4ef23ae840247973ed39740de6784327acf955328a558e5334`.
The before/after typed source inventory is identical, with SHA256
`8204999b72a24f3501e0545dd84624f24afed7b261d82c3149acc09ade26818f`.
All seven owned stages exited zero without cleanup signals, poisoned cleanup or
remaining children. Neither verification nor a solver was invoked.

The diagnostic reused only the compiler actually built in
[run 37236538953](https://github.com/erniecohen/dafny/actions/runs/37236538953),
source `7b60f93c2cf81486178eb7fcd6fdc35f7e868c71`. That gate failed 435/436
normalizer controls; its original failed summary is not relabeled. Public
artifact 11315592727 is 10380775 bytes, SHA256
`599958e1acbb7229345ef32bbf6a5ef260a322edb2ceba831bd66ced503406e5`.
All 89 flat compiler/runtime inputs match their recorded bytes both in the
captured input directory and the runner directory. In particular DafnyCore.dll
is 4581376 bytes, SHA256
`faa4d536aa04f1ebed24a155f6f5ca2a906f2a6fa4ac7a1961e0e47b65a4d468`;
DafnyB3Protocol.dll is 102912 bytes, SHA256
`2f258fe6e75dc4407b5d7522e1e8dfddb5458342588657e87c39cfb37464da37`.
The exact original fixture is 201 UTF-8 bytes, SHA256
`7a88712dd0e3f04549981d39986dd16f81898d9f431e4c24c2122d8f9e994b29`,
URI `file:///B3VisibilityTests.dfy`, unchanged default options and captured prelude.
No complete library, worker, proof verdict or default parity is reused.

| Original unit | Actual normalization | Original check inventory |
| --- | --- | --- |
| Impl$$_module.__default.Native | Unsupported at 2:18, no partial program | Four raw assertions: 2:12, 2:26, 3:22 and 3:3 |
| CheckWellFormed$$_module.__default.False | Success, one context | One conversion-fit obligation at 5:58 |
| Impl$$_module.__default.False | Success, one context | Two obligations at 6:3 and 7:3; explicit false assertion retained |

In the receipt's reference-ID space, the failing Native correctness unit is 773.
Its structured block 901 owns IfCmd 902 at `structured/block0/if/guard`, token
2:26. Guard 828 is `LitInt(0) <= nat_from_bv3(x#0)`. Its actual call 830 at 2:18
resolves to Function 562, a top-level body-free monomorphic bv3-to-Int wrapper.
Its argument is IdentifierExpr 837, declaration 838; this is not the distinct
procedure formal 906 merely bearing the same printed name.

The validated whole CFG and structured lists establish this concrete producer
mapping, beyond the diagnostic's flat expression matches:

- Raw block0, object 790, owns predecessor transfer 795 with exact targets 796
  and 797. Its four ordinary commands match structured block901 by reference.
- Positive assumption825 is raw/block1/command0 in block796. Its Expr is exactly
  guard828. Then-list904 has PrefixCommands `[825]` and anonymous first block905,
  so this is the producer's inlined positive prefix, not a discovered unrelated
  assumption. Transfer826 targets join block827.
- Negative assumption840 is raw/block2/command0 in block797; expression842 is
  the typed `<` complement with operands call830 and zero829. With no ElseBlock
  or ElseIf this is the producer's dedicated negative runoff block. Transfer841
  targets the same join827.
- Both assumptions have exact AssumeCmd runtime type and singleton empty
  `partition` attributes. Join block827 carries the four original assertions
  845, 846, 850 and 852, the range-learning assumption and conversion assignment.

Reference IDs identify observations inside this one receipt. Production
certificates must use original object references and occurrence paths, never
these integers, names, generated-label spelling or token coordinates. Flat
positive/negative expression matches alone are not producer or G3 certificates.

## Pinned source and the prefix/guard distinction

Pinned Boogie is `73a0e214a87df85fc058270268c1d0706fd05bc9`.
[CreateBlocks](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/Core/AST/StructuredBoogie/BigBlocksResolutionContext.cs#L480)
creates the positive and complementary negative assumptions, then either
inlines a prefix or creates a dedicated block. Its else-if predecessor carries
the previous If's negative assumption. [PrefixFirstBlock](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/Core/AST/StructuredBoogie/StmtList.cs#L92)
inlines only into the anonymous first block and preserves the actual commands.
CreateBlocks:347-368 consumes PrefixCommands exactly once before simpleCmds.

[B3StructuredCfgCorrespondence](../../../Source/DafnyCore/Backends/B3/B3StructuredCfgCorrespondence.cs)
currently checks the entire producer reconstruction, including exact ordinary
commands, block order, transfers and targets. CommandShape:25 drops If ownership;
Prefix:154-160 validates an inlined guard then describes it as an ordinary
original command. Create:199-223 handles dedicated, inlined and else-if routes.
The extension must retain ownership through every one of these paths.

[B3Normalizer.Structured](../../../Source/DafnyCore/Backends/B3/B3Normalizer.cs)
at 1267 normalizes PrefixCommands with ordinary Command/InCommand. It normalizes
then/else bodies first, and at 1306 independently normalizes the guard expression.
The latter has no active command scope, causing the actual failure. Do not remove
or reuse the already-normalized positive prefix. Its ordinary scope and the new
If-guard scope must have distinct invocation identities and distinct target
applications, even when they refer to exactly the same raw assumption825,
guard828 and source call830. The negative prefix, when present, likewise retains
its own ordinary command witness. Guard capture must surround only Expr(Guard),
not body normalization or a later arbitrary expression visit.

## Proposed internal records and APIs

No protocol, worker, vendor library, public verification result or option change
is required. The following are proposed internal APIs, not existing declarations.

1. `B3StructuredCfgCorrespondence.DescribeIfGuards(unit)` performs the same complete
   validation and returns a privately minted `IfGuardCatalogue`. Existing public
   `Validate(unit)` delegates to it and discards its result. The catalogue binds
   the exact Implementation and is keyed by IfCmd reference identity.
2. `IfGuardCertificate` binds that unit, actual IfCmd, original Guard, containing
   StmtList/BigBlock and chain position, exact source occurrence path, original
   Thn/ElseBlock/ElseIf objects, predecessor raw slot/transfer, and positive and
   negative `RawGuardSlot` records. A raw slot contains actual Block, block index,
   command index, exact AssumeCmd, original Expr and partition attribute object.
   It also records the role: inlined then/else prefix, dedicated branch, else-if
   predecessor, or negative runoff. Snapshot required original branch targets and
   enclosing runoff references. Constructors do not make Boogie nodes.
3. `SourceStep` records actual source container objects plus child indices and
   typed roles. Its bounded canonical encoding is the source occurrence path.
   A shared expression is allowed; a shared/cyclic structured owner remains
   rejected by the existing complete validator. No path resolves by label/name.
4. `B3UnsignedWrappers` accepts this catalogue for the new route. Keep the existing
   two-argument constructor/direct-only behavior for focused old helper callers,
   or migrate those callers explicitly without manufacturing certificates.
   `InIfGuard(owner, normalizeGuard, thenStatement, elseStatement)` creates a
   fresh guard Scope, indexes the certificate's exact positive raw root, calls
   normalizeGuard once, constructs the Conditional and attaches a fresh
   `ConditionalAnchor`. Its active scope is restored in finally. Missing or
   foreign certificates cannot borrow an ordinary or enclosing scope.
5. A guard Scope holds its RawRoot, certificate and a unique invocation token.
   Existing per-call Witness continues to bind exact source call/path/argument,
   resolved shape and newly returned Ir.Application. Its new anchor binds the
   exact original Conditional and Condition references. Source-call dictionaries
   remain per-scope, not global sets. Existing ordinary Anchors retain their
   selected-check/learning/assumption/assignment rules.
6. `BindOriginalGuardOccurrences(original)` runs only after the full original
   normalized tree and all prologue/body/exit wrappers exist. It assigns each
   ConditionalAnchor its exact statement occurrence path and the complete set
   of that witness application's expression occurrence paths within this
   condition. This accommodates legitimate expression copies inside one owned
   condition without transferring them to another owner. An ambiguous shared
   Conditional statement at multiple original positions rejects this route.
7. `RecheckIfGuards(capturedCatalogue)` reconstructs and validates the complete
   current CFG before Lower. For every captured guard scope, it compares actual
   source-path containers/indices, owner/branch objects, guard and raw slot
   identities, positions, attributes and transfers/targets with the capture.
   It also checks the bounded source field snapshot described below.
8. `AvailableGuard(witness, contextStatement, contextPath)` requires the certified
   source pair, a matching original Conditional occurrence path, the exact
   original Condition reference and the exact application's original expression
   occurrence path/reference. This is an additional narrow role in Available;
   it does not make every Conditional a valid root.

Normalizing a guard without eligible wrapper calls needs no unsigned witness.
A null guard remains the existing Choice. The initial new guard route is limited
to If owners outside While ancestry: it does not license While.Guard, invariants
or a new loop origin indirectly through an If. Existing direct raw-command
support remains unchanged. No specification, where predicate, call argument or
contract, actual Body/lambda occurrence, synthetic IR guard or selected
source-definition formula obtains a new source-call path.

## Complete producer mapping

The validator should add owner/polarity/role metadata to descriptive CommandShape
records, not search the finished CFG for matching expressions. During Create,
carry an incoming prefix description alongside prefixGuard/prefixNegated and the
actual source path. During Prefix, validate its sole actual assumption and retain
its owner metadata. When constructing the first source block's expected command
list, keep both Original identity and guard role on this prefix entry. MatchGuard
and ordinary ReferenceEquals validation must both hold for that entry.

For a dedicated then block, its descriptive positive guard carries the current
If owner, and Add supplies its actual raw block/index. For an inlined then, the
first prefix entry supplies the same positive slot. ElseBlock is symmetric for
the negative slot, either inlined or dedicated. With no else, the dedicated
negative runoff supplies the slot and exact successor. In an else-if chain,
the previous owner's negative CommandShape becomes the next predecessor's
command; retain that previous owner rather than assigning it to the next If.
The next predecessor transfer is bound to the next owner's attributes and
then/else targets. Each chain member receives its own pair and source path.

Only after all expected and actual blocks, commands and transfers match may a
pair be exposed. Require one positive and one negative slot for each non-null
eligible If guard, unique raw command identities, the exact complete owner-path
mapping and no leftover descriptive slots. Do not issue a partial catalogue when
later validation fails. The existing finite MatchNegation rules describe the
pinned Expr.Not simplifications and subsequent Bool typecheck rewrites; they are
not an invitation to create or simplify a source expression.

## Source capture and fresh mutation recheck

A certificate is not an assertion that mutable Boogie objects stay unchanged.
Capture a bounded read-only field view of each licensed guard expression and its
call arguments, including exact constructors, expression and child references,
typed operator/opcode or FunctionCall.Func, type instantiations, literal values,
identifier declarations, old/extract/concat indices, binder/let variables and
expression-valued metadata. Use typed pinned API fields, not reflection-based
structural equality, unbounded ToString, names or a generic ContentHash. Any
source constructor not covered by the audited view rejects this new route.
Read-only snapshots of an existing referenced Body/definition may detect changes;
they never add a SourceChildren root or license a wrapper inside that Body.
The source declaration/argument stability premises of existing expression and
Body correspondence remain explicit, rather than being inferred from a cache.

Before Lower, compare those field views and the complete fresh producer mapping.
Changing both guard and negative complement coherently still fails if their
semantics/fields differ from capture; pointer equality alone is insufficient.
Changing Thn/ElseBlock, moving a command while rebuilding an otherwise valid CFG,
replacing partition attributes, or borrowing another owner's pair also fails.
Existing RecheckSourceCalls must independently find each captured source call at
its exact positive raw expression root/child path, with its original Func,
monomorphic signature, argument reference and any IdentifierExpr.Decl unchanged.
Existing complete F/N/A ownership, formal, trigger, body and nonhideability
rechecks remain in force. Failure rejects the unit, without partial output or a
new logical assumption.

The source argument stability check must audit all supported argument fields;
a newly added guard test must not silently rely on an argument object's reference
if its semantic children can change. Scope of this defensive view is the new
guard route. It must not widen an old unsupported expression or turn a snapshot
of a declaration into a declaration-availability certificate.

## Original paths and G3 replay paths

Use the existing Walk grammar, not paths reconstructed from tokens. Statement
root is `body`; ordered children append `/stmtN`. The Conditional condition is
`/expr0`; ExpressionChildren append ordered `/N` segments, including complete
word arguments, let value/body and quantifier pattern/body traversal. Source
paths and target paths have separate typed purposes. Their encodings and resource
charges are checked before storing a receipt.

At BindOriginalGuardOccurrences, locate anchors by original Conditional reference
and require their exact Condition reference. For each witness, record every
original full expression path at which its target Application actually occurs in
that condition. If two distinct If owners share the source Guard/NAryExpr,
normalizing each must produce different scopes, anchors and target applications.
Even if their target expressions are structurally equal, neither owner can use
the other's path/reference receipt. The positive prefix's ordinary witness and
If condition's guard witness also remain distinct.

[B3DefinitionContexts.Replay](../../../Source/DafnyCore/Backends/B3/B3DefinitionContexts.cs)
at 106-113 clones Conditional records while preserving their Condition reference.
It preserves ordered child positions; an unselected Check becomes Assume or an
empty Block at the same leaf position, not a deleted sibling. Therefore the
original Conditional statement path and its expression paths remain meaningful
in every preliminary replay. Do not compare replay Conditional record identity
with the original, and do not match merely a globally shared Condition reference.

Lower first executes unchanged ValidatePartition on the fully opaque original
and every preliminary context. For each wrapper occurrence in each context,
require its exact Witness and either its existing ordinary availability rule or
its new original-path guard rule. Guard availability requires all of:

- the same source owner certificate and freshly rechecked positive raw root;
- the exact original Conditional statement path in this replay;
- the exact original Condition and Application references at the registered
  expression occurrence path, with expected Bool/Int/word sorts;
- an independent receipt keyed by this mask and full path; another mask or path
  cannot supply it.

A shared application copied inside the same original condition needs every
registered occurrence checked in every surviving context. Copying it into a
second Conditional, changing Condition to an equivalent clone or replacing its
application with another owner's structurally equal application rejects. A
selected definition formula still has no direct/guard root and must reject an
uncaptured wrapper. Removed unselected nonlearning checks need no receipt;
unselected learning checks retain only their already-reviewed ordinary rule.

After all surviving occurrences are independently certified, perform the existing
deterministic unsigned substitution. Retain original statement constructors,
child order, check IDs, learning, declarations and state. Recreate contexts from
the identical definition selection and retain unchanged ValidatePartition and
ValidateContexts/narrow exhaustive field comparison before/after substitution.
No generic expression-equivalence exception is added.

## Retained-root premise and conditional model argument

The precise availability premise is still source-to-G3 correspondence: the
certified positive partition assumption is retained in the source control context
corresponding to this validated preliminary replay. G3 retains each Conditional
and all ordinary branch assumptions while selecting/removing checks as specified.
The new owner/path certificate identifies the matching original raw root; it does
not claim arbitrary native VC path splits retain every original guard. If that
corresponding root is absent, the guard route has no availability certificate.
Pruning disabled does not erase the source-origin requirement.

Under this premise the pinned
[ReadOnlyVisitor](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/Core/StandardVisitor.cs#L1046)
and [PruneBlocksVisitor](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/VCGeneration/Prune/PruneBlocksVisitor.cs#L11)
visit the exact positive assumption Expr and root the actual Function F.
The already-reviewed sole positive singleton trigger gives F -> A and A -> N.
[Pruner.GetLiveDeclarations](https://github.com/erniecohen/boogie/blob/73a0e214a87df85fc058270268c1d0706fd05bc9/Source/VCGeneration/Prune/Pruner.cs#L61)
follows F -> A because A.CanHide is false. This is exact source dependency
reasoning, not a name-based guess, a new axiom or a Pruner mutation.

For any model of that retained source context, its existing universal A equates
F(b) and native unsigned N(b) for every full W-bit value. For each certified call,
existing typed argument correspondence gives the same argument value. Replacing
that occurrence with BitvectorToUnsignedInt thus preserves its Int value.
Structural expression correspondence preserves the complete Bool guard value,
so the same then/else branch is selected. The pinned complementary assumption has
the opposite truth value. An inlined positive Assume is deliberately retained;
when entering the then branch it tests the same condition before body state
changes. Its independent substitution witness preserves that value too. A
dedicated positive assumption is represented by the corresponding Conditional
branch constraint, even when no separate target Assume occurs in the body.

Every check and original fit/range condition remains at its original execution
position. Substitution adds neither range nor equality assumptions. Existing
source-command/control/G3 correspondence consequently carries a source
countermodel to a target countermodel; normalized validity implies source validity
conditional on those correspondences and pinned primitive semantics. This is an
English universal composition argument, not a mechanized end-to-end B3 theorem
or a proof that the executing verifier implements an upstream abstract model.

## Admission and resource invariants

Retain all original width/word/numeric/node/depth/message and context limits.
Use reference-identity catalogues and validate counts before allocation. The
complete raw CFG remains at most 256 blocks; each guard pair has its own producer
then entry, so at most 256 pairs can be admitted. Source container depth stays
below 128 and source node/recheck traversals have aggregate 100000-node ceilings.
Use separate bounded capture/recheck counters and charge repeated occurrences;
shared source objects do not cancel the cost of distinct invocation paths.
Guard/source snapshot metadata and source witness paths together are bounded by
the existing 32 MiB message ceiling. Unknown or oversized shape rejects; never
truncate a source tree, omit a root, raise a ceiling or manufacture a premise.

Original IR still passes CheckOwnedBounds before context selection. New original
anchor and each replay inventory use the same complete bounded walks. There are
at most 16 contexts, 200000 aggregate nodes and 64 MiB aggregate receipts/replays.
Final unsigned replacements retain the complete output bounds, including every
copied word argument, before request serialization. Catalogue and snapshots are
local normalization state, not new worker wire data or logical facts.

## Planned control denominator and checkpoints

These are planned rows, not implemented/discovered/executed results. Put new
source controls in B3IfGuardCertificateTests. G01 moves the existing typed If row
from NonDirectRootsCannotBorrowAProgramShape into a supported producer-owned
control without altering its program, branches or two checks. All other old rows
remain. G02-G34 add 33 rows; the exact original actual-Dafny three-unit test remains
and strengthens its 4/1/2 obligation inventory checks without changing its source.

| Row | Required control |
| --- | --- |
| G01 | Existing typed If case becomes certified; keep both original branch checks |
| G02 | Inlined positive branch with no else and exact negative runoff |
| G03 | Both branches inlined, preserving both ordinary partition prefixes |
| G04 | Dedicated positive block for a named first then block |
| G05 | Dedicated negative block for a named first else block |
| G06 | Both partition blocks dedicated |
| G07 | Inlined else-if chain: previous negative belongs to previous owner |
| G08 | Else-if with dedicated branch and exact predecessor/topology |
| G09 | Nested non-loop If owners have distinct bounded source/IR paths |
| G10 | Empty then branch as actually emitted for Native's range well-formedness |
| G11 | One source Guard/call object shared by distinct If owners yields separate witnesses |
| G12 | One raw positive prefix and guard share source objects but have distinct scopes/target applications |
| G13 | Attempted target Application identity reuse across those scopes rejects |
| G14 | Shared raw-command/guard source call cannot supply a third unowned invocation |
| G15 | Two G3 definition masks independently certify retained condition and prefixes |
| G16 | Unselected learning Check retains its ordinary source rule alongside guard receipts |
| G17 | Removed nonlearning Check has no receipt and cannot root another retained occurrence |
| G18 | Positive raw assumption expression mutation after capture rejects |
| G19 | Negative complement mutation after capture rejects |
| G20 | Positive partition attribute replacement/malformation rejects |
| G21 | Negative partition attribute replacement/malformation rejects |
| G22 | Raw block removal/reordering/borrowed branch pair rejects |
| G23 | Coherent source/raw command relocation still fails captured slot/path identity |
| G24 | Coherent guard/operator-plus-complement mutation fails field snapshot |
| G25 | Guard call argument/declaration/semantic-child mutation fails source recheck |
| G26 | A foreign unit/owner cannot supply an otherwise matching guard certificate |
| G27 | Another original Conditional path cannot borrow a structurally equal captured application |
| G28 | A cloned equivalent Condition cannot replace the original replay Condition reference |
| G29 | Actual Body-chain wrapper call inside a guard retains Unsupported behavior |
| G30 | If guard within While ancestry gains no new loop root |
| G31 | While guard and its generated raw assumptions gain no If certificate |
| G32 | Sharing a certified guard expression with a procedure spec does not license the spec |
| G33 | Source/anchor path or snapshot admission overflow rejects without truncation |
| G34 | Reachable unconditional false check after a certified guard remains present |

The projected focused selection is 34 guard rows plus the unchanged original
actual-Dafny test, 35 rows. Existing unsigned rows are 83: relocate one, then add
34 guard rows, producing 116 combined unsigned/guard rows. Existing whole
normalizer denominator is 436; net +33 projects 469. These counts must be recounted
from the frozen implementation and then checked against actual discovery/TRX;
no CI denominator changes precede that recount.

Add two actual-Dafny corpus cases only: a positive compound range/round-trip
assertion at width67, and a satisfiable width3 input with the same compound guard
followed by unconditional assert false. Keep original guards, additional-axioms
false, every old case and strict failure classifications. Existing 65 manifest
cases plus 12 driver controls total77; the proposed two cases project 67+12=79.
The negative must actually report Failed, never Unknown, Unsupported or ToolError.
No selected unit, WF check, conversion guard, assertion or old expected case is
dropped to meet these numbers.

Source implementation checkpoints after review are: (1) producer catalogue,
field recheck and focused owner-mapping controls; (2) isolated guard capture,
complete original-path binding and G3 per-occurrence availability controls;
(3) actual-Dafny check inventory/corpus/news and accurate approximation wording.
Keep each source checkpoint reviewable; no vendor/schema/primitive rewrite.
The coordinator first runs a frozen compiler/contracts focus, then inspects its
complete actual receipts before authorizing the complete applicable gate.
Trusted proof work retains pinned Dafny4.11.0 and Z3 5.1.0. Complete library/worker,
corpus, native compatibility and backend acceptance remain separate measured
gates. This English-only checkpoint changes no production code or verification
contract and asserts no new source or target fact.
