# IDE project replacement and publication

When the contents of one project file change, every open source that refers to its old manager moves to one replacement manager. The replacement resolves fresh project options and roots. The old compilation, backend and notification observer are retired; a worker or solver completion from that generation cannot be used by the replacement.

## Invariant and race argument

The project database holds its existing mapping lock while it snapshots all source entries whose manager is the old instance, closes that instance, creates one replacement, rebinds each source, and starts the replacement once. No lookup through that lock can observe a mixture of old and new managers for the same changed project. A move to a different project closes the actual source URI instead; other open sources keep their original project.

Notification publication and observer retirement hold the same `lastPublishedStateLock`. Retirement waits for a publication that already holds this lock, marks the observer retired, and performs its final clear under the lock. Later `OnNext`, `Clear` and `Migrate` calls observe retirement and return. The replacement is created only after retirement returns, so no old notification can follow a replacement notification. Cancelling old tasks happens after retirement; cancellation callbacks and delayed terminal results therefore cannot reactivate old publication. Direct disposal retires without a clear before cancelling its backend and compilation.

This is a lifecycle argument, not evidence about B3 proof semantics. The controlled verifier in the migration regression does not invoke a solver: it exposes task start, cancellation, and delayed cancelled completion. The observer tests separately control an in-flight publication and a late completion after retirement. Existing project-root and configuration tests remain relevant because replacement also applies to ordinary project edits.

## Focused checks

Run the LanguageServer test project with a filter covering `B3ProjectMigrationTest`, `IdeStateObserverRetirementTest`, `ProjectManagerDatabaseTest`, `ProjectFilesTest`, `MultipleFilesProjectTest`, `CompetingProjectFilesTest`, and `AdditionalAxiomsTest`. The new controls require two open sources to share one replacement on backend or worker changes, old tasks to receive cancellation before their delayed completion, updated ordinary roots to take effect, and a different-project move to release its old source reference.
