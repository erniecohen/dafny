#nullable enable
using System.CommandLine;
using System.Threading.Tasks;
using Microsoft.Dafny.Caching;

namespace Microsoft.Dafny;

/// <summary><c>dafny cache keep|release|gc|status</c>: retention of the cache store's large tier (issue 261).</summary>
public static class CacheCommand {
  private static readonly Option<string> Tag = new("--tag", "The name of the tag.") { ArgumentHelpName = "NAME" };

  static CacheCommand() {
    OptionRegistry.RegisterOption(Tag, OptionScope.Cli);
  }

  public static Command Create() {
    var result = new Command("cache", "Manage the large entries (resolution snapshots, Boogie text) of the cache store set by --cache-dir, by verification-cache in a project file given as the argument, or by DAFNY_CACHE_DIR.");
    result.AddCommand(Subcommand(CacheMaintenance.Keep, "Tag the entries that the partition (--cache-partition) holds now, so that its budget and expiry leave them; replaces the tag's earlier entries.", true));
    result.AddCommand(Subcommand(CacheMaintenance.Release, "Release a tag, deleting the entries that nothing else holds.", true));
    result.AddCommand(Subcommand(CacheMaintenance.Collect, "Enforce the partition's budget, expire orphaned partitions, enforce the store's cap, and delete entries that nothing holds.", false));
    result.AddCommand(Subcommand(CacheMaintenance.Status, "Show the store's size and what each partition and tag holds.", false));
    return result;
  }

  private static Command Subcommand(string name, string description, bool tagged) {
    var command = new Command(name, description);
    // An optional project file, whose verification-cache and cache-partition apply as in any other command.
    command.AddArgument(DafnyCommands.FilesArgument);
    foreach (var option in new Option[] { CommonOptionBag.VerificationCache, CommonOptionBag.CachePartition,
               CommonOptionBag.CachePartitionBudget, CommonOptionBag.CacheSizeCap, CommonOptionBag.CacheOrphanDays }) {
      command.AddOption(option);
    }
    if (tagged) { command.AddOption(Tag); }
    DafnyNewCli.SetHandlerUsingDafnyOptionsContinuation(command, (options, _) =>
      Task.FromResult(CacheMaintenance.Run(options, name, tagged ? options.Get(Tag) : null)));
    return command;
  }

  /// <summary>Whether <paramref name="command"/> is one of these, which use a store without resolving anything.</summary>
  public static bool IsCacheCommand(Command command) => command.Parents is { } parents &&
    System.Linq.Enumerable.Any(parents, p => p is Command { Name: "cache" });
}
