using System.Collections.Concurrent;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using Microsoft.Boogie;
using Microsoft.Dafny.LanguageServer.Workspace.Notifications;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using VC;

namespace Microsoft.Dafny.LanguageServer.Workspace;

public class GutterIconAndHoverVerificationDetailsManager {

  public static readonly Option<bool> LineVerificationStatus = new("--notify-line-verification-status", @"
(experimental, API will change)
Send notifications about the verification status of each line in the program.
".TrimStart());

  private readonly ILogger logger;

  public GutterIconAndHoverVerificationDetailsManager(ILogger logger) {
    this.logger = logger;
  }

  public static DocumentVerificationTree UpdateTree(DafnyOptions options, Program program, DocumentVerificationTree rootVerificationTree) {
    var previousTrees = rootVerificationTree.Children;

    ConcurrentBag<VerificationTree> result = [];

    HashSet<Position> recordedPositions = [];

    void AddAndPossiblyMigrateVerificationTree(VerificationTree verificationTree) {
      var position = verificationTree.Position;
      var previousTree = previousTrees.FirstOrDefault(
        oldNode => oldNode != null && oldNode.Position == position,
        null);
      if (previousTree != null) {
        previousTree.SetObsolete();
        verificationTree.StatusVerification = previousTree.StatusVerification;
        verificationTree.StatusCurrent = CurrentStatus.Obsolete;
        verificationTree.Children = previousTree.Children;
      }

      // Prevent duplicating trees, e.g. reveal lemmas that have the same position as the function.
      if (!recordedPositions.Contains(verificationTree.Position)) {
        result.Add(verificationTree);
        recordedPositions.Add(verificationTree.Position);
      }
    }

    foreach (var module in program.Modules()) {
      foreach (var topLevelDecl in module.TopLevelDecls) {
        if (topLevelDecl is DatatypeDecl datatypeDecl) {
          foreach (DatatypeCtor ctor in datatypeDecl.Ctors) {
            var aFormalHasADefaultValue = ctor.Destructors.SelectMany(
              destructor => destructor.CorrespondingFormals).Any(
              formal => formal.DefaultValue != null);
            if (aFormalHasADefaultValue) {
              var verificationTreeRange = ctor.StartToken.GetLspRange(ctor.EndToken);
              var verificationTree = new TopLevelDeclMemberVerificationTree(
                "datatype",
                ctor.Name,
                ctor.GetCompileName(options),
                ctor.Origin.Filepath,
                ctor.Origin.Uri,
                verificationTreeRange,
                ctor.Origin.GetLspPosition(),
                Attributes.Contains(ctor.Attributes, "only")
                );
              AddAndPossiblyMigrateVerificationTree(verificationTree);
            }
          }
        }

        if (topLevelDecl is TopLevelDeclWithMembers topLevelDeclWithMembers) {
          foreach (var member in topLevelDeclWithMembers.Members) {
            var memberWasNotIncluded = member.Origin.Uri != rootVerificationTree.Uri;
            if (memberWasNotIncluded) {
              continue;
            }

            if (member is ConstantField constantField) {
              if (constantField.Rhs == null) {
                continue; // Nothing to verify
              }

              var verificationTreeRange = member.StartToken.GetLspRange(member.EndToken);
              var verificationTree = new TopLevelDeclMemberVerificationTree(
                // a witness of a type with member invariants (issue 105) has a reserved name that is not shown
                member is MemberWitness ? "witness of" : "constant",
                member is MemberWitness ? topLevelDecl.Name : member.Name,
                member.GetCompileName(options),
                member.Origin.Filepath,
                member.Origin.Uri,
                verificationTreeRange,
                member.Origin.GetLspPosition(),
                Attributes.Contains(member.Attributes, "only"));
              AddAndPossiblyMigrateVerificationTree(verificationTree);
            } else if (member is MethodOrConstructor or Function) {
              var verificationTreeRange = member.StartToken.GetLspRange(member.EndToken);
              var verificationTree = new TopLevelDeclMemberVerificationTree(
                // a member invariant (issue 105) has a reserved name that is not shown
                member is MethodOrConstructor ? "method" : member is MemberInvariant ? "invariant of" : "function",
                member is MemberInvariant ? topLevelDecl.Name : member.Name,
                member.GetCompileName(options),
                member.Origin.Filepath,
                member.Origin.Uri,
                verificationTreeRange,
                member.NavigationRange.StartToken.GetLspPosition(),
                Attributes.Contains(member.Attributes, "only"));
              AddAndPossiblyMigrateVerificationTree(verificationTree);
              if (member is Function { ByMethodBody: { } } function) {
                var verificationTreeRangeByMethod = function.ByMethodBody.EntireRange.ToLspRange();
                var verificationTreeByMethod = new TopLevelDeclMemberVerificationTree(
                  "by method part of function",
                  member.Name,
                  member.GetCompileName(options) + "_by_method",
                  member.Origin.Filepath,
                  member.Origin.Uri,
                  verificationTreeRangeByMethod,
                  function.ByMethodTok.GetLspPosition(),
                  Attributes.Contains(member.Attributes, "only"));
                AddAndPossiblyMigrateVerificationTree(verificationTreeByMethod);
              }
            }
          }
        }

        if (topLevelDecl is SubsetTypeDecl subsetTypeDecl) {
          if (subsetTypeDecl.Origin.Uri != rootVerificationTree.Uri) {
            continue;
          }

          var verificationTreeRange = subsetTypeDecl.StartToken.GetLspRange(subsetTypeDecl.EndToken);
          var verificationTree = new TopLevelDeclMemberVerificationTree(
            $"subset type",
            subsetTypeDecl.Name,
            subsetTypeDecl.GetCompileName(options),
            subsetTypeDecl.Origin.Filepath,
            subsetTypeDecl.Origin.Uri,
            verificationTreeRange,
            subsetTypeDecl.Origin.GetLspPosition(),
            Attributes.Contains(subsetTypeDecl.Attributes, "only"));
          AddAndPossiblyMigrateVerificationTree(verificationTree);
        }
      }
    }

    return new DocumentVerificationTree(program, rootVerificationTree.Uri) {
      Children = result
    };
  }

  /// <summary>
  /// On receiving all implementations that are going to be verified, assign each implementation
  /// to its original method tree.
  /// Also set the implementation priority depending on the last edited methods 
  /// </summary>
  public virtual void ReportImplementationsBeforeVerification(IdeState state, ICanVerify canVerify, Implementation[] implementations) {
    var uri = canVerify.Origin.Uri;
    var tree = state.VerificationTrees.GetValueOrDefault(uri) ?? new DocumentVerificationTree(state.Program, uri);

    if (logger.IsEnabled(LogLevel.Debug)) {
      logger.LogDebug($"ReportImplementationsBeforeVerification for ${state.Uri}, version {state.Version}, implementations: " +
                      $"{string.Join(", ", implementations.Select(i => i.Name))}");
    }

    // We migrate existing implementations to the new provided ones if they exist.
    // (same child number, same file and same position)
    var canVerifyNode = tree.Children.OfType<TopLevelDeclMemberVerificationTree>()
      .FirstOrDefault(t => t.Position == canVerify.Origin.GetLspPosition());
    if (canVerifyNode == null) {
      return;
    }

    // Several ICanVerify can share this node.  A refining module's copy of an inherited member, whose override check
    // that module makes again, has the position of the member in the refinement base, which is verified too
    // (https://github.com/erniecohen/dafny/issues/423).  Each reports only its own implementations.  So the node keeps
    // the implementations of the others, as long as IdeState still has tasks of theirs at this range, and its status
    // and its assertion batches merge them all.  IdeState keys its tasks the same way (IdeState.CopyingModule).
    var copyingModule = IdeState.CopyingModule(canVerify);
    var verifiedAtRange = state.CanVerifyStates.GetValueOrDefault(uri)?
      .GetValueOrDefault(canVerify.NavigationRange.ToLspRange())?.VerificationTasks.Values
      .Select(task => IdeState.CopyingModule(Compilation.GetCanVerify(task.Task))).ToHashSet() ?? [];

    canVerifyNode.ResetNewChildren();
    foreach (var other in canVerifyNode.Children.OfType<ImplementationVerificationTree>()) {
      if (other.CopyingModule != copyingModule && verifiedAtRange.Contains(other.CopyingModule)) {
        canVerifyNode.AddNewChild(other);
      }
    }

    foreach (var implementation in implementations) {

      var targetMethodNode = GetTargetMethodTree(tree, implementation, out var oldImplementationNode, true);
      if (targetMethodNode == null) {
        NoMethodNodeAtLogging(tree, "ReportImplementationsBeforeVerification", state, implementation);
        continue;
      }

      var newDisplayName = targetMethodNode.DisplayName + " #" + (targetMethodNode.Children.Count + 1) + ":" +
                           implementation.Name;
      var newImplementationNode = new ImplementationVerificationTree(
        newDisplayName,
        implementation.VerboseName,
        implementation.Name,
        targetMethodNode.Filename,
        targetMethodNode.Uri,
        targetMethodNode.Range,
        targetMethodNode.Position
      ) {
        CopyingModule = copyingModule
      }.WithImplementation(implementation);
      if (oldImplementationNode != null) {
        newImplementationNode.Children = oldImplementationNode.Children;
      }

      targetMethodNode.AddNewChild(newImplementationNode);
    }

    var newChildren = canVerifyNode.NewChildren;
    canVerifyNode.SaveNewChildren();
    if (!canVerifyNode.Children.Any()) {
      canVerifyNode.Start();
      canVerifyNode.Stop();
      canVerifyNode.StatusCurrent = CurrentStatus.Current;
      canVerifyNode.StatusVerification = GutterVerificationStatus.Verified;
    } else if (canVerifyNode.Finished && canVerifyNode.Children.Any(child => !child.Finished)) {
      // Another ICanVerify at this node finished first.  The node is pending again until these implementations are
      // verified.  It is not started again: Start would restart the finished implementations too, and stopping
      // them would then drop their results.
      canVerifyNode.Finished = false;
      canVerifyNode.StatusCurrent = CurrentStatus.Obsolete;
    }

    canVerifyNode.PropagateChildrenErrorsUp();
    canVerifyNode.RecomputeAssertionBatchNodeDiagnostics();
  }

  /// <summary>Install source-bound reports without fabricating native implementations.</summary>
  public void ReportCachedImplementations(IdeState state, ICanVerify canVerify, IEnumerable<Caching.IdeTaskReport> reports) {
    var uri = canVerify.Origin.Uri;
    var tree = state.VerificationTrees.GetValueOrDefault(uri);
    var node = tree?.Children.OfType<TopLevelDeclMemberVerificationTree>()
      .FirstOrDefault(t => t.Position == canVerify.Origin.GetLspPosition());
    if (node == null) { return; }
    var copyingModule = IdeState.CopyingModule(canVerify);
    var verifiedAtRange = state.CanVerifyStates.GetValueOrDefault(uri)?
      .GetValueOrDefault(canVerify.NavigationRange.ToLspRange())?.VerificationTasks.Values
      .Select(task => IdeState.CopyingModule(Compilation.GetCanVerify(task.Task))).ToHashSet() ?? [];
    node.ResetNewChildren();
    foreach (var other in node.Children.OfType<ImplementationVerificationTree>()) {
      if (other.CopyingModule != copyingModule && verifiedAtRange.Contains(other.CopyingModule)) { node.AddNewChild(other); }
    }
    foreach (var report in reports.DistinctBy(r => r.Name)) {
      var old = node.Children.OfType<ImplementationVerificationTree>().FirstOrDefault(t =>
        t.CopyingModule == copyingModule && t.Identifier == report.Name);
      var child = new ImplementationVerificationTree(
        node.DisplayName + " #" + (node.Children.Count + 1) + ":" + report.Name,
        report.VerboseName, report.Name, node.Filename, node.Uri, node.Range, node.Position) {
        CopyingModule = copyingModule
      }.WithReport(report);
      if (old != null) { child.Children = old.Children; }
      node.AddNewChild(child);
    }
    node.SaveNewChildren();
    if (node.Finished && node.Children.Any(child => !child.Finished)) {
      node.Finished = false;
      node.StatusCurrent = CurrentStatus.Obsolete;
    }
    node.PropagateChildrenErrorsUp();
    node.RecomputeAssertionBatchNodeDiagnostics();
  }

  /// <summary>Publish a complete batch from its portable report.</summary>
  public void ReportCachedBatch(IdeState state, ICanVerify canVerify, Caching.IdeTaskReport report,
    VerificationRunResult result, bool finished, int resourceCount, VcOutcome outcome) {
    var tree = state.VerificationTrees[canVerify.Origin.Uri];
    var node = tree.Children.OfType<TopLevelDeclMemberVerificationTree>()
      .FirstOrDefault(t => t.Position == canVerify.Origin.GetLspPosition());
    var implementation = node?.Children.OfType<ImplementationVerificationTree>().SingleOrDefault(t =>
      t.CopyingModule == IdeState.CopyingModule(canVerify) && t.Report?.Name == report.Name);
    if (node == null || implementation == null) { return; }
    lock (LockProcessing) {
      if (!node.Started) { node.Start(); }
      if (!implementation.Started) { implementation.Start(); }
      if (!implementation.NewChildren.OfType<AssertionVerificationTree>().Any(a => a.AssertionBatchNum == result.VcNum)) {
        var time = (int)result.RunTime.TotalMilliseconds;
        implementation.AddAssertionBatchMetrics(result.VcNum, time, result.ResourceCount, result.CoveredElements.ToList());
        foreach (var assertion in report.Assertions) {
          var token = assertion.Origin;
          if (token.Filepath != implementation.Filename) { continue; }
          var count = implementation.GetNewChildrenCount();
          var display = assertion.Kind == "ensures" ? " ensures" : assertion.Kind == "call" ? "Call" : "Assertion";
          var identifier = assertion.Kind == "ensures" ? "_ensures" : assertion.Kind;
          var child = new AssertionVerificationTree(
            $"{node.DisplayName} {display} #{count}", $"{node.Identifier}_{count}_{identifier}",
            token.Filepath, token.Uri, assertion.SecondaryOrigin?.GetLspPosition(), token.GetLspRange()) {
            StatusVerification = GetNodeStatus(assertion.Outcome),
            StatusCurrent = CurrentStatus.Current, AssertionBatchNum = result.VcNum,
            ImmediatelyRelatedRanges = assertion.RelatedOrigins.Select(o => o.GetLspRange()).ToList(),
            DynamicallyRelatedRanges = (assertion.DynamicOrigins ?? []).Select(o => o.GetLspRange()).ToList(),
            Started = true, Finished = true
          }.WithDuration(implementation.StartTime, time).WithReport(assertion);
          implementation.AddNewChild(child);
          var previous = implementation.Children.OfType<AssertionVerificationTree>()
            .FirstOrDefault(c => c.Position == child.Position && c.SecondaryPosition == child.SecondaryPosition);
          if (previous != null) {
            implementation.Children = new ConcurrentBag<VerificationTree>(implementation.Children.Where(c => c != previous)) {
              previous with { StatusCurrent = CurrentStatus.Current, StatusVerification = GetNodeStatus(assertion.Outcome) }
            };
          } else { implementation.Children.Add(child); }
        }
      }
      if (finished) {
        implementation.Stop();
        implementation.ResourceCount = resourceCount;
        node.ResourceCount += resourceCount;
        var status = outcome == VcOutcome.Correct ? GutterVerificationStatus.Verified : GutterVerificationStatus.Error;
        implementation.StatusVerification = status;
        if (node.Children.All(child => child.Finished)) {
          node.Stop();
          node.StatusVerification = status;
        } else if (status == GutterVerificationStatus.Error) {
          node.StatusVerification = status;
        }
      }
      node.PropagateChildrenErrorsUp();
      node.RecomputeAssertionBatchNodeDiagnostics();
    }
  }

  /// <summary>
  /// Called when the verifier starts verifying an implementation
  /// </summary>
  public void ReportVerifyImplementationRunning(IdeState state, Implementation implementation) {
    var uri = ((IOrigin)implementation.tok).Uri;
    var tree = state.VerificationTrees[uri];

    lock (LockProcessing) {
      var targetMethodNode = GetTargetMethodTree(tree, implementation, out var implementationNode);
      if (targetMethodNode == null) {
        NoMethodNodeAtLogging(tree, "ReportVerifyImplementationRunning", state, implementation);
      } else {
        if (!targetMethodNode.Started) {
          // The same method could be started multiple times for each implementation
          targetMethodNode.Start();
        }

        if (implementationNode == null) {
          logger.LogError($"No implementation at {implementation.tok}");
        } else {
          implementationNode.Start();
        }

        targetMethodNode.PropagateChildrenErrorsUp();
      }
    }
  }

  /// <summary>
  /// Called when the verifier finished to visit an implementation
  /// </summary>
  public void ReportEndVerifyImplementation(IdeState state, Implementation implementation,
    int implementationResourceCount,
    VcOutcome implementationOutcome) {

    var uri = ((IOrigin)implementation.tok).Uri;
    var tree = state.VerificationTrees[uri];

    var targetMethodNode = GetTargetMethodTree(tree, implementation, out var implementationNode);
    if (targetMethodNode == null) {
      NoMethodNodeAtLogging(tree, "ReportEndVerifyImplementation", state, implementation);
    } else if (implementationNode == null) {
      logger.LogError($"No implementation node at {implementation.tok.filename}:{implementation.tok.line}:{implementation.tok.col}");
    } else {
      lock (LockProcessing) {
        implementationNode.Stop();
        implementationNode.ResourceCount = implementationResourceCount;
        targetMethodNode.ResourceCount += implementationResourceCount;
        var finalOutcome = implementationOutcome switch {
          VcOutcome.Correct => GutterVerificationStatus.Verified,
          _ => GutterVerificationStatus.Error
        };

        implementationNode.StatusVerification = finalOutcome;
        // Will be only executed by the last instance.
        if (!targetMethodNode.Children.All(child => child.Finished)) {
          targetMethodNode.StatusVerification = implementationOutcome switch {
            VcOutcome.Correct => targetMethodNode.StatusVerification,
            _ => GutterVerificationStatus.Error
          };
        } else {
          targetMethodNode.Stop();
          // Later, will be overriden by individual outcomes
          targetMethodNode.StatusVerification = finalOutcome;
        }

        targetMethodNode.PropagateChildrenErrorsUp();
        targetMethodNode.RecomputeAssertionBatchNodeDiagnostics();
      }
    }
  }

  private void NoMethodNodeAtLogging(VerificationTree tree, string methodName, IdeState state, Implementation implementation) {
    var position = implementation.tok.GetLspPosition();
    var availableMethodNodes = string.Join(",", tree!.Children.Select(vt =>
      $"{vt.Kind} {vt.DisplayName} at {vt.Filename}:{vt.Position.Line}"));
    logger.LogDebug(
      $"No method found in {methodName}, in document {state.Uri} and filename {tree.Filename}, " +
      $"no method node at {implementation.tok.filename}:{position.Line}:{position.Character}.\n" +
      $"Available nodes: " + availableMethodNodes);
  }

  /// <summary>
  /// Called when a split is finished to be verified
  /// </summary>
  public void ReportAssertionBatchResult(IdeState ideState, AssertionBatchResult batchResult) {
    var uri = ((IOrigin)batchResult.Implementation.tok).Uri;
    var tree = ideState.VerificationTrees[uri];

    lock (LockProcessing) {
      var implementation = batchResult.Implementation;
      var result = batchResult.Result;
      // While there is no error, just add successful nodes.
      var targetMethodNode = GetTargetMethodTree(tree, implementation, out var implementationNode);
      if (targetMethodNode == null) {
        NoMethodNodeAtLogging(tree, "ReportAssertionBatchResult", ideState, implementation);
      } else if (implementationNode == null) {
        logger.LogError($"No implementation node at {implementation.tok.filename}:{implementation.tok.line}:{implementation.tok.col}");
      } else {
        var wasAlreadyProcessed = implementationNode.NewChildren.Any(assertNode =>
          assertNode is AssertionVerificationTree assertionNode
          && assertionNode.AssertionBatchNum == result.VcNum);
        if (wasAlreadyProcessed) {
          return;
        }

        result.ComputePerAssertOutcomes(out var perAssertOutcome, out var perAssertCounterExample);

        var assertionBatchTime = (int)result.RunTime.TotalMilliseconds;
        var assertionBatchResourceCount = result.ResourceCount;
        implementationNode.AddAssertionBatchMetrics(result.VcNum, assertionBatchTime, assertionBatchResourceCount, result.CoveredElements.ToList());

        // Attaches the trace
        void AddChildOutcome(Counterexample? counterexample, AssertCmd assertCmd, IOrigin token,
          GutterVerificationStatus status, IOrigin? secondaryToken, string? assertDisplay = "",
          string assertIdentifier = "") {
          if (token.Filepath != implementationNode.Filename) {
            return;
          }

          var outcomePosition = token.GetLspPosition();
          var secondaryOutcomePosition = secondaryToken?.GetLspPosition();

          var childrenCount = implementationNode.GetNewChildrenCount();
          assertDisplay = assertDisplay != "" ? " " + assertDisplay : "";
          assertIdentifier = assertIdentifier != "" ? "_" + assertIdentifier : "";

          var outcomeRange = token.GetLspRange();
          var nodeDiagnostic = new AssertionVerificationTree(
            $"{targetMethodNode.DisplayName}{assertDisplay} #{childrenCount}",
            $"{targetMethodNode.Identifier}_{childrenCount}{assertIdentifier}",
            token.Filepath,
            token.Uri,
            secondaryOutcomePosition,
            outcomeRange
          ) {
            StatusVerification = status,
            StatusCurrent = CurrentStatus.Current,
            AssertionBatchNum = result.VcNum,
            Started = true,
            Finished = true
          }.WithDuration(implementationNode.StartTime, assertionBatchTime)
            .WithAssertionAndCounterExample(assertCmd, counterexample);
          // Add this diagnostics as the new one to display once the implementation is fully verified
          implementationNode.AddNewChild(nodeDiagnostic);
          // Update any previous pending "verifying" diagnostic as well so that they are updated in real-time
          var previousChild = implementationNode.Children.OfType<AssertionVerificationTree>()
            .FirstOrDefault(child =>
              child != null && child.Position == outcomePosition && (
                secondaryOutcomePosition == child.SecondaryPosition), null);
          if (previousChild != null) {
            // Temporary update of children.
            implementationNode.Children = new ConcurrentBag<VerificationTree>(implementationNode.Children.Where(child => child != previousChild)) {
              previousChild with {
                StatusCurrent = CurrentStatus.Current,
                StatusVerification = status
              }
            };
          } else {
            implementationNode.Children.Add(nodeDiagnostic);
          }
        }

        foreach (var (assertCmd, outcome) in perAssertOutcome) {
          var status = GetNodeStatus(outcome);
          perAssertCounterExample.TryGetValue(assertCmd, out var counterexample);
          IOrigin? secondaryToken = BoogieGenerator.ToDafnyToken(counterexample is ReturnCounterexample returnCounterexample ? returnCounterexample.FailingReturn.tok :
            counterexample is CallCounterexample callCounterexample ? callCounterexample.FailingRequires.tok :
            null);
          if (assertCmd is AssertEnsuresCmd assertEnsuresCmd) {
            AddChildOutcome(counterexample, assertCmd, BoogieGenerator.ToDafnyToken(assertEnsuresCmd.Ensures.tok), status, secondaryToken, " ensures", "_ensures");
          } else if (assertCmd is AssertRequiresCmd assertRequiresCmd) {
            AddChildOutcome(counterexample, assertCmd, BoogieGenerator.ToDafnyToken(assertRequiresCmd.Call.tok), status, secondaryToken, assertDisplay: "Call", assertIdentifier: "call");
          } else {
            AddChildOutcome(counterexample, assertCmd, BoogieGenerator.ToDafnyToken(assertCmd.tok), status, secondaryToken, assertDisplay: "Assertion", assertIdentifier: "assert");
          }
        }
        targetMethodNode.PropagateChildrenErrorsUp();
        targetMethodNode.RecomputeAssertionBatchNodeDiagnostics();
      }
    }
  }

  /// <summary>
  /// Given an implementation, returns the top-level verification tree.
  /// </summary>
  /// <param name="implementation">The implementation</param>
  /// <param name="implementationTree">Returns the tree of the implementation child of the returned top-level verification tree</param>
  /// <param name="nameBased">Whether it should try to locate the implementation using the name rather than the position. Used to relocate previous diagnostics.</param>
  /// <returns>The top-level verification tree</returns>
  private TopLevelDeclMemberVerificationTree? GetTargetMethodTree(VerificationTree tree,
    Implementation implementation, out ImplementationVerificationTree? implementationTree, bool nameBased = false) {
    var targetMethodNode = tree.Children.OfType<TopLevelDeclMemberVerificationTree>().FirstOrDefault(
      node => node?.Position == implementation.tok.GetLspPosition() &&
              node?.Uri == ((IOrigin)implementation.tok).Uri
      , null);
    if (nameBased) {
      implementationTree = targetMethodNode?.Children.OfType<ImplementationVerificationTree>().FirstOrDefault(
        node => {
          var nodeImpl = node?.GetImplementation();
          return nodeImpl?.Name == implementation.Name;
        }, null);
    } else {
      implementationTree = targetMethodNode?.Children.OfType<ImplementationVerificationTree>().FirstOrDefault(
        node => node?.GetImplementation() == implementation, null);
    }

    return targetMethodNode;
  }

  /// <summary>
  /// An object to lock so that the updating of the verification trees is always not concurrent
  /// </summary>
  private readonly object LockProcessing = new();

  /// <summary>
  /// Converts a ProverInterface.Outcome to a VerificationStatus
  /// </summary>
  /// <param name="outcome">The outcome set by the split result</param>
  /// <returns>The matching verification status</returns>
  private static GutterVerificationStatus GetNodeStatus(SolverOutcome outcome) {
    return outcome switch {
      SolverOutcome.Valid => GutterVerificationStatus.Verified,
      SolverOutcome.Invalid => GutterVerificationStatus.Error,
      SolverOutcome.Undetermined => GutterVerificationStatus.Inconclusive,
      SolverOutcome.TimeOut => GutterVerificationStatus.Error,
      SolverOutcome.OutOfMemory => GutterVerificationStatus.Error,
      SolverOutcome.OutOfResource => GutterVerificationStatus.Error,
      SolverOutcome.Bounded => GutterVerificationStatus.Error,
      _ => GutterVerificationStatus.Error
    };
  }
}
