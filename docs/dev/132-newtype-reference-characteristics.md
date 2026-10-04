# Reference characteristics through existing newtype wrappers

A newtype name does not erase references carried by its representation. The owner allocation repair for [issue #132](https://github.com/erniecohen/dafny/issues/132) distinguishes ordinary reference involvement from references shown by a general function value's captured reads frame. Both queries must keep that distinction when they cross an existing sequence or generic newtype wrapper.

## Failure and correction

At owner PR #137 source `8848d92ffd079e0be6a9780e03ea824ee85c0d1d`, the newtype branch of `ComputeMayInvolveReferences` always returned false. In the existing-language regression, a fresh object is read by a general function, that function is placed in a `seq<() ~> int>` newtype, and a total lambda captures the wrapper. Treating the captured wrapper as reference-free omitted the capture's allocation premise. The verifier then proved allocation in the heap preceding the fresh object and accepted the unchanged `ensures false` contract.

The correction uses the actual instantiated visible base of the newtype. It forwards the original `generalArrows` selector and datatype traversal context rather than classifying a raw declaration, every formal argument, or an erased general-arrow head. An unresolved, cyclic, or hidden ancestry cannot establish reference freedom. A provided internal type-synonym head is classified only from its public `ContainsNoReferenceTypes` promise; its hidden RHS is neither substituted nor classified.

The production diff changes only `Types.cs` and `UserDefinedType.cs`. It adds no allocation axiom, handle schema, assumed heap fact, or reverse reads-to-allocation implication. Existing nominal membership/allocation equivalences and the owner one-way function allocation rules remain in place.

## Why the changed allocation proof is weaker

The nominal wrapper denotes values in its base carrier. For any revealed instantiated wrapper, references observable through the base remain observable through the wrapper. Therefore the wrapper cannot establish reference freedom when its actual base does not. Returning true for such a wrapper removes an unconditional reference-free shortcut and makes the existing lambda allocation rule require allocation of that captured value. It does not assert a new membership or allocation fact.

In the measured fresh-capture regression, the generated allocation introductions gain exactly the captured wrapper's existing `$IsAlloc(values, Wrapped, heap)` premise. The nominal/sequence/function allocation equivalences cannot establish that premise in the old heap that precedes the fresh capture. The old allocation assertion is consequently rejected before it can establish the false method contract. The unchanged pure and preallocated capture controls remain provable.

A pure actual base retains its previous false answer. For partial and total arrow subsets, stopping before subset erasure keeps their empty reads family while still querying their actual arguments and result. A general arrow continues to answer the two reference queries according to the owner's selector; the follow-up does not collapse them into one characteristic.

## Instantiation, cycle, and opacity boundaries

- The checked walk substitutes each entered newtype's actual arguments. A projection such as `Project<Reference,int>` whose base uses only its second argument remains pure, while the reversed actual contains references. A finite value such as `Id<Id<int>>` is not rejected merely because one raw declaration is encountered twice.
- Subset preservation is local to this query. Existing callers keep the default subset-erasing ancestor behavior. Stopping at a subset still checks the finite redirecting declaration graph rooted at every entered newtype and the stopped subset. This finds a mixed expanding cycle hidden behind an identity or parameter projection without repeatedly constructing larger actual arguments. Erroneous ancestry returns a checked failure rather than a primitive fallback.
- Hidden outer newtypes return the conservative answer before RHS substitution. A revealed outer may use an opaque base's explicit public reference promise, but cannot inspect its representation. The unit control makes hidden RHS substitution/classification throw, and the promise query succeeds without either operation. Changing scope or resolving a proxy triggers a fresh query; no reference-classification result is cached.
- The existing datatype traversal checks instantiated arguments before its visited-declaration cutoff. The newtype forwarding retains that traversal context, so a wrapper does not bypass the argument check when a datatype declaration is revisited.

These points justify this finite source change. They are not a consistency proof of the entire type system, and they do not exempt new lambda, handle, old-heap, or generic paths from review.

## Existing diagnostic evidence

These are public focused diagnostic results on the named sources, not execution of the newly registered basenames or a strict final gate for this package. All listed diagnostic compilers use Dafny 4.11.0 and Z3 5.1.0.

| Evidence | Exact source and measured outcome |
| --- | --- |
| [Owner-matching false control](https://github.com/erniecohen/dafny/actions/runs/37237811423) | `478d9500bff28cecfcc569c4e9b2784b0f6a64d7`; all `Source/` files match owner `8848d92...`. The fresh-wrapper false contract was accepted, 3 verified / 0 errors. Pure and preallocated controls were also 3/0. |
| [Initial corrected comparison](https://github.com/erniecohen/dafny/actions/runs/37237762485) | `fd54953a44b0416e80077962786d961a275f006f`; fresh capture rejected, 2 verified / 1 error; pure and preallocated controls remain 3/0. |
| [Complete universal controls](https://github.com/erniecohen/dafny/actions/runs/37239127347) | `69a9e21cca2078123f5d6915c9919d1f54bc272c`; triggered finite actuals 2/0, complete partial/total universal proof 7/0, and the false/unknown-capture controls 1/3. The three errors are the intended final false assertion and two unpromised old-allocation postconditions. |
| [Scoped opaque promise controls](https://github.com/erniecohen/dafny/actions/runs/37240203988) | `47ecde37abdff3e133627ccb09029cddcac3d21f`; focused Core filter 38/38, provided/revealed characteristic declarations 0/0, inhabited false control 0/1, hidden newtype and unpromised opaque declarations rejected. |

The complete universal fixtures call the checked `AllPureValues<T(!new)>` theorem with explicit allocation triggers. The earlier empty-body untriggered partial/total draft failed 0/2 with warnings and is not evidence of a proved universal contract. Its registered counterpart retains both original contracts and adds the checked theorem/calls; a new package gate must measure that corrected body under this exact owner base.

## Regression and acceptance scope

The package registers 16 existing-language bodies: 13 newtype/opaque characteristic controls and three fresh, pure, and preallocated sequence-capture controls. It does not admit the extended base categories from issue #113. Each original body and body-start line is retained; only the registration basename and feature-specific RUN comments change. The owner's compiler has no extended-base option, so the redundant second toggle RUN is retained as a non-executable comment. Shipped `--additional-axioms=false` remains explicit.

Expected status classes are carried as source proposals. No golden outputs are fabricated or mechanically reused. Record-only output capture, reasoned golden review, and the strict focused/full scratch gates must complete on the isolated owner-based branch before publication. Trusted queued verification under the separately required toolchain remains a distinct acceptance claim.
