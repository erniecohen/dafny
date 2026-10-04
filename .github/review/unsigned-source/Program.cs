// Diagnostic source only: no verification engine, tasks, pruning or solver invocation.
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Dafny;
using Bpl = Microsoft.Boogie;

internal static class Program {
  const int MaximumNodes = 100000;
  const int MaximumDepth = 128;
  const int MaximumBlocks = 256;
  const long MaximumOutputBytes = 8 * 1024 * 1024;
  static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
  static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
  static void Require(bool condition, string message) { if (!condition) { throw new InvalidDataException(message); } }
  static object Token(Bpl.IToken token) => new { file = token.filename, line = token.line, column = token.col };
  static void Write(string path, object value) {
    var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
    Require(bytes.LongLength <= MaximumOutputBytes, "Diagnostic output exceeds8MiB"); File.WriteAllBytes(path, bytes);
  }

  public static async Task<int> Main(string[] args) {
    Require(args.Length == 3, "Usage: UnsignedSourceProbe FIXTURE OUTPUT EXPECTED_FIXTURE_SHA256");
    var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
    var receipt = new Dictionary<string, object?> { ["diagnosticOnly"] = true, ["acceptanceClaimed"] = false,
      ["verificationAttempted"] = false, ["allUnitsCaptured"] = false, ["units"] = new List<object>() };
    var units = (List<object>)receipt["units"]!;
    try {
      var fixture = File.ReadAllBytes(args[0]);
      Require(fixture.Length == 201 && Digest(fixture) == args[2], "Exact test raw-literal bytes required");
      var text = new UTF8Encoding(false, true).GetString(fixture);
      receipt["fixtureBytes"] = fixture.Length; receipt["fixtureSha256"] = Digest(fixture);
      receipt["fixtureUri"] = "file:///B3VisibilityTests.dfy";
      receipt["initialLoadedAssemblies"] = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && a.GetName().Name is { } name &&
        (name.StartsWith("Dafny", StringComparison.Ordinal) || name.StartsWith("Boogie.", StringComparison.Ordinal)))
        .OrderBy(a => a.GetName().Name).Select(a => new { name = a.GetName().Name, identity = a.FullName,
          sha256 = Digest(File.ReadAllBytes(a.Location)) }).ToArray();
      // Match B3DefinitionContextTests.Dafny, including defaults, prelude, URI and scope reset.
      Microsoft.Dafny.Type.ResetScopes();
      var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
      options.ApplyDefaultOptionsWithoutSettingsDefault();
      options.DafnyPrelude = Path.Combine(AppContext.BaseDirectory, "DafnyPrelude.bpl");
      var reporter = new BatchErrorReporter(options);
      var parsed = await ProgramParser.Parse(text, new Uri("file:///B3VisibilityTests.dfy"), reporter);
      await new ProgramResolver(parsed.Program).Resolve(CancellationToken.None);
      receipt["dafnyDiagnostics"] = reporter.AllMessages.Select(m => new { message = m.Message }).ToArray();
      Require(!reporter.HasErrors, "Dafny parse/resolution failed");
      var moduleIndex = 0;
      foreach (var (moduleName, source) in BoogieGenerator.Translate(parsed.Program, reporter)) {
        Require(moduleIndex < 16, "Translated module inventory bound");
        var resolution = source.Resolve(options); var typecheck = source.Typecheck(options);
        Require(resolution == 0 && typecheck == 0, "Typed Boogie resolution/typecheck failed");
        var inventory = new Inventory(source);
        inventory.ValidateDeclarations();
        var implementations = source.Implementations.ToArray();
        Require(implementations.Length <= 16, "Implementation inventory bound");
        foreach (var implementation in implementations) { B3StructuredCfgCorrespondence.Validate(implementation); }
        var before = implementations.Select(implementation => inventory.Unit(implementation)).ToArray();
        var beforeBytes = JsonSerializer.SerializeToUtf8Bytes(before, JsonOptions);
        Require(beforeBytes.LongLength <= MaximumOutputBytes, "Typed source inventory exceeds8MiB");
        // Exact public printer overload: no desugaring, and setTokens=false.
        var printed = Path.Combine(output, "module-" + moduleIndex + ".bpl");
        Bpl.ExecutionEngine.PrintBplFile(options, printed, source, false, false);
        Require(new FileInfo(printed).Length <= MaximumOutputBytes, "Printed BPL exceeds8MiB");
        var printedHash = Digest(File.ReadAllBytes(printed));
        for (var i = 0; i < implementations.Length; i++) {
          Require(units.Count < 16, "Total unit inventory bound");
          var implementation = implementations[i];
          var result = B3Normalizer.Normalize(source, implementation, options);
          units.Add(new { index = units.Count, moduleIndex, moduleName, implementationIndex = i,
            name = implementation.Name, procedure = implementation.Proc.Name,
            source = before[i], normalization = new { success = result.Success,
              diagnostics = result.Diagnostics.Select(d => new { code = d.Code, message = d.Message, token = Token(d.Token) }).ToArray(),
              obligations = result.Obligations, approximations = result.Approximations,
              contextCount = result.Contexts?.Count, programPresent = result.Program != null } });
        }
        var afterBytes = JsonSerializer.SerializeToUtf8Bytes(implementations.Select(implementation => inventory.Unit(implementation)).ToArray(), JsonOptions);
        Require(beforeBytes.SequenceEqual(afterBytes), "Normalizer changed captured source identity/field inventory");
        receipt["sourceInventorySha256-" + moduleIndex] = Digest(beforeBytes);
        receipt["sourceInventoryUnchanged-" + moduleIndex] = true;
        receipt["printedBplSha256-" + moduleIndex] = printedHash;
        moduleIndex++;
      }
      Require(units.Count == 3, "All three original implementations must be captured");
      receipt["unitCount"] = units.Count; receipt["moduleCount"] = moduleIndex;
      receipt["finalLoadedAssemblies"] = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && a.GetName().Name is { } name &&
        (name.StartsWith("Dafny", StringComparison.Ordinal) || name.StartsWith("Boogie.", StringComparison.Ordinal)))
        .OrderBy(a => a.GetName().Name).Select(a => new { name = a.GetName().Name, identity = a.FullName,
          sha256 = Digest(File.ReadAllBytes(a.Location)) }).ToArray();
      receipt["allUnitsCaptured"] = true;
    } catch (Exception error) {
      receipt["failure"] = error.GetType().Name + ": " + error.Message;
    }
    Write(Path.Combine(output, "typed-source.json"), receipt);
    Console.WriteLine("Typed unsigned source diagnostic; all units captured: " + receipt["allUnitsCaptured"] + "; acceptance: False");
    return 0;
  }

  sealed class Inventory {
    readonly Bpl.Program source;
    readonly Dictionary<object, int> identities = new(ReferenceEqualityComparer.Instance);
    int nodes;
    int Id(object? value) {
      if (value == null) { return 0; }
      if (!identities.TryGetValue(value, out var id)) {
        Require(identities.Count < MaximumNodes, "Reference identity inventory bound");
        id = identities.Count + 1; identities.Add(value, id);
      }
      return id;
    }
    void Charge(int depth) { Require(depth < MaximumDepth && ++nodes <= MaximumNodes, "Source node/depth inventory bound"); }
    public Inventory(Bpl.Program source) { this.source = source; }
    public void ValidateDeclarations() {
      Require(source.TopLevelDeclarations.Count() <= MaximumNodes, "Top-level declaration bound");
      foreach (var declaration in source.TopLevelDeclarations) { Id(declaration); }
    }
    object Attributes(Bpl.QKeyValue? attributes) {
      var list = new List<object>();
      for (var current = attributes; current != null; current = current.Next) {
        Require(list.Count < MaximumDepth, "Attribute chain bound");
        list.Add(new { id = Id(current), key = current.Key,
          arguments = current.Params.Select(value => value is Bpl.Expr e ? (object)new { expression = Id(e) } : new { literal = value.ToString() }).ToArray() });
      }
      return list;
    }
    object Variable(Bpl.Variable variable, string path) => new { id = Id(variable), name = variable.Name,
      type = variable.TypedIdent.Type.ToString(), where = variable.TypedIdent.WhereExpr == null ? null : Expression(variable.TypedIdent.WhereExpr, path + "/where") };
    object Expression(Bpl.Expr expression, string root, int initialDepth = 0) {
      var rows = new List<object>();
      var pending = new Stack<(Bpl.Expr Expr, string Path, int Depth)>(); pending.Push((expression, root, initialDepth));
      while (pending.Count > 0) {
        var (current, path, depth) = pending.Pop(); Charge(depth);
        var children = Children(current).ToArray();
        var function = current is Bpl.NAryExpr { Fun: Bpl.FunctionCall call } ? call.Func : null;
        rows.Add(new { path, id = Id(current), kind = current.GetType().FullName, type = current.Type?.ToString(), token = Token(current.tok),
          operation = current is Bpl.NAryExpr applied ? applied.Fun.FunctionName : null,
          resolvedTypeInstantiationCount = current is Bpl.NAryExpr typed ? typed.TypeParameters?.FormalTypeParams.Count : null,
          function = function == null ? null : new { id = Id(function), name = function.Name,
            topLevel = source.TopLevelDeclarations.Any(d => ReferenceEquals(d, function)), actualBody = function.Body != null,
            typeParameters = function.TypeParameters.Count, inputs = function.InParams.Select(p => new { id = Id(p), type = p.TypedIdent.Type.ToString() }).ToArray(),
            outputs = function.OutParams.Select(p => new { id = Id(p), type = p.TypedIdent.Type.ToString() }).ToArray(), attributes = Attributes(function.Attributes) },
          identifier = current is Bpl.IdentifierExpr identifier ? new { declaration = Id(identifier.Decl), name = identifier.Name } : null,
          children = children.Select(child => new { slot = child.Slot, id = Id(child.Expr) }).ToArray() });
        for (var i = children.Length - 1; i >= 0; i--) { pending.Push((children[i].Expr, path + "/" + children[i].Slot, depth + 1)); }
      }
      return new { root = Id(expression), occurrences = rows };
    }
    static IEnumerable<(string Slot, Bpl.Expr Expr)> Children(Bpl.Expr expression) {
      switch (expression) {
        case Bpl.NAryExpr applied:
          for (var i = 0; i < applied.Args.Count; i++) { yield return ("arg" + i, applied.Args[i]); } break;
        case Bpl.OldExpr old: yield return ("old", old.Expr); break;
        case Bpl.BvExtractExpr extract: yield return ("word", extract.Bitvector); break;
        case Bpl.BvConcatExpr concat: yield return ("left", concat.E0); yield return ("right", concat.E1); break;
        case Bpl.QuantifierExpr quantified:
          yield return ("body", quantified.Body);
          var t = 0;
          for (var trigger = quantified.Triggers; trigger != null; trigger = trigger.Next) {
            Require(++t <= MaximumDepth, "Trigger chain bound");
            for (var i = 0; i < trigger.Tr.Count; i++) { yield return ("trigger" + (t - 1) + "/term" + i, trigger.Tr[i]); }
          }
          break;
        case Bpl.LetExpr let:
          for (var i = 0; i < let.Rhss.Count; i++) { yield return ("rhs" + i, let.Rhss[i]); }
          yield return ("body", let.Body); break;
        case Bpl.LiteralExpr or Bpl.IdentifierExpr: break;
        default: throw new InvalidDataException("Unknown diagnostic expression constructor: " + expression.GetType().FullName);
      }
    }
    public object Unit(Bpl.Implementation implementation) {
      nodes = 0;
      Require(implementation.Blocks.Count is > 0 and <= MaximumBlocks && implementation.Proc != null && implementation.StructuredStmts != null,
        "Resolved structured unit/raw block bound");
      var commands = new List<object>(); var assumptions = new List<(Bpl.AssumeCmd Command, string Path)>();
      var guards = new List<object>(); var structuredBlocks = new List<object>(); var rawBlocks = new List<object>();
      var seen = new HashSet<Bpl.Cmd>(ReferenceEqualityComparer.Instance);
      for (var b = 0; b < implementation.Blocks.Count; b++) {
        var block = implementation.Blocks[b]; Charge(0);
        rawBlocks.Add(new { index = b, id = Id(block), label = block.Label, commands = block.Cmds.Select(Id).ToArray(), transfer = Id(block.TransferCmd),
          targets = block.TransferCmd is Bpl.GotoCmd branch ? branch.LabelTargets.Select(Id).ToArray() : Array.Empty<int>() });
        for (var c = 0; c < block.Cmds.Count; c++) { Command(block.Cmds[c], "raw/block" + b + "/command" + c, Id(block), Array.Empty<int>(), 0); }
        commands.Add(new { path = "raw/block" + b + "/transfer", block = Id(block), id = Id(block.TransferCmd), kind = block.TransferCmd.GetType().FullName,
          targets = block.TransferCmd is Bpl.GotoCmd jump ? jump.LabelTargets.Select(Id).ToArray() : Array.Empty<int>() });
      }
      Structured(implementation.StructuredStmts, "structured", 0);
      return new { id = Id(implementation), name = implementation.Name, procedure = Id(implementation.Proc), token = Token(implementation.tok),
        cfgCorrespondenceValidated = true, rawBlockCount = implementation.Blocks.Count, rawCommandCount = seen.Count,
        rawBlocks, rawCommands = commands, structuredBlocks, structuredGuards = guards,
        inputs = implementation.Proc.InParams.Select((v, i) => Variable(v, "procedure/input" + i)).ToArray(),
        outputs = implementation.Proc.OutParams.Select((v, i) => Variable(v, "procedure/output" + i)).ToArray(),
        locals = implementation.LocVars.Select((v, i) => Variable(v, "local" + i)).ToArray(),
        globals = source.TopLevelDeclarations.OfType<Bpl.GlobalVariable>().Select((v, i) => Variable(v, "global" + i)).ToArray(),
        topLevelDeclarations = source.TopLevelDeclarations.Select((d, i) => new { ordinal = i, id = Id(d), kind = d.GetType().FullName,
          name = d is Bpl.NamedDeclaration named ? named.Name : null }).ToArray(),
        requires = implementation.Proc.Requires.Select((r, i) => new { id = Id(r), r.Free, expression = Expression(r.Condition, "requires" + i) }).ToArray(),
        ensures = implementation.Proc.Ensures.Select((r, i) => new { id = Id(r), r.Free, expression = Expression(r.Condition, "ensures" + i) }).ToArray() };

      void Command(Bpl.Cmd command, string path, int block, int[] containers, int depth) {
        Charge(depth); Require(seen.Add(command), "Raw command identity reused");
        var expressions = new List<object>();
        switch (command) {
          case Bpl.PredicateCmd predicate: expressions.Add(Expression(predicate.Expr, path + "/expr", depth + 1)); break;
          case Bpl.AssignCmd assign:
            for (var i = 0; i < assign.Rhss.Count; i++) { expressions.Add(Expression(assign.Rhss[i], path + "/rhs" + i, depth + 1)); } break;
          case Bpl.CallCmd call:
            for (var i = 0; i < call.Ins.Count; i++) { if (call.Ins[i] != null) { expressions.Add(Expression(call.Ins[i], path + "/in" + i, depth + 1)); } }
            Require(call.Proc != null, "Unresolved diagnostic call");
            for (var i = 0; i < call.Proc.Requires.Count; i++) { expressions.Add(Expression(call.Proc.Requires[i].Condition, path + "/callee-requires" + i, depth + 1)); }
            for (var i = 0; i < call.Proc.Ensures.Count; i++) { expressions.Add(Expression(call.Proc.Ensures[i].Condition, path + "/callee-ensures" + i, depth + 1)); }
            break;
        }
        if (command is Bpl.AssumeCmd assumed) { assumptions.Add((assumed, path)); }
        commands.Add(new { path, block, containers, id = Id(command), kind = command.GetType().FullName, token = Token(command.tok),
          attributes = command is Bpl.ICarriesAttributes carries ? Attributes(carries.Attributes) : null,
          assignmentTargets = command is Bpl.AssignCmd assignment ? assignment.Lhss.Select(lhs => new { kind = lhs.GetType().FullName,
            variable = Id(lhs.DeepAssignedVariable) }).ToArray() : null,
          stateLocals = command is Bpl.StateCmd scoped ? scoped.Locals.Select((v, i) => Variable(v, path + "/state-local" + i)).ToArray() : null,
          callee = command is Bpl.CallCmd called ? Id(called.Proc) : 0, expressions });
        if (command is Bpl.StateCmd state) {
          for (var i = 0; i < state.Cmds.Count; i++) { Command(state.Cmds[i], path + "/state" + i, block, containers.Append(Id(state)).ToArray(), depth + 1); }
        }
      }
      void Structured(Bpl.StmtList list, string path, int depth) {
        Charge(depth);
        structuredBlocks.Add(new { path, list = Id(list), prefixCommands = list.PrefixCommands?.Select(Id).ToArray() ?? Array.Empty<int>(),
          blocks = list.BigBlocks.Select(block => new { id = Id(block), label = block.LabelName, commands = block.simpleCmds.Select(Id).ToArray(),
            structuredCommand = Id(block.ec), transfer = Id(block.tc) }).ToArray() });
        for (var i = 0; i < list.BigBlocks.Count; i++) {
          var block = list.BigBlocks[i]; Charge(depth + 1);
          var at = path + "/block" + i;
          if (block.ec is Bpl.IfCmd conditional) { Conditional(conditional, at + "/if", depth + 2); }
          else if (block.ec is Bpl.WhileCmd loop) {
            if (loop.Guard != null) { Guard(loop, loop.Guard, at + "/while-guard", depth + 2); }
            Structured(loop.Body, at + "/while-body", depth + 2);
          } else { Require(block.ec == null || block.ec is Bpl.BreakCmd, "Unknown structured diagnostic constructor"); }
        }
      }
      void Conditional(Bpl.IfCmd conditional, string path, int depth) {
        Charge(depth);
        if (conditional.Guard != null) { Guard(conditional, conditional.Guard, path + "/guard", depth + 1); }
        Structured(conditional.Thn, path + "/then", depth + 1);
        if (conditional.ElseBlock != null) { Structured(conditional.ElseBlock, path + "/else", depth + 1); }
        if (conditional.ElseIf != null) { Conditional(conditional.ElseIf, path + "/else-if", depth + 1); }
      }
      void Guard(Bpl.StructuredCmd owner, Bpl.Expr guard, string path, int depth) {
        var positive = new List<object>(); var negative = new List<object>();
        foreach (var assumption in assumptions) {
          Charge(0);
          if (ReferenceEquals(assumption.Command.Expr, guard)) { positive.Add(Match(assumption)); }
          if (Negation(assumption.Command.Expr, guard)) { negative.Add(Match(assumption)); }
        }
        guards.Add(new { path, owner = Id(owner), kind = owner.GetType().FullName, token = Token(owner.tok), expression = Expression(guard, path, depth),
          rawPositive = positive, rawNegative = negative });
      }
      object Match((Bpl.AssumeCmd Command, string Path) match) => new { path = match.Path, command = Id(match.Command), expression = Id(match.Command.Expr),
        exactAssumeType = match.Command.GetType() == typeof(Bpl.AssumeCmd),
        exactPartitionAttribute = match.Command.Attributes is { Key: "partition", Params.Count: 0, Next: null } };
    }
    // Read-only description of the exact existing CFG validator's complement shapes.
    static bool Negation(Bpl.Expr actual, Bpl.Expr guard) {
      if (ReferenceEquals(guard, Bpl.Expr.True)) { return ReferenceEquals(actual, Bpl.Expr.False); }
      if (ReferenceEquals(guard, Bpl.Expr.False)) { return ReferenceEquals(actual, Bpl.Expr.True); }
      if (guard is Bpl.NAryExpr source) {
        if (source.Fun is Bpl.UnaryOperator { Op: Bpl.UnaryOperator.Opcode.Not } && source.Args.Count == 1) { return ReferenceEquals(actual, source.Args[0]); }
        if (source.Fun is Bpl.BinaryOperator op && source.Args.Count == 2) {
          if (op.Op == Bpl.BinaryOperator.Opcode.Iff && actual is Bpl.NAryExpr { Fun: Bpl.BinaryOperator { Op: Bpl.BinaryOperator.Opcode.Iff }, Args.Count: 2 } iff && ReferenceEquals(iff.Args[0], source.Args[0])) {
            return DirectNot(iff.Args[1], source.Args[1]) || DirectNot(source.Args[1], iff.Args[1]);
          }
          var replacement = op.Op switch {
            Bpl.BinaryOperator.Opcode.Eq => Bpl.BinaryOperator.Opcode.Neq, Bpl.BinaryOperator.Opcode.Neq => Bpl.BinaryOperator.Opcode.Eq,
            Bpl.BinaryOperator.Opcode.Lt => Bpl.BinaryOperator.Opcode.Le, Bpl.BinaryOperator.Opcode.Le => Bpl.BinaryOperator.Opcode.Lt,
            Bpl.BinaryOperator.Opcode.Ge => Bpl.BinaryOperator.Opcode.Gt, Bpl.BinaryOperator.Opcode.Gt => Bpl.BinaryOperator.Opcode.Ge,
            _ => (Bpl.BinaryOperator.Opcode?)null };
          if (replacement.HasValue) {
            var reverse = op.Op is not (Bpl.BinaryOperator.Opcode.Eq or Bpl.BinaryOperator.Opcode.Neq);
            return actual is Bpl.NAryExpr { Fun: Bpl.BinaryOperator changed, Args.Count: 2 } binary && changed.Op == replacement.Value &&
              ReferenceEquals(binary.Args[0], source.Args[reverse ? 1 : 0]) && ReferenceEquals(binary.Args[1], source.Args[reverse ? 0 : 1]);
          }
        }
      }
      return DirectNot(actual, guard);
    }
    static bool DirectNot(Bpl.Expr expression, Bpl.Expr operand) => expression is Bpl.NAryExpr {
      Fun: Bpl.UnaryOperator { Op: Bpl.UnaryOperator.Opcode.Not }, Args.Count: 1 } negation && ReferenceEquals(negation.Args[0], operand);
  }
}
