using System.Numerics;

namespace DafnyB3Protocol;

/// <summary>Rejects malformed/oversized IR before constructing generated runtime objects.</summary>
public static class ProtocolValidation {
  public static void ValidateRequest(Request request) {
    if (request.Version != Protocol.Version || request.NormalizerVersion != Protocol.NormalizerVersion) {
      throw new InvalidDataException("Unsupported B3 protocol or normalizer version");
    }
    Require(!string.IsNullOrWhiteSpace(request.RequestId), "Missing request identity");
    Require(IsHash(request.ProgramHash) && IsCommit(request.B3Commit) && IsHash(request.WorkerFingerprint), "Invalid program/build identity");
    Require(request.ProgramHash == Protocol.GetProgramHash(request.Program), "Program hash mismatch");
    Require(request.UnitId == request.Program.Unit.Name, "Wrong selected unit");
    Require(request.Program.Types.Count + request.Program.Functions.Count + request.Program.Axioms.Count +
      request.Program.Unit.Variables.Count + request.Obligations.Count <= Protocol.MaximumNodes,
      "Program declaration manifest exceeds resource bounds");
    var configuration = request.Configuration;
    Require(!string.IsNullOrWhiteSpace(configuration.SolverExecutable), "Missing solver executable");
    Require(configuration.TimeoutMilliseconds > 0 && configuration.ResourceLimit >= 0,
      "B3 requires a positive deadline and nonnegative resource budget");
    Require(configuration.MaximumResponseCharacters is > 0 and <= Protocol.MaximumMessageBytes,
      "Invalid solver response bound");
    Require(configuration.SolverVersion == "5.1.0" && IsHash(configuration.SolverSha256),
      "B3 requires the pinned Z3 5.1.0 executable identity");
    Require(configuration.ArithmeticSolver == 2, "This B3 protocol version supports arithmetic solver 2 only");
    Require(configuration.SolverArguments.SequenceEqual(new[] { "-in", "-smt2" }),
      "Unsupported solver arguments");
    var count = 0;
    long bitvectorBits = 0;
    var types = new HashSet<string>(StringComparer.Ordinal) { "bool", "int", "real" };
    foreach (var type in request.Program.Types) {
      Name(type);
      Require(types.Add(type), "Duplicate/reserved type name");
    }
    var functions = new Dictionary<string, Function>(StringComparer.Ordinal);
    foreach (var function in request.Program.Functions) {
      Name(function.Name);
      Require(functions.TryAdd(function.Name, function), "Duplicate function");
      ChargeType(function.ResultType);
      Bindings(function.Parameters);
    }
    var scope = Bindings(request.Program.Unit.Variables);
    Name(request.Program.Unit.Name);
    var obligations = new HashSet<string>(StringComparer.Ordinal);
    foreach (var source in request.Obligations) {
      Name(source.Id);
      Require(obligations.Add(source.Id), "Duplicate obligation identity");
      Require(source.Line >= 0 && source.Column >= 0 && source.Description is not null,
        "Invalid source identity");
    }
    var seenChecks = new HashSet<string>(StringComparer.Ordinal);
    foreach (var axiom in request.Program.Axioms) {
      foreach (var function in axiom.Explains) {
        Require(functions.ContainsKey(function), "Unknown axiom association");
      }
      Expr(axiom.Condition, new Dictionary<string, string>(), 0);
      Require(axiom.Condition.Type == "bool", "Non-boolean axiom");
    }
    Stmt(request.Program.Unit.Body, new HashSet<string>(), 0);
    Require(seenChecks.SetEquals(obligations), "Static obligation manifest differs from unit checks");

    void ChargeType(string type) {
      Require(types.Contains(type) || TryBitvectorWidth(type, out _), "Unknown normalized type");
      if (TryBitvectorWidth(type, out var width)) {
        bitvectorBits += width;
        Require(bitvectorBits <= Protocol.MaximumBitvectorBits, "Aggregate native bitvector cost exceeds resource bound");
      }
    }
    Dictionary<string, string> Bindings(IReadOnlyList<Binding> bindings, int depth = 0) {
      var result = new Dictionary<string, string>(StringComparer.Ordinal);
      foreach (var binding in bindings) {
        Visit(depth);
        Name(binding.Name);
        ChargeType(binding.Type);
        Require(result.TryAdd(binding.Name, binding.Type), "Duplicate binding");
      }
      return result;
    }
    void Visit(int depth) {
      Require(depth <= Protocol.MaximumDepth && ++count <= Protocol.MaximumNodes,
        "Normalized program exceeds resource bounds");
    }
    void Expr(Expression expression, IReadOnlyDictionary<string, string> environment, int depth) {
      Visit(depth);
      ChargeType(expression.Type);
      switch (expression) {
        case BooleanLiteral boolean:
          Require(boolean.Type == "bool", "Invalid boolean literal type");
          break;
        case IntegerLiteral integer:
          Require(integer.Type == "int" && integer.Value.Length <= Protocol.MaximumIntegerCharacters &&
            BigInteger.TryParse(integer.Value, System.Globalization.NumberStyles.AllowLeadingSign,
              System.Globalization.CultureInfo.InvariantCulture, out _), "Invalid exact integer literal");
          break;
        case RationalLiteral rational:
          ParseRationalLiteral(rational);
          break;
        case BitvectorLiteral word:
          ParseBitvectorLiteral(word);
          break;
        case BitvectorOperation bitvectorOperation:
          foreach (var argument in bitvectorOperation.Arguments) { Expr(argument, environment, depth + 1); }
          ValidateBitvectorOperation(bitvectorOperation);
          break;
        case Variable variable:
          Require(environment.TryGetValue(variable.Name, out var type) && type == variable.Type,
            "Unknown or ill-typed variable");
          break;
        case Application application:
          Require(functions.TryGetValue(application.Name, out var function), "Unknown function");
          Require(function!.ResultType == application.Type &&
            function.Parameters.Count == application.Arguments.Count, "Wrong function signature");
          for (var i = 0; i < application.Arguments.Count; i++) {
            Expr(application.Arguments[i], environment, depth + 1);
            Require(application.Arguments[i].Type == function.Parameters[i].Type,
              "Wrong function argument type");
          }
          break;
        case Operation operation:
          foreach (var argument in operation.Arguments) { Expr(argument, environment, depth + 1); }
          ValidateOperation(operation);
          break;
        case Quantifier quantifier:
          var bound = Bindings(quantifier.Bindings, depth + 1);
          var nested = new Dictionary<string, string>(environment);
          foreach (var binding in bound) { nested[binding.Key] = binding.Value; }
          Expr(quantifier.Body, nested, depth + 1);
          Require(quantifier.Body.Type == "bool", "Non-boolean quantified body");
          foreach (var pattern in quantifier.Patterns) {
            Require(pattern.Count > 0, "Empty trigger group");
            foreach (var term in pattern) { Expr(term, nested, depth + 1); }
          }
          break;
        case Let let:
          Visit(depth + 1);
          Name(let.Binding.Name);
          ChargeType(let.Binding.Type);
          Expr(let.Value, environment, depth + 1);
          Require(let.Binding.Type == let.Value.Type, "Ill-typed let binding");
          var letScope = new Dictionary<string, string>(environment) { [let.Binding.Name] = let.Binding.Type };
          Expr(let.Body, letScope, depth + 1);
          Require(let.Type == let.Body.Type, "Ill-typed let result");
          break;
        case Label label:
          Name(label.Name);
          Expr(label.Body, environment, depth + 1);
          Require(label.Type == label.Body.Type, "Ill-typed label");
          break;
        default:
          throw new InvalidDataException("Unknown normalized expression");
      }
    }
    void Stmt(Statement statement, HashSet<string> labels, int depth) {
      Visit(depth);
      switch (statement) {
        case Block block:
          foreach (var child in block.Statements) { Stmt(child, labels, depth + 1); }
          break;
        case Assign assign:
          Expr(assign.Value, scope, depth + 1);
          Require(scope.TryGetValue(assign.Variable, out var type) && type == assign.Value.Type,
            "Unknown or ill-typed assignment target");
          ChargeType(scope[assign.Variable]);
          break;
        case Havoc havoc:
          Require(havoc.Variables.Distinct().Count() == havoc.Variables.Count,
            "Repeated havoc target");
          foreach (var variable in havoc.Variables) {
            Require(scope.ContainsKey(variable), "Unknown havoc target");
            ChargeType(scope[variable]);
          }
          break;
        case Check check:
          Require(obligations.Contains(check.ObligationId), "Unknown check identity");
          Expr(check.Condition, scope, depth + 1);
          Require(check.Condition.Type == "bool", "Non-boolean check");
          seenChecks.Add(check.ObligationId);
          break;
        case Assume assume:
          Expr(assume.Condition, scope, depth + 1);
          Require(assume.Condition.Type == "bool", "Non-boolean assumption");
          break;
        case Choice choice:
          Require(choice.Branches.Count > 0, "Empty nondeterministic choice");
          foreach (var branch in choice.Branches) { Stmt(branch, labels, depth + 1); }
          break;
        case Conditional conditional:
          Expr(conditional.Condition, scope, depth + 1);
          Require(conditional.Condition.Type == "bool", "Non-boolean branch condition");
          Stmt(conditional.Then, labels, depth + 1);
          Stmt(conditional.Else, labels, depth + 1);
          break;
        case Loop loop:
          Require(loop.Invariants.Count == 0,
            "Loop invariant semantics are not supported by this protocol version");
          Stmt(loop.Body, labels, depth + 1);
          break;
        case Labeled labeled:
          Name(labeled.Name);
          Require(!labels.Contains(labeled.Name), "Shadowed control label");
          var nested = new HashSet<string>(labels) { labeled.Name };
          Stmt(labeled.Body, nested, depth + 1);
          break;
        case Exit exit:
          Require(labels.Contains(exit.Label), "Unknown exit label");
          break;
        case Return:
          break;
        default:
          throw new InvalidDataException("Unknown normalized statement");
      }
    }
  }

  public static void ValidateCompletion(Request request, Completion completion) {
    Require(completion.Version == Protocol.Version && completion.RequestId == request.RequestId &&
      completion.ProgramHash == request.ProgramHash && completion.UnitId == request.UnitId &&
      completion.B3Commit == request.B3Commit && completion.WorkerFingerprint == request.WorkerFingerprint, "Mismatched B3 completion identity");
    Require(Enum.IsDefined(completion.Outcome), "Unknown completion outcome");
    var obligations = request.Obligations.Select(source => source.Id).ToHashSet(StringComparer.Ordinal);
    Require(completion.Attempts.Count <= Protocol.MaximumNodes, "Proof-attempt response exceeds resource bounds");
    for (var i = 0; i < completion.Attempts.Count; i++) {
      var attempt = completion.Attempts[i];
      Require(attempt.Sequence == i && obligations.Contains(attempt.ObligationId) &&
        Enum.IsDefined(attempt.Outcome), "Invalid/out-of-order proof attempt");
      Require((attempt.Description?.Length ?? 0) <= 65536 && (attempt.Reason?.Length ?? 0) <= 65536 &&
        (attempt.Breadcrumbs?.Count ?? 0) <= Protocol.MaximumDepth &&
        (attempt.Breadcrumbs?.All(breadcrumb => breadcrumb != null && breadcrumb.Length <= 65536) ?? true),
        "Proof-attempt diagnostic exceeds resource bounds");
    }
    if (completion.Outcome == Outcome.Verified) {
      Require(completion.TraversalCompleted && completion.Error is null &&
        completion.Attempts.All(attempt => attempt.Outcome == Outcome.Verified),
        "Incomplete or failed unit cannot be verified");
    }
  }

  private static void ValidateOperation(Operation operation) {
    var args = operation.Arguments;
    var arity = operation.Operator switch {
      Operator.IfThenElse => 3, Operator.Not or Operator.Negate or Operator.ToReal or Operator.ToInt => 1, _ => 2
    };
    Require(Enum.IsDefined(operation.Operator) && args.Count == arity, "Invalid operator arity");
    var valid = operation.Operator switch {
      Operator.IfThenElse => args[0].Type == "bool" && args[1].Type == args[2].Type &&
        operation.Type == args[1].Type,
      Operator.Equiv or Operator.Implies or Operator.And or Operator.Or =>
        operation.Type == "bool" && args.All(argument => argument.Type == "bool"),
      Operator.Equal or Operator.NotEqual => operation.Type == "bool" && args[0].Type == args[1].Type,
      Operator.Less or Operator.LessEqual => operation.Type == "bool" &&
        IsNumeric(args[0].Type) && args[0].Type == args[1].Type,
      Operator.Not => operation.Type == "bool" && args[0].Type == "bool",
      Operator.Add or Operator.Subtract or Operator.Multiply or Operator.Negate =>
        IsNumeric(operation.Type) && args.All(argument => argument.Type == operation.Type),
      Operator.Divide or Operator.Modulo => operation.Type == "int" && args.All(argument => argument.Type == "int"),
      Operator.RealDivide => operation.Type == "real" && args.All(argument => argument.Type == "real"),
      Operator.ToReal => operation.Type == "real" && args[0].Type == "int",
      Operator.ToInt => operation.Type == "int" && args[0].Type == "real",
      _ => false
    };
    Require(valid, "Invalid operator signature");
  }
  /// <summary>Canonical positive bounded #bv width; malformed reserved spellings never become user types.</summary>
  public static bool TryBitvectorWidth(string type, out int width) {
    width = 0;
    if (type is not { Length: >= 4 and <= 7 } || !type.StartsWith("#bv", StringComparison.Ordinal) || type[3] == '0') {
      return false;
    }
    for (var i = 3; i < type.Length; i++) {
      if (type[i] is < '0' or > '9') { width = 0; return false; }
      width = width * 10 + type[i] - '0';
      if (width > Protocol.MaximumBitvectorWidth) { width = 0; return false; }
    }
    return width > 0;
  }

  public static BigInteger ParseBitvectorLiteral(BitvectorLiteral literal) {
    Require(literal.Width is > 0 and <= Protocol.MaximumBitvectorWidth &&
      literal.Type == Protocol.BitvectorTypeName(literal.Width), "Invalid bounded bitvector literal width or type");
    Require(literal.Value is { Length: > 0 } && literal.Value.Length <= Protocol.MaximumBitvectorLiteralCharacters &&
      (literal.Value == "0" || literal.Value[0] != '0') &&
      literal.Value.All(character => character is >= '0' and <= '9'), "Invalid canonical unsigned bitvector numeral");
    var value = BigInteger.Parse(literal.Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);
    Require(value >= 0 && value < (BigInteger.One << literal.Width), "Bitvector literal exceeds its word width");
    return value;
  }

  public static void ValidateBitvectorOperation(BitvectorOperation operation) {
    Require(Enum.IsDefined(operation.Operator) && operation.Width is > 0 and <= Protocol.MaximumBitvectorWidth,
      "Invalid native bitvector operator or width");
    var extract = operation.Operator == BitvectorOperator.Extract;
    Require(extract
      ? operation.Start >= 0 && operation.Start < operation.End && operation.End <= Protocol.MaximumBitvectorWidth &&
        operation.Width == operation.End - operation.Start
      : operation.Start == 0 && operation.End == 0, "Invalid native bitvector indices");
    var args = operation.Arguments;
    var unary = operation.Operator is BitvectorOperator.Not or BitvectorOperator.Extract or
      BitvectorOperator.IntToBitvector or BitvectorOperator.BitvectorToUnsignedInt;
    Require(args.Count == (unary ? 1 : 2), "Invalid native bitvector arity");
    var wordType = Protocol.BitvectorTypeName(operation.Width);
    var valid = operation.Operator switch {
      BitvectorOperator.IntToBitvector => operation.Type == wordType && args[0].Type == "int",
      BitvectorOperator.BitvectorToUnsignedInt => operation.Type == "int" && args[0].Type == wordType,
      BitvectorOperator.Extract => operation.Type == wordType && TryBitvectorWidth(args[0].Type, out var inputWidth) &&
        operation.End <= inputWidth,
      BitvectorOperator.Concat => operation.Type == wordType && TryBitvectorWidth(args[0].Type, out var leftWidth) &&
        TryBitvectorWidth(args[1].Type, out var rightWidth) && leftWidth + rightWidth == operation.Width,
      BitvectorOperator.UnsignedLess or BitvectorOperator.UnsignedLessEqual => operation.Type == "bool" &&
        args.All(argument => argument.Type == wordType),
      _ => operation.Type == wordType && args.All(argument => argument.Type == wordType)
    };
    Require(valid, "Invalid native bitvector signature");
  }

  private static bool IsNumeric(string type) => type is "int" or "real";

  public static (BigInteger Numerator, BigInteger Denominator) ParseRationalLiteral(RationalLiteral literal) {
    Require(literal.Type == "real" && literal.Numerator is { Length: > 0 } &&
      literal.Denominator is { Length: > 0 } &&
      literal.Numerator.Length <= Protocol.MaximumIntegerCharacters &&
      literal.Denominator.Length <= Protocol.MaximumIntegerCharacters, "Invalid bounded rational literal");
    var style = System.Globalization.NumberStyles.AllowLeadingSign;
    var culture = System.Globalization.CultureInfo.InvariantCulture;
    var validNumerator = BigInteger.TryParse(literal.Numerator, style, culture, out var numerator);
    var validDenominator = BigInteger.TryParse(literal.Denominator, style, culture, out var denominator);
    Require(validNumerator && validDenominator && denominator > 0,
      "Invalid exact rational literal or nonpositive denominator");
    return (numerator, denominator);
  }

  private static void Name(string name) {
    Require(!string.IsNullOrEmpty(name) && name[0] == 's' && name.Length <= 4096 &&
      name.All(character => char.IsAsciiLetterOrDigit(character)), "Illegal normalized name");
  }
  private static bool IsHash(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
  private static bool IsCommit(string value) => value is { Length: 40 } && value.All(Uri.IsHexDigit);
  private static void Require(bool condition, string message) {
    if (!condition) { throw new InvalidDataException(message); }
  }
}
