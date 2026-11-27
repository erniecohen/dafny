// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
#nullable enable
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.Synchronization;

public partial class PersistentCacheLifecycleTest {
  private static void AssertCheckedResolutionHits(string profile) {
    using var events = JsonDocument.Parse("[" + string.Join(",", File.ReadLines(profile)) + "]");
    var snapshots = events.RootElement.EnumerateArray().Where(e =>
      e.TryGetProperty("stage", out var stage) && stage.GetString() == "resolve.snapshot").ToArray();
    Assert.NotEmpty(snapshots);
    Assert.Contains(snapshots, e => e.GetProperty("module").GetString() == "_module.Cached");
    Assert.All(snapshots, e => Assert.StartsWith("hit (checked ", e.GetProperty("state").GetString()));
  }
}
