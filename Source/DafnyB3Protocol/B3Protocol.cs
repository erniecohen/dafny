using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DafnyB3Protocol;

/// <summary>Serializable normalized IR, independent of Boogie and generated B3 runtime types.</summary>
public static class Protocol {
  public const int Version = 1;
  public const string NormalizerVersion = "experimental-1";
  public const int MaximumMessageBytes = 32 * 1024 * 1024;
  public const int MaximumNodes = 100000;
  public const int MaximumDepth = 128;
  public static string GetProgramHash(Program program) =>
    Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(program, JsonOptions))).ToLowerInvariant();
  public static readonly JsonSerializerOptions JsonOptions = new() {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    MaxDepth = 256,
    Converters = { new JsonStringEnumConverter() }
  };
}

public sealed record SourceIdentity(string Id, string Uri, int Line, int Column, string Description);
public sealed record Configuration(string SolverExecutable, IReadOnlyList<string> SolverArguments,
  int TimeoutMilliseconds, long ResourceLimit, int MaximumResponseCharacters, int ArithmeticSolver);
public sealed record Request(int Version, string RequestId, string NormalizerVersion,
  string B3Commit, string ProgramHash, string UnitId, Program Program,
  Configuration Configuration, IReadOnlyList<SourceIdentity> Obligations);
public sealed record Program(IReadOnlyList<string> Types, IReadOnlyList<Function> Functions,
  IReadOnlyList<Axiom> Axioms, Unit Unit);
public sealed record Binding(string Name, string Type);
public sealed record Function(string Name, IReadOnlyList<Binding> Parameters, string ResultType);
public sealed record Axiom(IReadOnlyList<string> Explains, Expression Condition);
/// <summary>Contracts and state are explicit statements; native B3 procedure specs are empty.</summary>
public sealed record Unit(string Name, IReadOnlyList<Binding> Variables, Statement Body);

public enum Operator {
  IfThenElse, Equiv, Implies, And, Or, Equal, NotEqual, Less, LessEqual,
  Add, Subtract, Multiply, Divide, Modulo, Not, Negate
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(BooleanLiteral), "boolean")]
[JsonDerivedType(typeof(IntegerLiteral), "integer")]
[JsonDerivedType(typeof(Variable), "variable")]
[JsonDerivedType(typeof(Application), "application")]
[JsonDerivedType(typeof(Operation), "operation")]
[JsonDerivedType(typeof(Quantifier), "quantifier")]
[JsonDerivedType(typeof(Let), "let")]
[JsonDerivedType(typeof(Label), "label")]
public abstract record Expression(string Type);
public sealed record BooleanLiteral(bool Value) : Expression("bool");
public sealed record IntegerLiteral(string Value) : Expression("int");
public sealed record Variable(string Name, string ResultType) : Expression(ResultType);
public sealed record Application(string Name, string ResultType, IReadOnlyList<Expression> Arguments)
  : Expression(ResultType);
public sealed record Operation(Operator Operator, string ResultType, IReadOnlyList<Expression> Arguments)
  : Expression(ResultType);
public sealed record Quantifier(bool Universal, IReadOnlyList<Binding> Bindings,
  IReadOnlyList<IReadOnlyList<Expression>> Patterns, Expression Body) : Expression("bool");
public sealed record Let(Binding Binding, Expression Value, Expression Body) : Expression(Body.Type);
public sealed record Label(string Name, Expression Body) : Expression(Body.Type);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Block), "block")]
[JsonDerivedType(typeof(Assign), "assign")]
[JsonDerivedType(typeof(Havoc), "havoc")]
[JsonDerivedType(typeof(Check), "check")]
[JsonDerivedType(typeof(Assume), "assume")]
[JsonDerivedType(typeof(Choice), "choice")]
[JsonDerivedType(typeof(Conditional), "conditional")]
[JsonDerivedType(typeof(Loop), "loop")]
[JsonDerivedType(typeof(Labeled), "labeled")]
[JsonDerivedType(typeof(Exit), "exit")]
[JsonDerivedType(typeof(Return), "return")]
public abstract record Statement;
public sealed record Block(IReadOnlyList<Statement> Statements) : Statement;
public sealed record Assign(string Variable, Expression Value) : Statement;
public sealed record Havoc(IReadOnlyList<string> Variables) : Statement;
public sealed record Check(string ObligationId, Expression Condition, bool Learn) : Statement;
public sealed record Assume(Expression Condition) : Statement;
public sealed record Choice(IReadOnlyList<Statement> Branches) : Statement;
public sealed record Conditional(Expression Condition, Statement Then, Statement Else) : Statement;
/// <summary>Invariant initialization/preservation is explicit in the body; loop headers are assumptions.</summary>
public sealed record Loop(IReadOnlyList<Expression> Invariants, Statement Body) : Statement;
public sealed record Labeled(string Name, Statement Body) : Statement;
public sealed record Exit(string Label) : Statement;
public sealed record Return : Statement;

public enum Outcome {
  Verified, Failed, Inconclusive, TimedOut, ResourceExhausted, OutOfMemory,
  Cancelled, Unsupported, ToolError
}
public sealed record Attempt(int Sequence, string ObligationId, Outcome Outcome, string? Reason);
public sealed record Completion(int Version, string RequestId, string ProgramHash, string UnitId,
  string B3Commit, bool TraversalCompleted, Outcome Outcome,
  IReadOnlyList<Attempt> Attempts, string? Error);

public sealed record WorkerStarted(int Version, string RequestId, int Sequence, int ProcessId, bool IsolatedProcessGroup);
