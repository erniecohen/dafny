using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Configuration;
using System.IO;
using System.Linq;
using Dafny;
using DCOMP;

namespace Microsoft.Dafny.Compilers {

  class RustCodeGenerator : DafnyWrittenCodeGenerator {
    public DafnyOptions Options { get; set; }
    public Program Program { get; set; } = null!;

    public RustCodeGenerator(DafnyOptions options) {
      this.Options = options;
    }

    private COMP CreateCompiler() {
      var c = new DCOMP.COMP();
      var charType = Options.Get(CommonOptionBag.UnicodeCharacters)
        ? Defs.CharType.create_UTF32()
        : Defs.CharType.create_UTF16();
      var pointerType = Options.Get(CommonOptionBag.RawPointers)
        ? Defs.PointerType.create_Raw()
        : Defs.PointerType.create_RcMut();
      var rootType = Options.Get(RustBackend.RustModuleNameOption) is { } opt && opt != "" ?
        Defs.RootType.create_RootPath(Sequence<Rune>.UnicodeFromString(opt))
        : Defs.RootType.create_RootCrate();
      var syncType = Options.Get(RustBackend.RustSyncOption)
        ? Defs.SyncType.create_Sync()
        : Defs.SyncType.create_NoSync();
      c.__ctor(charType, pointerType, rootType, syncType);
      return c;
    }

    public override void Compile(Sequence<DAST.Module> program, Sequence<ISequence<Rune>> otherFiles, ConcreteSyntaxTree w) {
      var c = CreateCompiler();
      var s = c.Compile(GroupModules(program, c), otherFiles);
      if (!Options.Get(CommonOptionBag.EmitUncompilableCode) && c.error.is_Some) {
        throw new UnsupportedInvalidOperationException(Token.NoToken, c.error.dtor_value.ToVerbatimString(false));
      }
      // We do this check afterwards for better code coverage
      if (!Options.Get(CommonOptionBag.EnforceDeterminism)) {
        // DEV: This requirement could be lifted in the future if
        // BoogieGenerator.DefiniteAssignment.cs:
        // the line
        //   if (!isGhost && type.HasCompilableValue) {
        // could become
        //   if (!isGhost && type.HasCompilableValue && options.DefiniteAssignmentLevel == 1) {
        // Meaning that the default behavior for fields and array initialization is the same as for local variables:
        // Auto-init is not supported, fields have to be initialized.

        throw new UnsupportedInvalidOperationException(Token.NoToken,
          "The Rust compiler requires `--enforce-determinism`");
      }
      w.Write(s.ToVerbatimString(false));
    }

    // Rust permits mutually referring modules. Keep each member's namespace inside
    // one group container, and publish its contents through a public facade at the
    // original path. Qualified references, enum constructors and translation-record
    // clients then use the same interface as an independently translated module.
    private ISequence<DAST.Module> GroupModules(Sequence<DAST.Module> modules, COMP compiler) {
      var groups = Program.CompileModules.Where(module => module.RecursiveGroup != null)
        .GroupBy(module => module.RecursiveGroup)
        .Select(group => group.OrderBy(module => module.FullDafnyName, StringComparer.Ordinal).ToList())
        .OrderBy(group => group[0].FullDafnyName, StringComparer.Ordinal).ToList();
      if (groups.Count == 0) {
        return modules;
      }
      var byName = modules.Elements.ToDictionary(module => module.dtor_name.ToVerbatimString(false));
      var reserved = byName.Keys.Select(name => RustPath(name.Split('.')[0])).ToHashSet();
      var groupedNames = new HashSet<string>();
      var result = new List<DAST.Module>();
      var facades = new Dictionary<string, string>();
      foreach (var group in groups) {
        var names = group.Select(module => module.GetCompileName(Options)).Where(byName.ContainsKey).ToList();
        if (names.Count == 0) {
          continue;
        }
        var groupName = "_dafny_recursive_group_" + group[0].GetCompileName(Options).Replace('.', '_');
        while (!reserved.Add(RustPath(groupName))) {
          groupName += "_";
        }
        var members = names.Select(name => DAST.ModuleItem.create_Module(byName[name])).ToArray();
        result.Add(new DAST.Module(Sequence<Rune>.UnicodeFromString(groupName), Sequence<Rune>.Empty,
          Sequence<DAST._IAttribute>.Empty, false,
          Std.Wrappers.Option<ISequence<DAST._IModuleItem>>.create_Some(Sequence<DAST._IModuleItem>.FromArray(members))));
        foreach (var name in names) {
          groupedNames.Add(name);
          var module = byName[name];
          result.Add(new DAST.Module(module.dtor_name, Sequence<Rune>.Empty, Sequence<DAST._IAttribute>.Empty,
            false, Std.Wrappers.Option<ISequence<DAST._IModuleItem>>.create_Some(Sequence<DAST._IModuleItem>.Empty)));
          facades[RustPath(name)] = RustPath(groupName) + "::" + RustPath(name);
        }
      }
      result.AddRange(modules.Elements.Where(module => !groupedNames.Contains(module.dtor_name.ToVerbatimString(false))));
      var root = compiler.thisFile;
      RAST._IMod PublishMembers(RAST._IMod module, string parent) {
        if (!module.is_Mod) {
          return module;
        }
        var path = parent + module.dtor_name.ToVerbatimString(false);
        var body = module.dtor_body.Elements.Select(item => item.is_ModDecl
          ? RAST.ModDecl.create_ModDecl(PublishMembers(item.dtor_mod, path + "::")) : item).ToList();
        if (facades.TryGetValue(path, out var target)) {
          var importPath = root;
          foreach (var segment in target.Split("::")) {
            importPath = importPath.MSel(Sequence<Rune>.UnicodeFromString(segment));
          }
          body.Add(RAST.ModDecl.create_UseDecl(RAST.Use.create(RAST.Visibility.create_PUB(),
            importPath.MSel(Sequence<Rune>.UnicodeFromString("*")))));
        }
        return RAST.Mod.create_Mod(module.dtor_docString, module.dtor_attributes, module.dtor_name,
          Sequence<RAST._IModDecl>.FromArray(body.ToArray()));
      }
      compiler.optimizations = Sequence<Func<RAST._IMod, RAST._IMod>>.Concat(
        Sequence<Func<RAST._IMod, RAST._IMod>>.FromElements(module => PublishMembers(module, "")), compiler.optimizations);
      return Sequence<DAST.Module>.FromArray(result.ToArray());
    }

    private static string RustPath(string name) => string.Join("::", name.Split('.').Select(segment =>
      Defs.__default.escapeName(Sequence<Rune>.UnicodeFromString(segment)).ToVerbatimString(false)));

    public override ISequence<Rune> EmitCallToMain(
      DAST.Expression companion,
      Sequence<Rune> mainMethodName,
      bool hasArguments) {
      var c = CreateCompiler();
      var result = c.EmitCallToMain(companion, mainMethodName, hasArguments);
      if (!Options.Get(CommonOptionBag.EmitUncompilableCode) && c.error.is_Some) {
        throw new UnsupportedInvalidOperationException(Token.NoToken, c.error.dtor_value.ToVerbatimString(false));
      }

      return result;
    }
  }

}
