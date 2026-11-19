// Inspect the actual translator result, before Boogie task preparation or VC generation.
using System.Text.Json;
using Microsoft.Dafny;
using B = Microsoft.Boogie;

try {
  if (args.Length == 1 && args[0] == "--self-test") {
    var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "control.bpl"));
    if (B.Parser.Parse(text, "control.bpl", out var parsed) != 0) { throw new Exception("control parse"); }
    var settings = DafnyOptions.CreateUsingOldParser(Console.Error);
    Census.RequireTyped(parsed, settings);
    var census = new Census(); census.Visit(parsed);
    foreach (var feature in new[] { "where.Formal", "where.LocalVariable", "contract.free-requires",
      "contract.free-ensures", "contract.checked-requires", "contract.checked-ensures",
      "control.structured-preserved", "control.IfCmd", "control.WhileCmd", "attribute.census_control",
      "builtin.bvadd", "node.OldExpr", "node.ForallExpr", "node.LambdaExpr", "polymorphic.function",
      "operator.MapSelect.MapSelect", "visibility.hideable-axiom",
      "attribute.second_control", "attribute-value.subsumption.0" }) {
      if (census.Counts.GetValueOrDefault(feature) == 0) { throw new Exception("missing feature: " + feature); }
    }
    if (census.Counts.ContainsKey("attribute.inside_comment")) { throw new Exception("comment counted"); }
    if (B.Parser.Parse("procedure Q() { assert 0; }", "ill-typed.bpl", out var invalid) != 0) {
      throw new Exception("negative control must parse");
    }
    var rejected = false;
    try { Census.RequireTyped(invalid, settings); } catch (InvalidOperationException) { rejected = true; }
    if (!rejected) { throw new Exception("ill-typed input was accepted"); }
    Console.WriteLine("21 census controls passed"); return;
  }
  if (args.Length == 3 && args[0] == "--prelude") {
    var settings = DafnyOptions.CreateUsingOldParser(Console.Error);
    var defines = new List<string>();
    if (settings.Get(CommonOptionBag.UnicodeCharacters)) { defines.Add("UNICODE_CHAR"); }
    if (B.Parser.Parse(args[1], defines, out var parsed) != 0) { throw new Exception("prelude parsing"); }
    Census.RequireTyped(parsed, settings);
    var census = new Census(); census.Visit(parsed);
    File.WriteAllText(args[2], JsonSerializer.Serialize(new { Prelude = census.Counts },
      new JsonSerializerOptions { WriteIndented = true }) + "\n"); return;
  }
  var output = args[0];
  var roots = JsonSerializer.Deserialize<string[]>(File.ReadAllText(args[1]));
  var dependencies = JsonSerializer.Deserialize<string[]>(File.ReadAllText(args[2]));
  var config = JsonDocument.Parse(File.ReadAllText(args[3])).RootElement;
  var language = config.GetProperty("language_options");
  var options = DafnyOptions.CreateUsingOldParser(Console.Error, TextReader.Null, "/compile:0");
  options.Set(CommonOptionBag.TypeSystemRefresh, language.GetProperty("type-system-refresh").GetBoolean());
  options.Set(CommonOptionBag.GeneralTraits, Enum.Parse<CommonOptionBag.GeneralTraitsOptions>(
    language.GetProperty("general-traits").GetString(), ignoreCase: true));
  options.Set(CommonOptionBag.GeneralNewtypes, language.GetProperty("general-newtypes").GetBoolean());
  if (dependencies.Length > 0) {
    var library = config.GetProperty("standard_library_options");
    options.Set(MethodOrConstructor.ReadsClausesOnMethods, library.GetProperty("reads-clauses-on-methods").GetBoolean());
    options.Set(CommonOptionBag.EnforceDeterminism, library.GetProperty("enforce-determinism").GetBoolean());
    options.Set(PrintStmt.TrackPrintEffectsOption, library.GetProperty("track-print-effects").GetBoolean());
    options.ApplyBinding(CommonOptionBag.EnforceDeterminism);
    options.ApplyBinding(PrintStmt.TrackPrintEffectsOption);
  }
  var reporter = new ConsoleErrorReporter(options);
  var files = new List<DafnyFile>();
  foreach (var path in roots.Concat(dependencies).Distinct()) {
    await foreach (var file in DafnyFile.CreateAndValidate(OnDiskFileSystem.Instance, reporter, options,
      new Uri(Path.GetFullPath(path)), Token.NoToken, asLibrary: !roots.Contains(path))) {
      files.Add(file);
    }
  }
  var (program, error) = await DafnyMain.ParseCheck(TextReader.Null, files, "b3-census", options);
  if (error != null || reporter.ErrorCount != 0) { throw new Exception(error ?? "input errors"); }
  program.ProofDependencyManager = new ProofDependencyManager();
  var modules = await DafnyMain.LargeStackFactory.StartNew(() => {
    var result = new SortedDictionary<string, SortedDictionary<string, int>>();
    foreach (var (name, translated) in BoogieGenerator.Translate(program, program.Reporter)) {
      // Resolve/typecheck only. GetVerificationTasks would cross the census boundary.
      Census.RequireTyped(translated, options);
      var census = new Census();
      census.Visit(translated);
      if (!result.TryAdd(name, census.Counts)) { throw new Exception("duplicate module: " + name); }
    }
    if (program.Reporter.ErrorCount != 0 || result.Count == 0) { throw new Exception("empty/failed translation"); }
    return result;
  });
  File.WriteAllText(output, JsonSerializer.Serialize(modules, new JsonSerializerOptions { WriteIndented = true }) + "\n");

} catch (Exception error) {
  Console.Error.WriteLine(error.Message); Environment.ExitCode = 1;
}

sealed class Census : B.ReadOnlyVisitor {
  public static void RequireTyped(B.Program program, B.CoreOptions options) {
    if (program.Resolve(options) != 0 || program.Typecheck(options) != 0) {
      throw new InvalidOperationException("Boogie resolution/typecheck failed");
    }
  }
  public SortedDictionary<string, int> Counts = new();
  readonly HashSet<B.QKeyValue> attributes = new(ReferenceEqualityComparer.Instance);
  readonly HashSet<B.Variable> variables = new(ReferenceEqualityComparer.Instance);
  readonly HashSet<B.Absy> counted = new(ReferenceEqualityComparer.Instance);
  public void Add(string key) => Counts[key] = Counts.GetValueOrDefault(key) + 1;
  void Observe(B.Absy node) {
    if (node == null || !counted.Add(node)) { return; }
    Add("node." + node.GetType().Name);
    if (node is B.ICarriesAttributes carrier && carrier.Attributes != null) { VisitQKeyValue(carrier.Attributes); }
    if (node is B.NAryExpr call) { Add("operator." + call.Fun.GetType().Name + (call.Fun is B.FunctionCall ? "" : "." + call.Fun.FunctionName)); }
    if (node is B.Requires req) { Add(req.Free ? "contract.free-requires" : "contract.checked-requires"); }
    if (node is B.Ensures ens) { Add(ens.Free ? "contract.free-ensures" : "contract.checked-ensures"); }
    if (node is B.Function fun && fun.TypeParameters.Count > 0) { Add("polymorphic.function"); }
    if (node is B.CallCmd invocation && invocation.IsFree) { Add("call.free"); }
    if (node is B.MapType map && map.TypeParameters.Count > 0) { Add("polymorphic.map"); }
    if (node is B.Procedure proc && proc.TypeParameters.Count > 0) { Add("polymorphic.procedure"); }
    if (node is B.Axiom axiom && axiom.CanHide) { Add("visibility.hideable-axiom"); }
  }
  public override B.Absy Visit(B.Absy node) { Observe(node); return base.Visit(node); }
  public override B.Variable VisitVariable(B.Variable node) {
    Observe(node);
    if (!variables.Add(node)) { return node; }
    if (node.TypedIdent.WhereExpr != null && !counted.Contains(node.TypedIdent)) {
      Add("where." + node.GetType().Name); VisitExpr(node.TypedIdent.WhereExpr);
    }
    return base.VisitVariable(node);
  }
  public override B.TypedIdent VisitTypedIdent(B.TypedIdent node) { Observe(node); return base.VisitTypedIdent(node); }
  public override B.Constant VisitConstant(B.Constant node) {
    VisitVariable(node); foreach (var axiom in node.DefinitionAxioms) { Add("visibility.constant-definition-edge"); Visit(axiom); } return node;
  }
  public override B.Function VisitFunction(B.Function node) {
    foreach (var axiom in node.DefinitionAxioms) { Add("visibility.function-definition-edge"); Visit(axiom); }
    return base.VisitFunction(node);
  }
  public override B.Declaration VisitTypeSynonymDecl(B.TypeSynonymDecl node) {
    Visit(node.Body); return base.VisitTypeSynonymDecl(node);
  }
  public override B.Trigger VisitTrigger(B.Trigger node) { Observe(node); return base.VisitTrigger(node); }
  public override B.LocalVariable VisitLocalVariable(B.LocalVariable node) { VisitVariable(node); return node; }
  public override B.BoundVariable VisitBoundVariable(B.BoundVariable node) { VisitVariable(node); return node; }
  public override B.Formal VisitFormal(B.Formal node) { VisitVariable(node); return node; }
  public override B.Expr VisitIdentifierExpr(B.IdentifierExpr node) { Observe(node); return base.VisitIdentifierExpr(node); }
  public override B.Procedure VisitProcedure(B.Procedure node) { Observe(node); return base.VisitProcedure(node); }
  public override B.Requires VisitRequires(B.Requires node) { Observe(node); return base.VisitRequires(node); }
  public override B.Ensures VisitEnsures(B.Ensures node) { Observe(node); return base.VisitEnsures(node); }
  public override B.QKeyValue VisitQKeyValue(B.QKeyValue node) {
    if (attributes.Add(node)) {
      Observe(node); Add("attribute." + node.Key);
      if (node.Key is "subsumption" or "always_assume" or "inline" or "isolate") {
        Add("attribute-value." + node.Key + "." + string.Join(",", node.Params));
      }
      if (node.Key is "builtin" or "bvbuiltin") {
        Add("builtin." + string.Join(",", node.Params));
      }
    }
    return base.VisitQKeyValue(node);
  }
  public override B.Implementation VisitImplementation(B.Implementation node) {
    Add(node.StructuredStmts == null ? "control.unstructured-only" : "control.structured-preserved");
    Structured(node.StructuredStmts);
    return base.VisitImplementation(node);
  }
  void Structured(B.StmtList list) {
    if (list == null) { return; }
    foreach (var block in list.BigBlocks) {
      if (!block.Anonymous) { Add("control.label"); }
      Control(block.ec);
    }
  }
  void Control(B.StructuredCmd command) {
    if (command == null) { return; }
    Add("control." + command.GetType().Name);
    switch (command) {
      case B.IfCmd branch:
        if (branch.Attributes != null) { VisitQKeyValue(branch.Attributes); }
        if (branch.Guard != null) { VisitExpr(branch.Guard); }
        Structured(branch.Thn); Control(branch.ElseIf); Structured(branch.ElseBlock); break;
      case B.WhileCmd loop:
        if (loop.Guard != null) { VisitExpr(loop.Guard); }
        foreach (var invariant in loop.Invariants) { Visit(invariant); }
        Structured(loop.Body); break;
    }
  }
}
