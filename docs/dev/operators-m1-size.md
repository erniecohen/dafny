# M1 measured patch size

Measured against shipped base `908d5b8b8e53359955a03dac5a9033caee9c6470` using `git diff --numstat`. The last product revision is `07318fba0d0eeb72a834750a36d7f2190ae960db`; the accompanying metadata/report commit changes no product source. Each count is added plus deleted lines. Generated parser/scanner output is excluded.

The design estimated **1,091–2,023 product lines**. This revision changes **712 product lines** under the design's product scope. Existing call finalization, concrete-syntax traversal and Cloner inheritance account for the smaller implementation; line counts are not correctness evidence.

| Product file | Added | Deleted | Actual changed | Design estimate |
|---|---:|---:|---:|---:|
| `Source/DafnyCore/AST/Cloner.cs` | 25 | 4 | 29 | 35–70 |
| `Source/DafnyCore/AST/Expressions/Applications/FunctionCallExpr.cs` | 3 | 1 | 4 | 10–20 |
| `Source/DafnyCore/AST/Expressions/Operators/ChainingExpression.cs` | 9 | 4 | 13 | 30–60 |
| `Source/DafnyCore/AST/Expressions/Operators/OperatorSyntaxExpr.cs` | 69 | 0 | 69 | 75–130 |
| `Source/DafnyCore/AST/ExtremeLemmaSpecificationSubstituter.cs` | 4 | 0 | 4 | not separately estimated |
| `Source/DafnyCore/AST/Grammar/ParserNonGeneratedPart.cs` | 27 | 0 | 27 | 50–100 |
| `Source/DafnyCore/AST/Grammar/Printer/Printer.Expression.cs` | 4 | 1 | 5 | 35–65 |
| `Source/DafnyCore/AST/Grammar/ProgramParser.cs` | 17 | 0 | 17 | 10–25 |
| `Source/DafnyCore/AST/IHasReferences.cs` | 1 | 1 | 2 | 5–15 |
| `Source/DafnyCore/AST/Members/MemberDecl.cs` | 1 | 0 | 1 | 10–20 |
| `Source/DafnyCore/AST/Modules/AbstractSignatureCloner.cs` | 2 | 2 | 4 | not separately estimated |
| `Source/DafnyCore/AST/Modules/ScopeCloner.cs` | 22 | 1 | 23 | 90–160 |
| `Source/DafnyCore/AST/OperatorCloneContext.cs` | 42 | 0 | 42 | 70–120 |
| `Source/DafnyCore/AST/OperatorSelection.cs` | 38 | 0 | 38 | 70–130 |
| `Source/DafnyCore/AST/Substituter.cs` | 1 | 0 | 1 | 0–10 |
| `Source/DafnyCore/Backends/DafnyExecutableBackend.cs` | 3 | 0 | 3 | 5–10 |
| `Source/DafnyCore/Backends/ExecutableBackend.cs` | 10 | 0 | 10 | 20–40 |
| `Source/DafnyCore/Backends/Library/LibraryBackend.cs` | 5 | 0 | 5 | 5–15 |
| `Source/DafnyCore/Dafny.atg` | 11 | 8 | 19 | 20–45 |
| `Source/DafnyCore/DafnyFile.cs` | 12 | 0 | 12 | not separately estimated |
| `Source/DafnyCore/DooFile.cs` | 8 | 0 | 8 | 10–20 |
| `Source/DafnyCore/Options/CommonOptionBag.cs` | 10 | 0 | 10 | 20–35 |
| `Source/DafnyCore/Options/DafnyCommands.cs` | 1 | 0 | 1 | 1–3 |
| `Source/DafnyCore/Options/TranslationRecord.cs` | 0 | 0 | 0 | 0 |
| `Source/DafnyCore/Resolver/ModuleResolver.cs` | 8 | 2 | 10 | 45–90 |
| `Source/DafnyCore/Resolver/OperatorResolution.cs` | 103 | 0 | 103 | 160–260 |
| `Source/DafnyCore/Resolver/PreType/PreTypeConstraints.cs` | 1 | 1 | 2 | not separately estimated |
| `Source/DafnyCore/Resolver/PreType/PreTypeResolve.Expressions.cs` | 32 | 23 | 55 | 30–60 |
| `Source/DafnyCore/Resolver/PreType/PreTypeResolve.Operators.cs` | 114 | 0 | 114 | 230–360 |
| `Source/DafnyCore/Resolver/PreType/PreTypeResolve.cs` | 7 | 0 | 7 | not separately estimated |
| `Source/DafnyCore/Resolver/PreType/PreTypeToType.cs` | 0 | 0 | 0 | 0–15 |
| `Source/DafnyCore/Resolver/PreType/TypeRefinementVisitor.cs` | 0 | 0 | 0 | 0–15 |
| `Source/DafnyCore/Resolver/PreType/UnderspecificationDetector.cs` | 31 | 0 | 31 | 10–25 |
| `Source/DafnyCore/Rewriters/RefinementTransformer.cs` | 6 | 0 | 6 | 25–55 |
| `Source/DafnyLanguageServer/Handlers/DafnyHoverHandler.cs` | 7 | 0 | 7 | not separately estimated |
| `Source/DafnyLanguageServer/Handlers/DafnyRenameHandler.cs` | 2 | 1 | 3 | 10–25 |
| `Source/DafnyLanguageServer/Workspace/SymbolTable.cs` | 25 | 2 | 27 | 10–25 |

The input-validation estimate is shared by `ProgramParser.cs` and `DafnyFile.cs`; the additional file is shown separately above. Integration formerly budgeted in `ModuleResolver.cs` is partly in `PreTypeResolve.cs`.

| Other changed file | Added | Deleted | Actual changed |
|---|---:|---:|---:|
| `.github/review/base` | 1 | 1 | 2 |
| `.github/review/operators-m1-off.py` | 232 | 0 | 232 |
| `.github/review/operators-m1-parser.py` | 27 | 0 | 27 |
| `.github/review/operators-m1.py` | 7 | 0 | 7 |
| `.github/review/regressions` | 3 | 0 | 3 |
| `.github/workflows/operators-m1-off.yml` | 113 | 0 | 113 |
| `.github/workflows/operators-m1-probe.yml` | 77 | 0 | 77 |
| `.github/workflows/review.yml` | 8 | 0 | 8 |
| `Source/DafnyCore.Test/OperatorSelectionTests.cs` | 220 | 0 | 220 |
| `Source/DafnyLanguageServer.Test/Lookup/HoverTest.cs` | 20 | 0 | 20 |
| `Source/DafnyLanguageServer.Test/Refactoring/RenameTest.cs` | 26 | 1 | 27 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-abstract-import.dfy` | 8 | 0 | 8 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-arity.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-bitwise-0.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-bitwise-1.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-bitwise-2.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-bitwise-3.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-bitwise-4.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-bitwise-5.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-by-method.dfy` | 5 | 0 | 5 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-calc-negative.dfy` | 8 | 0 | 8 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-calc.dfy` | 6 | 0 | 6 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-chain-false.dfy` | 8 | 0 | 8 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-chain-named.dfy` | 8 | 0 | 8 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-chain.dfy` | 8 | 0 | 8 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-codatatype.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-compiled-rank.dfy` | 4 | 0 | 4 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-compound.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-constructor.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-contracts-named.dfy` | 7 | 0 | 7 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-contracts.dfy` | 7 | 0 | 7 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-default.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-duplicate-attribute.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-duplicate-slot.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-equality.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-escape.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-expansion.dfy` | 5 | 0 | 5 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-false.dfy` | 72 | 0 | 72 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-ghost-formal-named.dfy` | 4 | 0 | 4 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-ghost-formal.dfy` | 4 | 0 | 4 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-ghost-named.dfy` | 4 | 0 | 4 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-ghost.dfy` | 4 | 0 | 4 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-hidden-member.dfy` | 7 | 0 | 7 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-inequality.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-inference-named.dfy` | 14 | 0 | 14 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-inference.dfy` | 14 | 0 | 14 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-intrinsic-refinement.dfy` | 4 | 0 | 4 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-malformed.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-member-generic.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-method.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-module-attribute-named.dfy` | 5 | 0 | 5 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-module-attribute-user.dfy` | 5 | 0 | 5 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-module-attributes.dfy` | 3 | 0 | 3 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-module.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-named.dfy` | 74 | 0 | 74 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-nameonly.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-new-refinement.dfy` | 6 | 0 | 6 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-newtype.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-null-named.dfy` | 4 | 0 | 4 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-null.dfy` | 4 | 0 | 4 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-opaque-rank.dfy` | 14 | 0 | 14 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-opaque.dfy` | 7 | 0 | 7 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-override.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-positive.dfy` | 74 | 0 | 74 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-reads-named.dfy` | 5 | 0 | 5 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-reads.dfy` | 5 | 0 | 5 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-refinement.dfy` | 22 | 0 | 22 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-result.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-revealed.dfy` | 24 | 0 | 24 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-right-only.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-static-owner.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-static-unary.dfy` | 8 | 0 | 8 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-termination-named.dfy` | 3 | 0 | 3 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-termination.dfy` | 3 | 0 | 3 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-trait-named-body.dfy` | 11 | 0 | 11 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-trait.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-transitive-refinement.dfy` | 7 | 0 | 7 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-traversal-named.dfy` | 18 | 0 | 18 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-traversal.dfy` | 18 | 0 | 18 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-unresolved.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-wrong-argument-named.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-wrong-argument.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-wrong-result-named.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194-wrong-result.dfy` | 1 | 0 | 1 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/Inputs/github-issue-194.py` | 186 | 0 | 186 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/github-issue-194.dfy` | 6 | 0 | 6 |
| `Source/IntegrationTests/TestFiles/LitTests/LitTest/git-issues/github-issue-194.dfy.expect` | 1 | 0 | 1 |
| `docs/DafnyRef/Attributes.md` | 33 | 0 | 33 |
| `docs/DafnyRef/Expressions.md` | 35 | 0 | 35 |
| `docs/DafnyRef/UserGuide.md` | 23 | 0 | 23 |
| `docs/dev/news/194.feat` | 7 | 0 | 7 |
| `docs/dev/operators-m1-size.md` | 145 | 0 | 145 |
| `docs/dev/operators-m1.md` | 96 | 0 | 96 |

Other changes total **1821 lines**, including tests, CI, documentation and review metadata. The table includes this report. The complete patch has **2533 changed lines**.
