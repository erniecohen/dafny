#nullable enable
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using Microsoft.Dafny;

namespace DafnyCore.Options;

/// <summary>
/// Decides which --boogie arguments must agree between a project and a library (.doo) it loads.
/// An argument is either library-neutral or checked.  A library-neutral argument cannot change which
/// facts an imported, already verified declaration contributes to the project's verification, so a
/// project and a library may differ in it.  Every other argument is checked: the arguments of both
/// sides, with the library-neutral ones removed, must be equal as sequences.  The default is to check,
/// so an argument is exempt only if it is on <see cref="LibraryNeutral"/>, which is explicit and reviewed.
/// --legacy-boogie-library-check restores the exact comparison of all arguments.
/// </summary>
public static class BoogieLibraryCompatibility {

  /// <summary>
  /// The reviewed library-neutral Boogie arguments, by name (without the leading '/' or '-'), each with
  /// the reason it cannot change the facts an imported declaration contributes.  Only the bare form and
  /// the values 0 and 1 are recognized: any other form of an argument is checked.
  /// To add an entry, read Boogie's handling of the argument; leave it off if in doubt.
  /// </summary>
  public static readonly IReadOnlyDictionary<string, string> LibraryNeutral = new Dictionary<string, string> {
    ["normalizeDeclarationOrder"] =
      "only sorts the declarations sent to the solver by content; the set of declarations, hence of facts, is unchanged",
    ["structuralLiterals"] =
      "only makes expressions that denote the same literal or variable compare equal when joining and folding guards, so the verification conditions of the project's own code mean the same",
    ["cacheBackground"] =
      "only reuses a solver context that has the same declarations, instead of setting it up again",
    ["scheduleByImplementation"] =
      "only chooses which solver process runs which split; it changes no declaration or assertion",
  };

  /// <summary>Whether the --boogie argument (one already split from the command line) is library-neutral.</summary>
  public static bool IsLibraryNeutral(string argument) {
    if (argument.Length < 2 || (argument[0] != '/' && argument[0] != '-')) {
      return false;
    }
    var body = argument.Substring(1);
    var colon = body.IndexOf(':');
    var name = colon < 0 ? body : body.Substring(0, colon);
    var value = colon < 0 ? null : body.Substring(colon + 1);
    return LibraryNeutral.ContainsKey(name) && (value == null || value == "0" || value == "1");
  }

  public static IReadOnlyList<string> CheckedArguments(IEnumerable<string>? arguments) =>
    (arguments ?? Array.Empty<string>()).SelectMany(BoogieOptionBag.SplitArguments)
      .Where(argument => !IsLibraryNeutral(argument)).ToList();

  public static bool Check(ErrorReporter reporter, IOrigin origin, string prefix, Option option,
    object localValue, object libraryValue) {
    var local = localValue as IEnumerable<string>;
    var library = libraryValue as IEnumerable<string>;
    if (CheckedArguments(local).SequenceEqual(CheckedArguments(library))) {
      return true;
    }
    // The same error as the exact comparison, so that it names what differs as the user wrote it.
    return OptionCompatibility.CheckOptionMatches(reporter, origin, prefix, option, localValue, libraryValue);
  }
}
