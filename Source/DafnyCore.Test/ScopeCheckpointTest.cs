using System.IO;
using Microsoft.Dafny;
using Xunit;

namespace DafnyCore.Test;

public class ScopeCheckpointTest {
  [Fact]
  public void RestoresNestedScopesAndInstanceAvailability() {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    var scope = new Scope<object>(options);
    var outer = new object();
    scope.PushMarker();
    scope.Push("outer", outer);
    var checkpoint = scope.CreateCheckpoint();
    scope.PushMarker();
    scope.AllowInstance = false;
    scope.Push("inner", new object());
    scope.PushMarker();
    scope.Push("deep", new object());

    scope.RestoreCheckpoint(checkpoint);
    Assert.True(scope.AllowInstance);
    Assert.Same(outer, scope.Find("outer"));
    Assert.Null(scope.Find("inner"));
    Assert.Null(scope.Find("deep"));
    scope.PopMarker();
    Assert.Empty(scope.Names);
  }

  [Fact]
  public void PreservesAnEnclosingInstanceRestriction() {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    var scope = new Scope<object>(options);
    scope.PushMarker();
    scope.AllowInstance = false;
    var checkpoint = scope.CreateCheckpoint();
    scope.PushMarker();
    scope.Push("inner", new object());
    scope.RestoreCheckpoint(checkpoint);
    Assert.False(scope.AllowInstance);
    scope.PopMarker();
    Assert.True(scope.AllowInstance);
  }
}
