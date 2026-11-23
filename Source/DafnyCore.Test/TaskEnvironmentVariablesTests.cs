using System.Collections;
using Microsoft.Dafny.Caching;
using Xunit;

namespace Microsoft.Dafny.Test;

/// <summary>
/// The task-result cache keys only the environment variables that can change a result (issue
/// 261), so that one cache is shared across shells, checkouts and machines.
/// </summary>
public class TaskEnvironmentVariablesTests {
  private static Dictionary<string, string> Parts(Hashtable environment) =>
    TaskExecutionEnvironment.VariableParts(environment, path => path).ToDictionary(p => p.Name, p => p.Digest);

  private static Hashtable Shell(params (string Name, string Value)[] extra) {
    var environment = new Hashtable {
      ["PATH"] = "/usr/bin:/bin:/opt/tools/bin", ["HOME"] = "/home/a", ["DAFNY_CACHE_DIR"] = "/caches/one",
      ["DAFNY_CACHE_PARTITION"] = "first", ["CI"] = "true", ["TERM"] = "xterm"
    };
    foreach (var (name, value) in extra) { environment[name] = value; }
    return environment;
  }

  [Fact]
  public void UnrelatedVariablesAreNotKeyed() {
    Assert.Empty(Parts(Shell()));
    var other = new Hashtable {
      ["PATH"] = "/opt/tools/bin:/bin:/usr/bin", ["HOME"] = "/home/b", ["DAFNY_CACHE_DIR"] = "/caches/two",
      ["DAFNY_CACHE_PARTITION"] = "second", ["GITHUB_ACTIONS"] = "true"
    };
    Assert.Empty(Parts(other));
    Assert.Empty(Parts(new Hashtable()));
  }

  [Theory]
  [MemberData(nameof(Listed))]
  public void EachListedVariableIsKeyedByItsValue(string name) {
    var set = Parts(Shell((name, "/lib/one")));
    Assert.Equal([name], set.Keys);
    var changed = Parts(Shell((name, "/lib/two")));
    Assert.NotEqual(set[name], changed[name]);
    Assert.Equal(set, Parts(Shell((name, "/lib/one"), ("HOME", "/elsewhere"))));
    Assert.DoesNotContain("/lib/one", set[name]);
  }

  public static IEnumerable<object[]> Listed() => TaskExecutionEnvironment.ResultVariables.Select(name => new object[] { name });

  [Fact]
  public void TheListIsTheLoaderVariablesOnly() {
    Assert.All(TaskExecutionEnvironment.ResultVariables, name => Assert.True(name.StartsWith("DYLD_") || name.StartsWith("LD_"), name));
    Assert.Equal(TaskExecutionEnvironment.ResultVariables.Distinct().Count(), TaskExecutionEnvironment.ResultVariables.Count);
  }
}
