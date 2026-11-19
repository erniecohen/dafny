// https://github.com/erniecohen/dafny/issues/425#issuecomment-6915818313
// Proof-cost reproducer: lemma NoWidePreserved.
//   dafny verify repro.dfy --boogie "/normalizeDeclarationOrder:0 /structuralLiterals" \
//     --heap-free-lambda-facts --general-traits datatype --enforce-determinism \
//     --resource-limit 64000000 --verification-time-limit 0 --cores 2 \
//     --filter-symbol=NoWidePreserved   [--legacy-noncanonical-map-payloads]
// Solver: Z3 5.1.0.
module M {
  type Node = nat
  type Ident = nat

  type Sig(==)
  type Loc(==)
  type Callee(==)
  type Op(==)
  type Chunk(==)
  type Mode(==)
  type AddrOp(==)
  datatype Extern = Extern(name: Ident, esig: Sig)
  datatype Data = Data(bytes: seq<int>)

  datatype Instr =
    | Nop(nsucc: Node)
    | Op(op: Op, oargs: seq<Loc>, odst: Loc, osucc: Node)
    | Store(schunk: Chunk, smode: Mode, sargs: seq<Loc>, ssrc: Loc, ssucc: Node)
    | Call2(csig: Sig, cfn: Callee, cargs: seq<Loc>, cdst: Loc, cdst2: Loc, csucc: Node)
    | Table(jarg: Loc, jtargets: seq<Node>, jdflt: Node)
    | Ret(res: Loc)
    | Ret2(res1: Loc, res2: Loc)
    | IntPtr(isrc: Loc, idst: Loc, isucc: Node)
    | Addr(aop: AddrOp, aargs: seq<Loc>, adst: Loc, asucc: Node)

  datatype Fn = Fn(fsig: Sig, entry: Node, code: map<Node, Instr>)
  datatype Def = Internal(ifn: Fn) | External(efn: Extern)
  datatype Prog<D> = Prog(gvars: seq<(Ident, seq<Data>)>, functs: seq<(Ident, D)>)

  function Successors(i: Instr): seq<Node>
  {
    match i
    case Nop(s) => [s]
    case Op(_, _, _, s) => [s]
    case Store(_, _, _, _, s) => [s]
    case Call2(_, _, _, _, _, s) => [s]
    case Table(_, targets, dflt) => targets + [dflt]
    case Ret(_) => []
    case Ret2(_, _) => []
    case IntPtr(_, _, s) => [s]
    case Addr(_, _, _, s) => [s]
  }

  function Chase(code: map<Node, Instr>, s: Node, fuel: nat): Node
    decreases fuel
  {
    if fuel > 0 && s in code && code[s].Nop? then Chase(code, code[s].nsucc, fuel - 1)
    else s
  }

  function Dest(f: Fn, s: Node): Node { Chase(f.code, s, |f.code|) }

  function Rewrite(f: Fn, i: Instr): (j: Instr)
    ensures |Successors(j)| == |Successors(i)|
    ensures forall k | 0 <= k < |Successors(i)| :: Successors(j)[k] == Dest(f, Successors(i)[k])
  {
    match i
    case Nop(s) => Nop(Dest(f, s))
    case Op(op, args, dst, s) => Op(op, args, dst, Dest(f, s))
    case Store(c, md, args, src, s) => Store(c, md, args, src, Dest(f, s))
    case Table(arg, targets, dflt) =>
      Table(arg, seq(|targets|, j requires 0 <= j < |targets| => Dest(f, targets[j])), Dest(f, dflt))
    case Ret(_) => i
    case IntPtr(src, dst, s) => IntPtr(src, dst, Dest(f, s))
    case Addr(op, args, dst, s) => Addr(op, args, dst, Dest(f, s))
    case Call2(sg, cf, args, dst, dst2, s) => Call2(sg, cf, args, dst, dst2, Dest(f, s))
    case Ret2(_, _) => i
  }

  function RwFn(f: Fn): (g: Fn)
    ensures g.entry == Dest(f, f.entry) && g.code.Keys == f.code.Keys
    ensures forall n :: n in f.code ==> g.code[n] == Rewrite(f, f.code[n])
  {
    f.(entry := Dest(f, f.entry),
       code := map n | n in f.code :: Rewrite(f, f.code[n]))
  }

  function RwDef(fd: Def): Def
  {
    match fd case Internal(f) => Internal(RwFn(f)) case External(_) => fd
  }

  function RwList(fs: seq<(Ident, Def)>): (qs: seq<(Ident, Def)>)
    ensures |qs| == |fs|
    ensures forall i :: 0 <= i < |fs| ==> qs[i] == (fs[i].0, RwDef(fs[i].1))
    decreases |fs|
  {
    if |fs| == 0 then [] else [(fs[0].0, RwDef(fs[0].1))] + RwList(fs[1..])
  }

  function RwProg(p: Prog<Def>): (q: Prog<Def>)
    ensures q.gvars == p.gvars
    ensures |q.functs| == |p.functs|
    ensures forall i :: 0 <= i < |p.functs| ==>
      q.functs[i] == (p.functs[i].0, RwDef(p.functs[i].1))
  {
    p.(functs := RwList(p.functs))
  }

  predicate NoWide(f: Fn)
  {
    forall n :: n in f.code ==> !f.code[n].Call2? && !f.code[n].Ret2?
  }

  lemma NoWidePreserved(p: Prog<Def>)
    requires forall i :: 0 <= i < |p.functs| && p.functs[i].1.Internal?
               ==> NoWide(p.functs[i].1.ifn)
    ensures forall i :: 0 <= i < |RwProg(p).functs| && RwProg(p).functs[i].1.Internal?
              ==> NoWide(RwProg(p).functs[i].1.ifn)
  {
    forall i | 0 <= i < |RwProg(p).functs| && RwProg(p).functs[i].1.Internal?
      ensures NoWide(RwProg(p).functs[i].1.ifn)
    {
      var f := p.functs[i].1.ifn;
      forall n | n in RwFn(f).code
        ensures !RwFn(f).code[n].Call2? && !RwFn(f).code[n].Ret2?
      {
        assert RwFn(f).code[n] == Rewrite(f, f.code[n]);
      }
    }
  }
}
