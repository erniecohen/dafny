# User-defined operators, M1 implementation boundary

This implements the source-only first milestone of the
[reviewed design at 11dd7345](https://github.com/erniecohen/dafny/blob/11dd7345fc8f8559aba974ff113228bee8fa7fe9/docs/dev/design/194-operators.md),
following its [review](https://github.com/erniecohen/dafny/issues/194#issuecomment-6807326113).
The option is `--user-defined-operators`, default false. It requires
`--type-system-refresh`; its accepted surface and exclusions are documented in
the Reference Manual. Its experimental help is listed by `dafny verify --help-internal`;
ordinary automatic usage must remain byte-identical on existing CLI errors.
The modular-newtype example belongs to the later override
milestone, so M1 includes the rational and matrix examples.

## Semantic boundary

The parser adds `OperatorSyntaxExpr` only when enabled. Equality, generated
primitives and calc connectors retain their existing representation. Module
attribute arguments are recursively restored to intrinsic syntax before their
legacy resolution phase. No terminals or parser alternatives are introduced.

An operator obligation resolves operands once, captures the ordinary call context,
and waits for ordinary type constraints to determine the receiver head. It does
not enumerate possible owner types or use a candidate's result to invent a
receiver. The chosen function is passed through the same member-selection and
actual-parameter finalization helpers as a named function call. An ordinary
`FunctionCallExpr` is the resolved child. No user-selected expression receives a
primitive resolved opcode, new axiom, intrinsic implementation or algebraic law.

`UnderspecificationDetector` freezes the choice before conversion to final types.
`PreTypeToTypeVisitor` and `TypeRefinementVisitor` already traverse/fuse concrete
syntax through its resolved child; they need no operator branch. Call-graph,
termination, reads, ghost checking, verification and compilation consequently see
ordinary calls. The separately retained printable root is never visited as a
semantic binary expression. Its clone reconstructs that root directly, avoiding
opcode inspection by specialized semantic cloners.

## Clone and view audit

The immutable selection stores declaration identities and recursively frozen type
heads/arguments, never inference proxies or local-variable objects. A member
choice also records its symbol/arity. Every source clone retains the recipe even
with `CloneResolvedFields = false`. Repeated clones compose their correspondence
contexts; completed re-resolution freezes the transported recipe. A missing
resolved choice fails rather than initiating another lookup.

Final declaration correspondence is separate from Cloner's reference caches.
A prior reference cache entry cannot stand in for a final definition. Export
cloning requires the final owner/member/type-parameter objects, checks visibility
and revelation of user type heads, and retains canonical compiler-owned type
heads. An unavailable or hidden saved operation rejects the view. In particular,
the unsafe trait/refinement and opaque-rank examples are rejection tests, not
accepted proof examples.

| Existing Cloner class | M1 handling |
|---|---|
| `Cloner` | Wrapper recipe and final declaration/type-parameter correspondence |
| `ClonerKeepParensExpressions` | Delegates wrappers to base handling |
| `DeepModuleSignatureCloner` | Delegates wrappers; ordinary signature copying unchanged |
| `ClonerButDropMethodBodies` | Delegates expressions; method bodies remain omitted |
| `ScopeCloner` | Final export objects, visibility/revelation checks and required mappings |
| `AbstractSignatureCloner` | Inherits checked scope handling; feature-bearing abstract imports reject |
| `RefinementCloner` | Preserves recipes; transitive feature-bearing refinement/replacement rejects |
| `SubstitutingCloner` | Operand variable substitution; recipe contains no local-variable references |
| `ClonerButIVariablesAreKeptOnce` | Delegates wrappers and preserves recipe |
| `QuantifiedVariableDomainCloner` | Origin wrapping and base recipe transport |
| `QuantifiedVariableRangeCloner` | Origin wrapping and base recipe transport |
| `ExtremeCloner` | Abstract base; exercised through both descendants |
| `ExtremeLemmaSpecificationSubstituter` | Preserves wrapper before its general concrete-syntax bypass |
| `ExtremeLemmaBodyCloner` | Base wrapper handling; source root bypasses semantic opcode inspection |

`Substituter` consumes the ordinary resolved child and preserves the call's
nontextual reference marker. The additional parser-only attribute cloner strips
unresolved wrappers for the explicitly intrinsic module-attribute phase.

## Evidence owners

The integration driver and separate positive/negative inputs are
`git-issues/github-issue-194.dfy` and `Inputs/github-issue-194-*`. It distinguishes
front-end rejection from verifier failure, checks false controls at the actual
assertion line, runs Python/C# expansions, checks artifact restrictions, and
compares identical-path ordinary-call Boogie and resource vectors. Core tests
check selected declarations/type arguments, call edges, Boogie/trigger equality,
source printing, both clone field modes, final maps, all clone routes and stale
records. The language-server test checks that renaming a callable leaves its
operator symbols untouched.

The disabled comparison workflow builds the pinned starting product
`908d5b8b8e53359955a03dac5a9033caee9c6470` and the candidate with identical assembly
version metadata. It compares literal outputs and resource vectors on the full
existing verifier-suite plan and the source standard library with all six target
parts, plus malformed-input/parser/printing and artifact controls. Generated
scanner bytes, token IDs, lookahead sets and syntax-error text are compared
separately. It never rewrites verdict baselines.

Development probes retain command exits and diagnostics while exiting zero to
avoid expected-failure notifications. A green probe workflow is not acceptance;
its retained checks must pass and the ordinary review workflow must also pass.
