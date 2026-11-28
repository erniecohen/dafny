using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace Microsoft.Dafny;

/// <summary>
/// Solver options that make the solver unsound, so that Dafny could verify a false assertion.
/// Z3's int-blasting bit-vector solver, selected with smt.bv.solver=2, reports unsat for satisfiable
/// queries of the shape Dafny emits for bit-vector subtraction
/// (https://github.com/erniecohen/dafny/issues/618). Every way a solver option reaches Z3
/// (--solver-option, --boogie /proverOpt:, the legacy /proverOpt:, and either of the first two in a
/// project file) ends in DafnyOptions.ProverOptions, so the check runs there, once, before the solver
/// is set up. It is on by default; the hidden --legacy-allow-unsound-bv-solver turns the error into a
/// warning that the results are untrusted.
/// </summary>
public static class UnsoundSolverOptions {
  public const string IssueUrl = "https://github.com/erniecohen/dafny/issues/618";
  private const string BvSolverParameter = "smt.bv.solver";

  /// <summary>
  /// Whether a prover option sets Z3's smt.bv.solver to 2, in any spelling that Z3 accepts: with or
  /// without Boogie's "O:" prefix, any letter case in the name, and a value Z3 reads as 2 (2, 02, "2",
  /// 2.0, #x2, #b10). A value that cannot be read as a number is treated as unsafe, so the check fails
  /// closed.
  /// </summary>
  public static bool SelectsIntBlastingBvSolver(string proverOption) {
    var body = proverOption.Trim();
    if (body.StartsWith("O:", StringComparison.OrdinalIgnoreCase)) {
      body = body[2..];
    }
    var equals = body.IndexOf('=');
    if (equals < 0) {
      return false;
    }
    var name = body[..equals].Trim().TrimStart(':');
    if (!string.Equals(name, BvSolverParameter, StringComparison.OrdinalIgnoreCase)) {
      return false;
    }
    var value = body[(equals + 1)..].Trim().Trim('"').Trim();
    return !TryParseUnsigned(value, out var number) || number == 2;
  }

  private static bool TryParseUnsigned(string value, out BigInteger number) {
    number = BigInteger.Zero;
    if (value.StartsWith("#x", StringComparison.OrdinalIgnoreCase)) {
      return value.Length > 2 && value[2..].All(Uri.IsHexDigit) &&
             BigInteger.TryParse("0" + value[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out number);
    }
    if (value.StartsWith("#b", StringComparison.OrdinalIgnoreCase)) {
      if (value.Length == 2 || !value[2..].All(c => c is '0' or '1')) {
        return false;
      }
      foreach (var digit in value[2..]) {
        number = number * 2 + (digit - '0');
      }
      return true;
    }
    var point = value.IndexOf('.');
    var whole = point < 0 ? value : value[..point];
    if (point >= 0 && !value[(point + 1)..].All(c => c == '0')) {
      return false;
    }
    return whole.Length > 0 && whole.All(char.IsAsciiDigit) &&
           BigInteger.TryParse(whole, NumberStyles.None, CultureInfo.InvariantCulture, out number);
  }

  public static IReadOnlyList<string> Find(IEnumerable<string> proverOptions) =>
    proverOptions.Where(SelectsIntBlastingBvSolver).Distinct().ToList();

  /// <summary>
  /// Reports each prover option that selects the int-blasting bit-vector solver: as an error, or with
  /// --legacy-allow-unsound-bv-solver as a warning. Returns false when it reported an error.
  /// </summary>
  public static bool Check(DafnyOptions options, ErrorReporter reporter, IOrigin origin) {
    var unsound = Find(options.ProverOptions);
    if (unsound.Count == 0) {
      return true;
    }
    var allow = options.Get(BoogieOptionBag.LegacyAllowUnsoundBvSolver);
    foreach (var option in unsound) {
      var what = $"The solver option '{option}' selects Z3's int-blasting bit-vector solver ({BvSolverParameter}=2), " +
                 $"which can report unsatisfiable for satisfiable queries, so Dafny could verify a false assertion ({IssueUrl}).";
      if (allow) {
        reporter.Warning(MessageSource.Verifier, ResolutionErrors.ErrorId.none, origin,
          what + $" --{BoogieOptionBag.LegacyAllowUnsoundBvSolver.Name} allows it: verification results are untrusted.");
      } else {
        reporter.Error(MessageSource.Verifier, origin,
          what + $" Remove the option. The hidden --{BoogieOptionBag.LegacyAllowUnsoundBvSolver.Name} allows it, for comparison only.");
      }
    }
    return allow;
  }
}
