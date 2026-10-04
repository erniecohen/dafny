// Copyright by the contributors to the Dafny Project
// SPDX-License-Identifier: MIT
using System.Numerics;
using Microsoft.Dafny;
using Xunit;
using Bpl = Microsoft.Boogie;
using Ir = DafnyB3Protocol;

namespace DafnyB3Normalizer.Test;

[Collection("B3 translation")]
public class B3IfGuardCertificateTests {
  private static string Header => "function {:bvbuiltin \"bv2int\"} N(x:bv3):int; function F(x:bv3):int; " +
    "axiom (forall b:bv3 :: { F(b) } ((0 <= F(b) && F(b) < 8) && F(b) == N(b))); ";
  private static (Bpl.Program Source, DafnyOptions Options) Parse(string body) {
    var options = new DafnyOptions(TextReader.Null, TextWriter.Null, TextWriter.Null);
    options.ApplyDefaultOptionsWithoutSettingsDefault();
    options.DafnyPrelude = Path.Combine(AppContext.BaseDirectory, "DafnyPrelude.bpl");
    Assert.Equal(0, Bpl.Parser.Parse(Header + "procedure P(x:bv3); implementation P(x:bv3) { " + body + " }",
      "B3IfGuardCertificateTests.bpl", out var source));
    Assert.Equal(0, source.Resolve(options)); Assert.Equal(0, source.Typecheck(options));
    return (source, options);
  }
  private static IEnumerable<Bpl.IfCmd> Ifs(Bpl.StmtList list) {
    foreach (var block in list.BigBlocks) {
      if (block.ec is Bpl.IfCmd conditional) {
        for (var current = conditional; current != null; current = current.ElseIf) {
          yield return current;
          foreach (var child in Ifs(current.Thn)) { yield return child; }
          if (current.ElseBlock != null) { foreach (var child in Ifs(current.ElseBlock)) { yield return child; } }
        }
      }
      if (block.ec is Bpl.WhileCmd loop) { foreach (var child in Ifs(loop.Body)) { yield return child; } }
    }
  }

  // G01-G10 cover complete producer topology, not a flat expression match.
  [Theory]
  [InlineData("if (F(x) >= 0) { assert true; } else { assert false; }", 1, "inlined-then", "inlined-else")]
  [InlineData("if (F(x) >= 0) { assert true; } assert true;", 1, "inlined-then", "negative-runoff")]
  [InlineData("if (F(x) >= 0) { assert F(x) < 8; } else { assert F(x) < 8; }", 1, "inlined-then", "inlined-else")]
  [InlineData("if (F(x) >= 0) { T: assert true; } else { assert true; }", 1, "dedicated-then", "inlined-else")]
  [InlineData("if (F(x) >= 0) { assert true; } else { E: assert true; }", 1, "inlined-then", "dedicated-else")]
  [InlineData("if (F(x) >= 0) { T: assert true; } else { E: assert true; }", 1, "dedicated-then", "dedicated-else")]
  [InlineData("if (F(x) >= 0) { assert true; } else if (F(x) < 8) { assert true; } else { assert false; }", 2, "inlined-then", "else-if-predecessor")]
  [InlineData("if (F(x) >= 0) { T: assert true; } else if (F(x) < 8) { U: assert true; }", 2, "dedicated-then", "else-if-predecessor")]
  [InlineData("if (F(x) >= 0) { if (F(x) < 8) { assert true; } } assert true;", 2, "inlined-then", "negative-runoff")]
  [InlineData("if (0 <= F(x)) { } assert F(x) < 8;", 1, "inlined-then", "negative-runoff")]
  public void ProducerOwnedPairsKeepTheirExactOwnerAndSlots(string body, int pairs, string positiveRole, string negativeRole) {
    var (source, _) = Parse(body); var unit = source.Implementations.Single();
    var catalogue = B3StructuredCfgCorrespondence.DescribeIfGuards(unit);
    Assert.Equal(pairs, catalogue.Count);
    var owners = Ifs(unit.StructuredStmts).ToArray();
    Assert.True(catalogue.TryGet(unit, owners[0], out var first));
    Assert.Equal(positiveRole, first.Positive.Role); Assert.Equal(negativeRole, first.Negative.Role);
    var commands = new HashSet<Bpl.Cmd>(ReferenceEqualityComparer.Instance);
    var paths = new HashSet<string>(StringComparer.Ordinal);
    foreach (var owner in owners) {
      Assert.True(catalogue.TryGet(unit, owner, out var certificate));
      Assert.Same(owner.Guard, certificate.Guard); Assert.True(paths.Add(certificate.SourcePath));
      Assert.Same(owner.Guard, certificate.Positive.Expression);
      foreach (var slot in new[] { certificate.Positive, certificate.Negative }) {
        Assert.Same(unit.Blocks[slot.BlockIndex], slot.Block);
        Assert.Same(slot.Block.Cmds[slot.CommandIndex], slot.Command);
        Assert.Same(slot.Command.Attributes, slot.Attributes);
        Assert.True(commands.Add(slot.Command));
      }
    }
    catalogue.Recheck();
  }
}
