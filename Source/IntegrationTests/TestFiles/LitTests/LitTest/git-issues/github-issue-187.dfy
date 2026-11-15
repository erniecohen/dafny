// RUN: %run --no-verify --allow-warnings --target cs "%s" > "%t"
// RUN: %diff "%s.expect" "%t"

// Regression: the C# backend writes a Dafny identifier that is a C# keyword as a verbatim
// identifier, @name (https://github.com/erniecohen/dafny/issues/187).  It escaped only some
// keywords, so `protected` as a parameter name, or `record`, `file`, `required` or `scoped` as a
// type name, made C# that does not compile.  This program uses, in many positions, every C#
// keyword that can be a Dafny identifier: 52 of the 77 reserved keywords and 41 of the 46
// contextual ones.  The others are Dafny keywords too.

// Names of datatypes and classes.
module DatatypeNames {
  datatype base = Mk(n: int)
  datatype byte = Mk(n: int)
  datatype catch = Mk(n: int)
  datatype checked = Mk(n: int)
  datatype decimal = Mk(n: int)
  datatype default = Mk(n: int)
  datatype delegate = Mk(n: int)
  datatype do = Mk(n: int)
  datatype double = Mk(n: int)
  datatype enum = Mk(n: int)
  datatype event = Mk(n: int)
  datatype explicit = Mk(n: int)
  datatype extern = Mk(n: int)
  datatype finally = Mk(n: int)
  datatype fixed = Mk(n: int)
  datatype float = Mk(n: int)
  datatype foreach = Mk(n: int)
  datatype goto = Mk(n: int)
  datatype implicit = Mk(n: int)
  datatype interface = Mk(n: int)
  datatype internal = Mk(n: int)
  datatype lock = Mk(n: int)
  datatype long = Mk(n: int)
  datatype namespace = Mk(n: int)
  datatype operator = Mk(n: int)
  datatype out = Mk(n: int)
  datatype override = Mk(n: int)
  datatype params = Mk(n: int)
  datatype private = Mk(n: int)
  datatype protected = Mk(n: int)
  datatype public = Mk(n: int)
  datatype readonly = Mk(n: int)
  datatype ref = Mk(n: int)
  datatype sbyte = Mk(n: int)
  datatype sealed = Mk(n: int)
  datatype short = Mk(n: int)
  datatype sizeof = Mk(n: int)
  datatype stackalloc = Mk(n: int)
  datatype struct = Mk(n: int)
  datatype switch = Mk(n: int)
  datatype throw = Mk(n: int)
  datatype try = Mk(n: int)
  datatype typeof = Mk(n: int)
  datatype uint = Mk(n: int)
  datatype ulong = Mk(n: int)
  datatype unchecked = Mk(n: int)
  datatype unsafe = Mk(n: int)
  datatype ushort = Mk(n: int)
  datatype using = Mk(n: int)
  datatype virtual = Mk(n: int)
  datatype void = Mk(n: int)
  datatype volatile = Mk(n: int)
  datatype add = Mk(n: int)
  datatype allows = Mk(n: int)
  datatype alias = Mk(n: int)
  datatype and = Mk(n: int)
  datatype args = Mk(n: int)
  datatype ascending = Mk(n: int)
  datatype async = Mk(n: int)
  datatype await = Mk(n: int)
  datatype descending = Mk(n: int)
  datatype dynamic = Mk(n: int)
  datatype equals = Mk(n: int)
  datatype extension = Mk(n: int)
  datatype file = Mk(n: int)
  datatype from = Mk(n: int)
  datatype get = Mk(n: int)
  datatype global = Mk(n: int)
  datatype group = Mk(n: int)
  datatype init = Mk(n: int)
  datatype into = Mk(n: int)
  datatype join = Mk(n: int)
  datatype let = Mk(n: int)
  datatype managed = Mk(n: int)
  datatype nameof = Mk(n: int)
  datatype nint = Mk(n: int)
  datatype not = Mk(n: int)
  datatype notnull = Mk(n: int)
  datatype nuint = Mk(n: int)
  datatype on = Mk(n: int)
  datatype or = Mk(n: int)
  datatype orderby = Mk(n: int)
  datatype partial = Mk(n: int)
  datatype record = Mk(n: int)
  datatype remove = Mk(n: int)
  datatype required = Mk(n: int)
  datatype scoped = Mk(n: int)
  datatype select = Mk(n: int)
  datatype unmanaged = Mk(n: int)
  datatype value = Mk(n: int)
  datatype when = Mk(n: int)
  datatype where = Mk(n: int)
  datatype with = Mk(n: int)
}

module ClassNames {
  class base { var n: int  constructor (n: int) { this.n := n; } }
  class byte { var n: int  constructor (n: int) { this.n := n; } }
  class catch { var n: int  constructor (n: int) { this.n := n; } }
  class checked { var n: int  constructor (n: int) { this.n := n; } }
  class decimal { var n: int  constructor (n: int) { this.n := n; } }
  class default { var n: int  constructor (n: int) { this.n := n; } }
  class delegate { var n: int  constructor (n: int) { this.n := n; } }
  class do { var n: int  constructor (n: int) { this.n := n; } }
  class double { var n: int  constructor (n: int) { this.n := n; } }
  class enum { var n: int  constructor (n: int) { this.n := n; } }
  class event { var n: int  constructor (n: int) { this.n := n; } }
  class explicit { var n: int  constructor (n: int) { this.n := n; } }
  class extern { var n: int  constructor (n: int) { this.n := n; } }
  class finally { var n: int  constructor (n: int) { this.n := n; } }
  class fixed { var n: int  constructor (n: int) { this.n := n; } }
  class float { var n: int  constructor (n: int) { this.n := n; } }
  class foreach { var n: int  constructor (n: int) { this.n := n; } }
  class goto { var n: int  constructor (n: int) { this.n := n; } }
  class implicit { var n: int  constructor (n: int) { this.n := n; } }
  class interface { var n: int  constructor (n: int) { this.n := n; } }
  class internal { var n: int  constructor (n: int) { this.n := n; } }
  class lock { var n: int  constructor (n: int) { this.n := n; } }
  class long { var n: int  constructor (n: int) { this.n := n; } }
  class namespace { var n: int  constructor (n: int) { this.n := n; } }
  class operator { var n: int  constructor (n: int) { this.n := n; } }
  class out { var n: int  constructor (n: int) { this.n := n; } }
  class override { var n: int  constructor (n: int) { this.n := n; } }
  class params { var n: int  constructor (n: int) { this.n := n; } }
  class private { var n: int  constructor (n: int) { this.n := n; } }
  class protected { var n: int  constructor (n: int) { this.n := n; } }
  class public { var n: int  constructor (n: int) { this.n := n; } }
  class readonly { var n: int  constructor (n: int) { this.n := n; } }
  class ref { var n: int  constructor (n: int) { this.n := n; } }
  class sbyte { var n: int  constructor (n: int) { this.n := n; } }
  class sealed { var n: int  constructor (n: int) { this.n := n; } }
  class short { var n: int  constructor (n: int) { this.n := n; } }
  class sizeof { var n: int  constructor (n: int) { this.n := n; } }
  class stackalloc { var n: int  constructor (n: int) { this.n := n; } }
  class struct { var n: int  constructor (n: int) { this.n := n; } }
  class switch { var n: int  constructor (n: int) { this.n := n; } }
  class throw { var n: int  constructor (n: int) { this.n := n; } }
  class try { var n: int  constructor (n: int) { this.n := n; } }
  class typeof { var n: int  constructor (n: int) { this.n := n; } }
  class uint { var n: int  constructor (n: int) { this.n := n; } }
  class ulong { var n: int  constructor (n: int) { this.n := n; } }
  class unchecked { var n: int  constructor (n: int) { this.n := n; } }
  class unsafe { var n: int  constructor (n: int) { this.n := n; } }
  class ushort { var n: int  constructor (n: int) { this.n := n; } }
  class using { var n: int  constructor (n: int) { this.n := n; } }
  class virtual { var n: int  constructor (n: int) { this.n := n; } }
  class void { var n: int  constructor (n: int) { this.n := n; } }
  class volatile { var n: int  constructor (n: int) { this.n := n; } }
  class add { var n: int  constructor (n: int) { this.n := n; } }
  class allows { var n: int  constructor (n: int) { this.n := n; } }
  class alias { var n: int  constructor (n: int) { this.n := n; } }
  class and { var n: int  constructor (n: int) { this.n := n; } }
  class args { var n: int  constructor (n: int) { this.n := n; } }
  class ascending { var n: int  constructor (n: int) { this.n := n; } }
  class async { var n: int  constructor (n: int) { this.n := n; } }
  class await { var n: int  constructor (n: int) { this.n := n; } }
  class descending { var n: int  constructor (n: int) { this.n := n; } }
  class dynamic { var n: int  constructor (n: int) { this.n := n; } }
  class equals { var n: int  constructor (n: int) { this.n := n; } }
  class extension { var n: int  constructor (n: int) { this.n := n; } }
  class file { var n: int  constructor (n: int) { this.n := n; } }
  class from { var n: int  constructor (n: int) { this.n := n; } }
  class get { var n: int  constructor (n: int) { this.n := n; } }
  class global { var n: int  constructor (n: int) { this.n := n; } }
  class group { var n: int  constructor (n: int) { this.n := n; } }
  class init { var n: int  constructor (n: int) { this.n := n; } }
  class into { var n: int  constructor (n: int) { this.n := n; } }
  class join { var n: int  constructor (n: int) { this.n := n; } }
  class let { var n: int  constructor (n: int) { this.n := n; } }
  class managed { var n: int  constructor (n: int) { this.n := n; } }
  class nameof { var n: int  constructor (n: int) { this.n := n; } }
  class nint { var n: int  constructor (n: int) { this.n := n; } }
  class not { var n: int  constructor (n: int) { this.n := n; } }
  class notnull { var n: int  constructor (n: int) { this.n := n; } }
  class nuint { var n: int  constructor (n: int) { this.n := n; } }
  class on { var n: int  constructor (n: int) { this.n := n; } }
  class or { var n: int  constructor (n: int) { this.n := n; } }
  class orderby { var n: int  constructor (n: int) { this.n := n; } }
  class partial { var n: int  constructor (n: int) { this.n := n; } }
  class record { var n: int  constructor (n: int) { this.n := n; } }
  class remove { var n: int  constructor (n: int) { this.n := n; } }
  class required { var n: int  constructor (n: int) { this.n := n; } }
  class scoped { var n: int  constructor (n: int) { this.n := n; } }
  class select { var n: int  constructor (n: int) { this.n := n; } }
  class unmanaged { var n: int  constructor (n: int) { this.n := n; } }
  class value { var n: int  constructor (n: int) { this.n := n; } }
  class when { var n: int  constructor (n: int) { this.n := n; } }
  class where { var n: int  constructor (n: int) { this.n := n; } }
  class with { var n: int  constructor (n: int) { this.n := n; } }
}

// Names of modules.
module base { const n: int := 1 }
module byte { const n: int := 2 }
module catch { const n: int := 3 }
module checked { const n: int := 4 }
module decimal { const n: int := 5 }
module default { const n: int := 6 }
module delegate { const n: int := 7 }
module do { const n: int := 8 }
module double { const n: int := 9 }
module enum { const n: int := 10 }
module event { const n: int := 11 }
module explicit { const n: int := 12 }
module extern { const n: int := 13 }
module finally { const n: int := 14 }
module fixed { const n: int := 15 }
module float { const n: int := 16 }
module foreach { const n: int := 17 }
module goto { const n: int := 18 }
module implicit { const n: int := 19 }
module interface { const n: int := 20 }
module internal { const n: int := 21 }
module lock { const n: int := 22 }
module long { const n: int := 23 }
module namespace { const n: int := 24 }
module operator { const n: int := 25 }
module out { const n: int := 26 }
module override { const n: int := 27 }
module params { const n: int := 28 }
module private { const n: int := 29 }
module protected { const n: int := 30 }
module public { const n: int := 31 }
module readonly { const n: int := 32 }
module ref { const n: int := 33 }
module sbyte { const n: int := 34 }
module sealed { const n: int := 35 }
module short { const n: int := 36 }
module sizeof { const n: int := 37 }
module stackalloc { const n: int := 38 }
module struct { const n: int := 39 }
module switch { const n: int := 40 }
module throw { const n: int := 41 }
module try { const n: int := 42 }
module typeof { const n: int := 43 }
module uint { const n: int := 44 }
module ulong { const n: int := 45 }
module unchecked { const n: int := 46 }
module unsafe { const n: int := 47 }
module ushort { const n: int := 48 }
module using { const n: int := 49 }
module virtual { const n: int := 50 }
module void { const n: int := 51 }
module volatile { const n: int := 52 }
module add { const n: int := 53 }
module allows { const n: int := 54 }
module alias { const n: int := 55 }
module and { const n: int := 56 }
module args { const n: int := 57 }
module ascending { const n: int := 58 }
module async { const n: int := 59 }
module await { const n: int := 60 }
module descending { const n: int := 61 }
module dynamic { const n: int := 62 }
module equals { const n: int := 63 }
module extension { const n: int := 64 }
module file { const n: int := 65 }
module from { const n: int := 66 }
module get { const n: int := 67 }
module global { const n: int := 68 }
module group { const n: int := 69 }
module init { const n: int := 70 }
module into { const n: int := 71 }
module join { const n: int := 72 }
module let { const n: int := 73 }
module managed { const n: int := 74 }
module nameof { const n: int := 75 }
module nint { const n: int := 76 }
module not { const n: int := 77 }
module notnull { const n: int := 78 }
module nuint { const n: int := 79 }
module on { const n: int := 80 }
module or { const n: int := 81 }
module orderby { const n: int := 82 }
module partial { const n: int := 83 }
module record { const n: int := 84 }
module remove { const n: int := 85 }
module required { const n: int := 86 }
module scoped { const n: int := 87 }
module select { const n: int := 88 }
module unmanaged { const n: int := 89 }
module value { const n: int := 90 }
module when { const n: int := 91 }
module where { const n: int := 92 }
module with { const n: int := 93 }

// Names of module-level constants and functions, of fields, of destructors, of constructors,
// of in- and out-parameters, and of type parameters.
// Names of constructors, in a module of their own: a constructor is also visible unqualified in
// its module, where it would clash with the constants below.
module CtorNames {
  datatype Constructors = base | byte | catch | checked | decimal | default | delegate | do | double | enum | event | explicit | extern | finally | fixed | float | foreach | goto | implicit | interface | internal | lock | long | namespace | operator | out | override | params | private | protected | public | readonly | ref | sbyte | sealed | short | sizeof | stackalloc | struct | switch | throw | try | typeof | uint | ulong | unchecked | unsafe | ushort | using | virtual | void | volatile | add | allows | alias | and | args | ascending | async | await | descending | dynamic | equals | extension | file | from | get | global | group | init | into | join | let | managed | nameof | nint | not | notnull | nuint | on | or | orderby | partial | record | remove | required | scoped | select | unmanaged | value | when | where | with
}

module Members {
  const base: int := 1
  const byte: int := 2
  const catch: int := 3
  const checked: int := 4
  const decimal: int := 5
  const default: int := 6
  const delegate: int := 7
  const do: int := 8
  const double: int := 9
  const enum: int := 10
  const event: int := 11
  const explicit: int := 12
  const extern: int := 13
  const finally: int := 14
  const fixed: int := 15
  const float: int := 16
  const foreach: int := 17
  const goto: int := 18
  const implicit: int := 19
  const interface: int := 20
  const internal: int := 21
  const lock: int := 22
  const long: int := 23
  const namespace: int := 24
  const operator: int := 25
  const out: int := 26
  const override: int := 27
  const params: int := 28
  const private: int := 29
  const protected: int := 30
  const public: int := 31
  const readonly: int := 32
  const ref: int := 33
  const sbyte: int := 34
  const sealed: int := 35
  const short: int := 36
  const sizeof: int := 37
  const stackalloc: int := 38
  const struct: int := 39
  const switch: int := 40
  const throw: int := 41
  const try: int := 42
  const typeof: int := 43
  const uint: int := 44
  const ulong: int := 45
  const unchecked: int := 46
  const unsafe: int := 47
  const ushort: int := 48
  const using: int := 49
  const virtual: int := 50
  const void: int := 51
  const volatile: int := 52
  const add: int := 53
  const allows: int := 54
  const alias: int := 55
  const and: int := 56
  const args: int := 57
  const ascending: int := 58
  const async: int := 59
  const await: int := 60
  const descending: int := 61
  const dynamic: int := 62
  const equals: int := 63
  const extension: int := 64
  const file: int := 65
  const from: int := 66
  const get: int := 67
  const global: int := 68
  const group: int := 69
  const init: int := 70
  const into: int := 71
  const join: int := 72
  const let: int := 73
  const managed: int := 74
  const nameof: int := 75
  const nint: int := 76
  const not: int := 77
  const notnull: int := 78
  const nuint: int := 79
  const on: int := 80
  const or: int := 81
  const orderby: int := 82
  const partial: int := 83
  const record: int := 84
  const remove: int := 85
  const required: int := 86
  const scoped: int := 87
  const select: int := 88
  const unmanaged: int := 89
  const value: int := 90
  const when: int := 91
  const where: int := 92
  const with: int := 93

  module Functions {
    function base(): int { 1 }
    function byte(): int { 2 }
    function catch(): int { 3 }
    function checked(): int { 4 }
    function decimal(): int { 5 }
    function default(): int { 6 }
    function delegate(): int { 7 }
    function do(): int { 8 }
    function double(): int { 9 }
    function enum(): int { 10 }
    function event(): int { 11 }
    function explicit(): int { 12 }
    function extern(): int { 13 }
    function finally(): int { 14 }
    function fixed(): int { 15 }
    function float(): int { 16 }
    function foreach(): int { 17 }
    function goto(): int { 18 }
    function implicit(): int { 19 }
    function interface(): int { 20 }
    function internal(): int { 21 }
    function lock(): int { 22 }
    function long(): int { 23 }
    function namespace(): int { 24 }
    function operator(): int { 25 }
    function out(): int { 26 }
    function override(): int { 27 }
    function params(): int { 28 }
    function private(): int { 29 }
    function protected(): int { 30 }
    function public(): int { 31 }
    function readonly(): int { 32 }
    function ref(): int { 33 }
    function sbyte(): int { 34 }
    function sealed(): int { 35 }
    function short(): int { 36 }
    function sizeof(): int { 37 }
    function stackalloc(): int { 38 }
    function struct(): int { 39 }
    function switch(): int { 40 }
    function throw(): int { 41 }
    function try(): int { 42 }
    function typeof(): int { 43 }
    function uint(): int { 44 }
    function ulong(): int { 45 }
    function unchecked(): int { 46 }
    function unsafe(): int { 47 }
    function ushort(): int { 48 }
    function using(): int { 49 }
    function virtual(): int { 50 }
    function void(): int { 51 }
    function volatile(): int { 52 }
    function add(): int { 53 }
    function allows(): int { 54 }
    function alias(): int { 55 }
    function and(): int { 56 }
    function args(): int { 57 }
    function ascending(): int { 58 }
    function async(): int { 59 }
    function await(): int { 60 }
    function descending(): int { 61 }
    function dynamic(): int { 62 }
    function equals(): int { 63 }
    function extension(): int { 64 }
    function file(): int { 65 }
    function from(): int { 66 }
    function get(): int { 67 }
    function global(): int { 68 }
    function group(): int { 69 }
    function init(): int { 70 }
    function into(): int { 71 }
    function join(): int { 72 }
    function let(): int { 73 }
    function managed(): int { 74 }
    function nameof(): int { 75 }
    function nint(): int { 76 }
    function not(): int { 77 }
    function notnull(): int { 78 }
    function nuint(): int { 79 }
    function on(): int { 80 }
    function or(): int { 81 }
    function orderby(): int { 82 }
    function partial(): int { 83 }
    function record(): int { 84 }
    function remove(): int { 85 }
    function required(): int { 86 }
    function scoped(): int { 87 }
    function select(): int { 88 }
    function unmanaged(): int { 89 }
    function value(): int { 90 }
    function when(): int { 91 }
    function where(): int { 92 }
    function with(): int { 93 }
  }

  class Fields {
    var base: int
    var byte: int
    var catch: int
    var checked: int
    var decimal: int
    var default: int
    var delegate: int
    var do: int
    var double: int
    var enum: int
    var event: int
    var explicit: int
    var extern: int
    var finally: int
    var fixed: int
    var float: int
    var foreach: int
    var goto: int
    var implicit: int
    var interface: int
    var internal: int
    var lock: int
    var long: int
    var namespace: int
    var operator: int
    var out: int
    var override: int
    var params: int
    var private: int
    var protected: int
    var public: int
    var readonly: int
    var ref: int
    var sbyte: int
    var sealed: int
    var short: int
    var sizeof: int
    var stackalloc: int
    var struct: int
    var switch: int
    var throw: int
    var try: int
    var typeof: int
    var uint: int
    var ulong: int
    var unchecked: int
    var unsafe: int
    var ushort: int
    var using: int
    var virtual: int
    var void: int
    var volatile: int
    var add: int
    var allows: int
    var alias: int
    var and: int
    var args: int
    var ascending: int
    var async: int
    var await: int
    var descending: int
    var dynamic: int
    var equals: int
    var extension: int
    var file: int
    var from: int
    var get: int
    var global: int
    var group: int
    var init: int
    var into: int
    var join: int
    var let: int
    var managed: int
    var nameof: int
    var nint: int
    var not: int
    var notnull: int
    var nuint: int
    var on: int
    var or: int
    var orderby: int
    var partial: int
    var record: int
    var remove: int
    var required: int
    var scoped: int
    var select: int
    var unmanaged: int
    var value: int
    var when: int
    var where: int
    var with: int
    constructor () {
      base := 1;
      byte := 2;
      catch := 3;
      checked := 4;
      decimal := 5;
      default := 6;
      delegate := 7;
      do := 8;
      double := 9;
      enum := 10;
      event := 11;
      explicit := 12;
      extern := 13;
      finally := 14;
      fixed := 15;
      float := 16;
      foreach := 17;
      goto := 18;
      implicit := 19;
      interface := 20;
      internal := 21;
      lock := 22;
      long := 23;
      namespace := 24;
      operator := 25;
      out := 26;
      override := 27;
      params := 28;
      private := 29;
      protected := 30;
      public := 31;
      readonly := 32;
      ref := 33;
      sbyte := 34;
      sealed := 35;
      short := 36;
      sizeof := 37;
      stackalloc := 38;
      struct := 39;
      switch := 40;
      throw := 41;
      try := 42;
      typeof := 43;
      uint := 44;
      ulong := 45;
      unchecked := 46;
      unsafe := 47;
      ushort := 48;
      using := 49;
      virtual := 50;
      void := 51;
      volatile := 52;
      add := 53;
      allows := 54;
      alias := 55;
      and := 56;
      args := 57;
      ascending := 58;
      async := 59;
      await := 60;
      descending := 61;
      dynamic := 62;
      equals := 63;
      extension := 64;
      file := 65;
      from := 66;
      get := 67;
      global := 68;
      group := 69;
      init := 70;
      into := 71;
      join := 72;
      let := 73;
      managed := 74;
      nameof := 75;
      nint := 76;
      not := 77;
      notnull := 78;
      nuint := 79;
      on := 80;
      or := 81;
      orderby := 82;
      partial := 83;
      record := 84;
      remove := 85;
      required := 86;
      scoped := 87;
      select := 88;
      unmanaged := 89;
      value := 90;
      when := 91;
      where := 92;
      with := 93;
    }
    method Sum() returns (r: int) {
      r := base + byte + catch + checked + decimal + default + delegate + do + double + enum + event + explicit + extern + finally + fixed + float + foreach + goto + implicit + interface + internal + lock + long + namespace + operator + out + override + params + private + protected + public + readonly + ref + sbyte + sealed + short + sizeof + stackalloc + struct + switch + throw + try + typeof + uint + ulong + unchecked + unsafe + ushort + using + virtual + void + volatile + add + allows + alias + and + args + ascending + async + await + descending + dynamic + equals + extension + file + from + get + global + group + init + into + join + let + managed + nameof + nint + not + notnull + nuint + on + or + orderby + partial + record + remove + required + scoped + select + unmanaged + value + when + where + with;
    }
  }

  datatype Destructors0 = Destructors0(base: int, byte: int, catch: int, checked: int, decimal: int, default: int, delegate: int, do: int, double: int, enum: int, event: int, explicit: int, extern: int, finally: int, fixed: int, float: int)
  function AddAll0(base: int, byte: int, catch: int, checked: int, decimal: int, default: int, delegate: int, do: int, double: int, enum: int, event: int, explicit: int, extern: int, finally: int, fixed: int, float: int): int {
    base + byte + catch + checked + decimal + default + delegate + do + double + enum + event + explicit + extern + finally + fixed + float
  }
  method Outs0() returns (base: int, byte: int, catch: int, checked: int, decimal: int, default: int, delegate: int, do: int, double: int, enum: int, event: int, explicit: int, extern: int, finally: int, fixed: int, float: int) {
    base := 1;
    byte := 2;
    catch := 3;
    checked := 4;
    decimal := 5;
    default := 6;
    delegate := 7;
    do := 8;
    double := 9;
    enum := 10;
    event := 11;
    explicit := 12;
    extern := 13;
    finally := 14;
    fixed := 15;
    float := 16;
  }
  function First0<base, byte, catch, checked, decimal, default, delegate, do, double, enum, event, explicit, extern, finally, fixed, float>(x0: base, x1: byte, x2: catch, x3: checked, x4: decimal, x5: default, x6: delegate, x7: do, x8: double, x9: enum, x10: event, x11: explicit, x12: extern, x13: finally, x14: fixed, x15: float): base {
    x0
  }
  datatype Destructors1 = Destructors1(foreach: int, goto: int, implicit: int, interface: int, internal: int, lock: int, long: int, namespace: int, operator: int, out: int, override: int, params: int, private: int, protected: int, public: int, readonly: int)
  function AddAll1(foreach: int, goto: int, implicit: int, interface: int, internal: int, lock: int, long: int, namespace: int, operator: int, out: int, override: int, params: int, private: int, protected: int, public: int, readonly: int): int {
    foreach + goto + implicit + interface + internal + lock + long + namespace + operator + out + override + params + private + protected + public + readonly
  }
  method Outs1() returns (foreach: int, goto: int, implicit: int, interface: int, internal: int, lock: int, long: int, namespace: int, operator: int, out: int, override: int, params: int, private: int, protected: int, public: int, readonly: int) {
    foreach := 17;
    goto := 18;
    implicit := 19;
    interface := 20;
    internal := 21;
    lock := 22;
    long := 23;
    namespace := 24;
    operator := 25;
    out := 26;
    override := 27;
    params := 28;
    private := 29;
    protected := 30;
    public := 31;
    readonly := 32;
  }
  function First1<foreach, goto, implicit, interface, internal, lock, long, namespace, operator, out, override, params, private, protected, public, readonly>(x0: foreach, x1: goto, x2: implicit, x3: interface, x4: internal, x5: lock, x6: long, x7: namespace, x8: operator, x9: out, x10: override, x11: params, x12: private, x13: protected, x14: public, x15: readonly): foreach {
    x0
  }
  datatype Destructors2 = Destructors2(ref: int, sbyte: int, sealed: int, short: int, sizeof: int, stackalloc: int, struct: int, switch: int, throw: int, try: int, typeof: int, uint: int, ulong: int, unchecked: int, unsafe: int, ushort: int)
  function AddAll2(ref: int, sbyte: int, sealed: int, short: int, sizeof: int, stackalloc: int, struct: int, switch: int, throw: int, try: int, typeof: int, uint: int, ulong: int, unchecked: int, unsafe: int, ushort: int): int {
    ref + sbyte + sealed + short + sizeof + stackalloc + struct + switch + throw + try + typeof + uint + ulong + unchecked + unsafe + ushort
  }
  method Outs2() returns (ref: int, sbyte: int, sealed: int, short: int, sizeof: int, stackalloc: int, struct: int, switch: int, throw: int, try: int, typeof: int, uint: int, ulong: int, unchecked: int, unsafe: int, ushort: int) {
    ref := 33;
    sbyte := 34;
    sealed := 35;
    short := 36;
    sizeof := 37;
    stackalloc := 38;
    struct := 39;
    switch := 40;
    throw := 41;
    try := 42;
    typeof := 43;
    uint := 44;
    ulong := 45;
    unchecked := 46;
    unsafe := 47;
    ushort := 48;
  }
  function First2<ref, sbyte, sealed, short, sizeof, stackalloc, struct, switch, throw, try, typeof, uint, ulong, unchecked, unsafe, ushort>(x0: ref, x1: sbyte, x2: sealed, x3: short, x4: sizeof, x5: stackalloc, x6: struct, x7: switch, x8: throw, x9: try, x10: typeof, x11: uint, x12: ulong, x13: unchecked, x14: unsafe, x15: ushort): ref {
    x0
  }
  datatype Destructors3 = Destructors3(using: int, virtual: int, void: int, volatile: int, add: int, allows: int, alias: int, and: int, args: int, ascending: int, async: int, await: int, descending: int, dynamic: int, equals: int, extension: int)
  function AddAll3(using: int, virtual: int, void: int, volatile: int, add: int, allows: int, alias: int, and: int, args: int, ascending: int, async: int, await: int, descending: int, dynamic: int, equals: int, extension: int): int {
    using + virtual + void + volatile + add + allows + alias + and + args + ascending + async + await + descending + dynamic + equals + extension
  }
  method Outs3() returns (using: int, virtual: int, void: int, volatile: int, add: int, allows: int, alias: int, and: int, args: int, ascending: int, async: int, await: int, descending: int, dynamic: int, equals: int, extension: int) {
    using := 49;
    virtual := 50;
    void := 51;
    volatile := 52;
    add := 53;
    allows := 54;
    alias := 55;
    and := 56;
    args := 57;
    ascending := 58;
    async := 59;
    await := 60;
    descending := 61;
    dynamic := 62;
    equals := 63;
    extension := 64;
  }
  function First3<using, virtual, void, volatile, add, allows, alias, and, args, ascending, async, await, descending, dynamic, equals, extension>(x0: using, x1: virtual, x2: void, x3: volatile, x4: add, x5: allows, x6: alias, x7: and, x8: args, x9: ascending, x10: async, x11: await, x12: descending, x13: dynamic, x14: equals, x15: extension): using {
    x0
  }
  datatype Destructors4 = Destructors4(file: int, from: int, get: int, global: int, group: int, init: int, into: int, join: int, let: int, managed: int, nameof: int, nint: int, not: int, notnull: int, nuint: int, on: int)
  function AddAll4(file: int, from: int, get: int, global: int, group: int, init: int, into: int, join: int, let: int, managed: int, nameof: int, nint: int, not: int, notnull: int, nuint: int, on: int): int {
    file + from + get + global + group + init + into + join + let + managed + nameof + nint + not + notnull + nuint + on
  }
  method Outs4() returns (file: int, from: int, get: int, global: int, group: int, init: int, into: int, join: int, let: int, managed: int, nameof: int, nint: int, not: int, notnull: int, nuint: int, on: int) {
    file := 65;
    from := 66;
    get := 67;
    global := 68;
    group := 69;
    init := 70;
    into := 71;
    join := 72;
    let := 73;
    managed := 74;
    nameof := 75;
    nint := 76;
    not := 77;
    notnull := 78;
    nuint := 79;
    on := 80;
  }
  function First4<file, from, get, global, group, init, into, join, let, managed, nameof, nint, not, notnull, nuint, on>(x0: file, x1: from, x2: get, x3: global, x4: group, x5: init, x6: into, x7: join, x8: let, x9: managed, x10: nameof, x11: nint, x12: not, x13: notnull, x14: nuint, x15: on): file {
    x0
  }
  datatype Destructors5 = Destructors5(or: int, orderby: int, partial: int, record: int, remove: int, required: int, scoped: int, select: int, unmanaged: int, value: int, when: int, where: int, with: int)
  function AddAll5(or: int, orderby: int, partial: int, record: int, remove: int, required: int, scoped: int, select: int, unmanaged: int, value: int, when: int, where: int, with: int): int {
    or + orderby + partial + record + remove + required + scoped + select + unmanaged + value + when + where + with
  }
  method Outs5() returns (or: int, orderby: int, partial: int, record: int, remove: int, required: int, scoped: int, select: int, unmanaged: int, value: int, when: int, where: int, with: int) {
    or := 81;
    orderby := 82;
    partial := 83;
    record := 84;
    remove := 85;
    required := 86;
    scoped := 87;
    select := 88;
    unmanaged := 89;
    value := 90;
    when := 91;
    where := 92;
    with := 93;
  }
  function First5<or, orderby, partial, record, remove, required, scoped, select, unmanaged, value, when, where, with>(x0: or, x1: orderby, x2: partial, x3: record, x4: remove, x5: required, x6: scoped, x7: select, x8: unmanaged, x9: value, x10: when, x11: where, x12: with): or {
    x0
  }
}

method Main() {
  var dt := DatatypeNames.base.Mk(1).n + DatatypeNames.byte.Mk(2).n + DatatypeNames.catch.Mk(3).n + DatatypeNames.checked.Mk(4).n + DatatypeNames.decimal.Mk(5).n + DatatypeNames.default.Mk(6).n + DatatypeNames.delegate.Mk(7).n + DatatypeNames.do.Mk(8).n + DatatypeNames.double.Mk(9).n + DatatypeNames.enum.Mk(10).n + DatatypeNames.event.Mk(11).n + DatatypeNames.explicit.Mk(12).n + DatatypeNames.extern.Mk(13).n + DatatypeNames.finally.Mk(14).n + DatatypeNames.fixed.Mk(15).n + DatatypeNames.float.Mk(16).n + DatatypeNames.foreach.Mk(17).n + DatatypeNames.goto.Mk(18).n + DatatypeNames.implicit.Mk(19).n + DatatypeNames.interface.Mk(20).n + DatatypeNames.internal.Mk(21).n + DatatypeNames.lock.Mk(22).n + DatatypeNames.long.Mk(23).n + DatatypeNames.namespace.Mk(24).n + DatatypeNames.operator.Mk(25).n + DatatypeNames.out.Mk(26).n + DatatypeNames.override.Mk(27).n + DatatypeNames.params.Mk(28).n + DatatypeNames.private.Mk(29).n + DatatypeNames.protected.Mk(30).n + DatatypeNames.public.Mk(31).n + DatatypeNames.readonly.Mk(32).n + DatatypeNames.ref.Mk(33).n + DatatypeNames.sbyte.Mk(34).n + DatatypeNames.sealed.Mk(35).n + DatatypeNames.short.Mk(36).n + DatatypeNames.sizeof.Mk(37).n + DatatypeNames.stackalloc.Mk(38).n + DatatypeNames.struct.Mk(39).n + DatatypeNames.switch.Mk(40).n + DatatypeNames.throw.Mk(41).n + DatatypeNames.try.Mk(42).n + DatatypeNames.typeof.Mk(43).n + DatatypeNames.uint.Mk(44).n + DatatypeNames.ulong.Mk(45).n + DatatypeNames.unchecked.Mk(46).n + DatatypeNames.unsafe.Mk(47).n + DatatypeNames.ushort.Mk(48).n + DatatypeNames.using.Mk(49).n + DatatypeNames.virtual.Mk(50).n + DatatypeNames.void.Mk(51).n + DatatypeNames.volatile.Mk(52).n + DatatypeNames.add.Mk(53).n + DatatypeNames.allows.Mk(54).n + DatatypeNames.alias.Mk(55).n + DatatypeNames.and.Mk(56).n + DatatypeNames.args.Mk(57).n + DatatypeNames.ascending.Mk(58).n + DatatypeNames.async.Mk(59).n + DatatypeNames.await.Mk(60).n + DatatypeNames.descending.Mk(61).n + DatatypeNames.dynamic.Mk(62).n + DatatypeNames.equals.Mk(63).n + DatatypeNames.extension.Mk(64).n + DatatypeNames.file.Mk(65).n + DatatypeNames.from.Mk(66).n + DatatypeNames.get.Mk(67).n + DatatypeNames.global.Mk(68).n + DatatypeNames.group.Mk(69).n + DatatypeNames.init.Mk(70).n + DatatypeNames.into.Mk(71).n + DatatypeNames.join.Mk(72).n + DatatypeNames.let.Mk(73).n + DatatypeNames.managed.Mk(74).n + DatatypeNames.nameof.Mk(75).n + DatatypeNames.nint.Mk(76).n + DatatypeNames.not.Mk(77).n + DatatypeNames.notnull.Mk(78).n + DatatypeNames.nuint.Mk(79).n + DatatypeNames.on.Mk(80).n + DatatypeNames.or.Mk(81).n + DatatypeNames.orderby.Mk(82).n + DatatypeNames.partial.Mk(83).n + DatatypeNames.record.Mk(84).n + DatatypeNames.remove.Mk(85).n + DatatypeNames.required.Mk(86).n + DatatypeNames.scoped.Mk(87).n + DatatypeNames.select.Mk(88).n + DatatypeNames.unmanaged.Mk(89).n + DatatypeNames.value.Mk(90).n + DatatypeNames.when.Mk(91).n + DatatypeNames.where.Mk(92).n + DatatypeNames.with.Mk(93).n;
  print "datatype names: ", dt, "\n";
  var cl := 0;
  var c1 := new ClassNames.base(1); cl := cl + c1.n;
  var c2 := new ClassNames.byte(2); cl := cl + c2.n;
  var c3 := new ClassNames.catch(3); cl := cl + c3.n;
  var c4 := new ClassNames.checked(4); cl := cl + c4.n;
  var c5 := new ClassNames.decimal(5); cl := cl + c5.n;
  var c6 := new ClassNames.default(6); cl := cl + c6.n;
  var c7 := new ClassNames.delegate(7); cl := cl + c7.n;
  var c8 := new ClassNames.do(8); cl := cl + c8.n;
  var c9 := new ClassNames.double(9); cl := cl + c9.n;
  var c10 := new ClassNames.enum(10); cl := cl + c10.n;
  var c11 := new ClassNames.event(11); cl := cl + c11.n;
  var c12 := new ClassNames.explicit(12); cl := cl + c12.n;
  var c13 := new ClassNames.extern(13); cl := cl + c13.n;
  var c14 := new ClassNames.finally(14); cl := cl + c14.n;
  var c15 := new ClassNames.fixed(15); cl := cl + c15.n;
  var c16 := new ClassNames.float(16); cl := cl + c16.n;
  var c17 := new ClassNames.foreach(17); cl := cl + c17.n;
  var c18 := new ClassNames.goto(18); cl := cl + c18.n;
  var c19 := new ClassNames.implicit(19); cl := cl + c19.n;
  var c20 := new ClassNames.interface(20); cl := cl + c20.n;
  var c21 := new ClassNames.internal(21); cl := cl + c21.n;
  var c22 := new ClassNames.lock(22); cl := cl + c22.n;
  var c23 := new ClassNames.long(23); cl := cl + c23.n;
  var c24 := new ClassNames.namespace(24); cl := cl + c24.n;
  var c25 := new ClassNames.operator(25); cl := cl + c25.n;
  var c26 := new ClassNames.out(26); cl := cl + c26.n;
  var c27 := new ClassNames.override(27); cl := cl + c27.n;
  var c28 := new ClassNames.params(28); cl := cl + c28.n;
  var c29 := new ClassNames.private(29); cl := cl + c29.n;
  var c30 := new ClassNames.protected(30); cl := cl + c30.n;
  var c31 := new ClassNames.public(31); cl := cl + c31.n;
  var c32 := new ClassNames.readonly(32); cl := cl + c32.n;
  var c33 := new ClassNames.ref(33); cl := cl + c33.n;
  var c34 := new ClassNames.sbyte(34); cl := cl + c34.n;
  var c35 := new ClassNames.sealed(35); cl := cl + c35.n;
  var c36 := new ClassNames.short(36); cl := cl + c36.n;
  var c37 := new ClassNames.sizeof(37); cl := cl + c37.n;
  var c38 := new ClassNames.stackalloc(38); cl := cl + c38.n;
  var c39 := new ClassNames.struct(39); cl := cl + c39.n;
  var c40 := new ClassNames.switch(40); cl := cl + c40.n;
  var c41 := new ClassNames.throw(41); cl := cl + c41.n;
  var c42 := new ClassNames.try(42); cl := cl + c42.n;
  var c43 := new ClassNames.typeof(43); cl := cl + c43.n;
  var c44 := new ClassNames.uint(44); cl := cl + c44.n;
  var c45 := new ClassNames.ulong(45); cl := cl + c45.n;
  var c46 := new ClassNames.unchecked(46); cl := cl + c46.n;
  var c47 := new ClassNames.unsafe(47); cl := cl + c47.n;
  var c48 := new ClassNames.ushort(48); cl := cl + c48.n;
  var c49 := new ClassNames.using(49); cl := cl + c49.n;
  var c50 := new ClassNames.virtual(50); cl := cl + c50.n;
  var c51 := new ClassNames.void(51); cl := cl + c51.n;
  var c52 := new ClassNames.volatile(52); cl := cl + c52.n;
  var c53 := new ClassNames.add(53); cl := cl + c53.n;
  var c54 := new ClassNames.allows(54); cl := cl + c54.n;
  var c55 := new ClassNames.alias(55); cl := cl + c55.n;
  var c56 := new ClassNames.and(56); cl := cl + c56.n;
  var c57 := new ClassNames.args(57); cl := cl + c57.n;
  var c58 := new ClassNames.ascending(58); cl := cl + c58.n;
  var c59 := new ClassNames.async(59); cl := cl + c59.n;
  var c60 := new ClassNames.await(60); cl := cl + c60.n;
  var c61 := new ClassNames.descending(61); cl := cl + c61.n;
  var c62 := new ClassNames.dynamic(62); cl := cl + c62.n;
  var c63 := new ClassNames.equals(63); cl := cl + c63.n;
  var c64 := new ClassNames.extension(64); cl := cl + c64.n;
  var c65 := new ClassNames.file(65); cl := cl + c65.n;
  var c66 := new ClassNames.from(66); cl := cl + c66.n;
  var c67 := new ClassNames.get(67); cl := cl + c67.n;
  var c68 := new ClassNames.global(68); cl := cl + c68.n;
  var c69 := new ClassNames.group(69); cl := cl + c69.n;
  var c70 := new ClassNames.init(70); cl := cl + c70.n;
  var c71 := new ClassNames.into(71); cl := cl + c71.n;
  var c72 := new ClassNames.join(72); cl := cl + c72.n;
  var c73 := new ClassNames.let(73); cl := cl + c73.n;
  var c74 := new ClassNames.managed(74); cl := cl + c74.n;
  var c75 := new ClassNames.nameof(75); cl := cl + c75.n;
  var c76 := new ClassNames.nint(76); cl := cl + c76.n;
  var c77 := new ClassNames.not(77); cl := cl + c77.n;
  var c78 := new ClassNames.notnull(78); cl := cl + c78.n;
  var c79 := new ClassNames.nuint(79); cl := cl + c79.n;
  var c80 := new ClassNames.on(80); cl := cl + c80.n;
  var c81 := new ClassNames.or(81); cl := cl + c81.n;
  var c82 := new ClassNames.orderby(82); cl := cl + c82.n;
  var c83 := new ClassNames.partial(83); cl := cl + c83.n;
  var c84 := new ClassNames.record(84); cl := cl + c84.n;
  var c85 := new ClassNames.remove(85); cl := cl + c85.n;
  var c86 := new ClassNames.required(86); cl := cl + c86.n;
  var c87 := new ClassNames.scoped(87); cl := cl + c87.n;
  var c88 := new ClassNames.select(88); cl := cl + c88.n;
  var c89 := new ClassNames.unmanaged(89); cl := cl + c89.n;
  var c90 := new ClassNames.value(90); cl := cl + c90.n;
  var c91 := new ClassNames.when(91); cl := cl + c91.n;
  var c92 := new ClassNames.where(92); cl := cl + c92.n;
  var c93 := new ClassNames.with(93); cl := cl + c93.n;
  print "class names: ", cl, "\n";
  var md := base.n + byte.n + catch.n + checked.n + decimal.n + default.n + delegate.n + do.n + double.n + enum.n + event.n + explicit.n + extern.n + finally.n + fixed.n + float.n + foreach.n + goto.n + implicit.n + interface.n + internal.n + lock.n + long.n + namespace.n + operator.n + out.n + override.n + params.n + private.n + protected.n + public.n + readonly.n + ref.n + sbyte.n + sealed.n + short.n + sizeof.n + stackalloc.n + struct.n + switch.n + throw.n + try.n + typeof.n + uint.n + ulong.n + unchecked.n + unsafe.n + ushort.n + using.n + virtual.n + void.n + volatile.n + add.n + allows.n + alias.n + and.n + args.n + ascending.n + async.n + await.n + descending.n + dynamic.n + equals.n + extension.n + file.n + from.n + get.n + global.n + group.n + init.n + into.n + join.n + let.n + managed.n + nameof.n + nint.n + not.n + notnull.n + nuint.n + on.n + or.n + orderby.n + partial.n + record.n + remove.n + required.n + scoped.n + select.n + unmanaged.n + value.n + when.n + where.n + with.n;
  print "module names: ", md, "\n";
  var cs := Members.base + Members.byte + Members.catch + Members.checked + Members.decimal + Members.default + Members.delegate + Members.do + Members.double + Members.enum + Members.event + Members.explicit + Members.extern + Members.finally + Members.fixed + Members.float + Members.foreach + Members.goto + Members.implicit + Members.interface + Members.internal + Members.lock + Members.long + Members.namespace + Members.operator + Members.out + Members.override + Members.params + Members.private + Members.protected + Members.public + Members.readonly + Members.ref + Members.sbyte + Members.sealed + Members.short + Members.sizeof + Members.stackalloc + Members.struct + Members.switch + Members.throw + Members.try + Members.typeof + Members.uint + Members.ulong + Members.unchecked + Members.unsafe + Members.ushort + Members.using + Members.virtual + Members.void + Members.volatile + Members.add + Members.allows + Members.alias + Members.and + Members.args + Members.ascending + Members.async + Members.await + Members.descending + Members.dynamic + Members.equals + Members.extension + Members.file + Members.from + Members.get + Members.global + Members.group + Members.init + Members.into + Members.join + Members.let + Members.managed + Members.nameof + Members.nint + Members.not + Members.notnull + Members.nuint + Members.on + Members.or + Members.orderby + Members.partial + Members.record + Members.remove + Members.required + Members.scoped + Members.select + Members.unmanaged + Members.value + Members.when + Members.where + Members.with;
  print "constants: ", cs, "\n";
  var fs := Members.Functions.base() + Members.Functions.byte() + Members.Functions.catch() + Members.Functions.checked() + Members.Functions.decimal() + Members.Functions.default() + Members.Functions.delegate() + Members.Functions.do() + Members.Functions.double() + Members.Functions.enum() + Members.Functions.event() + Members.Functions.explicit() + Members.Functions.extern() + Members.Functions.finally() + Members.Functions.fixed() + Members.Functions.float() + Members.Functions.foreach() + Members.Functions.goto() + Members.Functions.implicit() + Members.Functions.interface() + Members.Functions.internal() + Members.Functions.lock() + Members.Functions.long() + Members.Functions.namespace() + Members.Functions.operator() + Members.Functions.out() + Members.Functions.override() + Members.Functions.params() + Members.Functions.private() + Members.Functions.protected() + Members.Functions.public() + Members.Functions.readonly() + Members.Functions.ref() + Members.Functions.sbyte() + Members.Functions.sealed() + Members.Functions.short() + Members.Functions.sizeof() + Members.Functions.stackalloc() + Members.Functions.struct() + Members.Functions.switch() + Members.Functions.throw() + Members.Functions.try() + Members.Functions.typeof() + Members.Functions.uint() + Members.Functions.ulong() + Members.Functions.unchecked() + Members.Functions.unsafe() + Members.Functions.ushort() + Members.Functions.using() + Members.Functions.virtual() + Members.Functions.void() + Members.Functions.volatile() + Members.Functions.add() + Members.Functions.allows() + Members.Functions.alias() + Members.Functions.and() + Members.Functions.args() + Members.Functions.ascending() + Members.Functions.async() + Members.Functions.await() + Members.Functions.descending() + Members.Functions.dynamic() + Members.Functions.equals() + Members.Functions.extension() + Members.Functions.file() + Members.Functions.from() + Members.Functions.get() + Members.Functions.global() + Members.Functions.group() + Members.Functions.init() + Members.Functions.into() + Members.Functions.join() + Members.Functions.let() + Members.Functions.managed() + Members.Functions.nameof() + Members.Functions.nint() + Members.Functions.not() + Members.Functions.notnull() + Members.Functions.nuint() + Members.Functions.on() + Members.Functions.or() + Members.Functions.orderby() + Members.Functions.partial() + Members.Functions.record() + Members.Functions.remove() + Members.Functions.required() + Members.Functions.scoped() + Members.Functions.select() + Members.Functions.unmanaged() + Members.Functions.value() + Members.Functions.when() + Members.Functions.where() + Members.Functions.with();
  print "functions: ", fs, "\n";
  var f := new Members.Fields();
  var fsum := f.Sum();
  print "fields: ", fsum, "\n";
  var ds := 0;
  var d0 := Members.Destructors0(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16);
  ds := ds + d0.base + d0.byte + d0.catch + d0.checked + d0.decimal + d0.default + d0.delegate + d0.do + d0.double + d0.enum + d0.event + d0.explicit + d0.extern + d0.finally + d0.fixed + d0.float;
  var d1 := Members.Destructors1(17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32);
  ds := ds + d1.foreach + d1.goto + d1.implicit + d1.interface + d1.internal + d1.lock + d1.long + d1.namespace + d1.operator + d1.out + d1.override + d1.params + d1.private + d1.protected + d1.public + d1.readonly;
  var d2 := Members.Destructors2(33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48);
  ds := ds + d2.ref + d2.sbyte + d2.sealed + d2.short + d2.sizeof + d2.stackalloc + d2.struct + d2.switch + d2.throw + d2.try + d2.typeof + d2.uint + d2.ulong + d2.unchecked + d2.unsafe + d2.ushort;
  var d3 := Members.Destructors3(49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64);
  ds := ds + d3.using + d3.virtual + d3.void + d3.volatile + d3.add + d3.allows + d3.alias + d3.and + d3.args + d3.ascending + d3.async + d3.await + d3.descending + d3.dynamic + d3.equals + d3.extension;
  var d4 := Members.Destructors4(65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80);
  ds := ds + d4.file + d4.from + d4.get + d4.global + d4.group + d4.init + d4.into + d4.join + d4.let + d4.managed + d4.nameof + d4.nint + d4.not + d4.notnull + d4.nuint + d4.on;
  var d5 := Members.Destructors5(81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93);
  ds := ds + d5.or + d5.orderby + d5.partial + d5.record + d5.remove + d5.required + d5.scoped + d5.select + d5.unmanaged + d5.value + d5.when + d5.where + d5.with;
  print "destructors: ", ds, "\n";
  var ctors := [CtorNames.Constructors.base, CtorNames.Constructors.byte, CtorNames.Constructors.catch, CtorNames.Constructors.checked, CtorNames.Constructors.decimal, CtorNames.Constructors.default, CtorNames.Constructors.delegate, CtorNames.Constructors.do, CtorNames.Constructors.double, CtorNames.Constructors.enum, CtorNames.Constructors.event, CtorNames.Constructors.explicit, CtorNames.Constructors.extern, CtorNames.Constructors.finally, CtorNames.Constructors.fixed, CtorNames.Constructors.float, CtorNames.Constructors.foreach, CtorNames.Constructors.goto, CtorNames.Constructors.implicit, CtorNames.Constructors.interface, CtorNames.Constructors.internal, CtorNames.Constructors.lock, CtorNames.Constructors.long, CtorNames.Constructors.namespace, CtorNames.Constructors.operator, CtorNames.Constructors.out, CtorNames.Constructors.override, CtorNames.Constructors.params, CtorNames.Constructors.private, CtorNames.Constructors.protected, CtorNames.Constructors.public, CtorNames.Constructors.readonly, CtorNames.Constructors.ref, CtorNames.Constructors.sbyte, CtorNames.Constructors.sealed, CtorNames.Constructors.short, CtorNames.Constructors.sizeof, CtorNames.Constructors.stackalloc, CtorNames.Constructors.struct, CtorNames.Constructors.switch, CtorNames.Constructors.throw, CtorNames.Constructors.try, CtorNames.Constructors.typeof, CtorNames.Constructors.uint, CtorNames.Constructors.ulong, CtorNames.Constructors.unchecked, CtorNames.Constructors.unsafe, CtorNames.Constructors.ushort, CtorNames.Constructors.using, CtorNames.Constructors.virtual, CtorNames.Constructors.void, CtorNames.Constructors.volatile, CtorNames.Constructors.add, CtorNames.Constructors.allows, CtorNames.Constructors.alias, CtorNames.Constructors.and, CtorNames.Constructors.args, CtorNames.Constructors.ascending, CtorNames.Constructors.async, CtorNames.Constructors.await, CtorNames.Constructors.descending, CtorNames.Constructors.dynamic, CtorNames.Constructors.equals, CtorNames.Constructors.extension, CtorNames.Constructors.file, CtorNames.Constructors.from, CtorNames.Constructors.get, CtorNames.Constructors.global, CtorNames.Constructors.group, CtorNames.Constructors.init, CtorNames.Constructors.into, CtorNames.Constructors.join, CtorNames.Constructors.let, CtorNames.Constructors.managed, CtorNames.Constructors.nameof, CtorNames.Constructors.nint, CtorNames.Constructors.not, CtorNames.Constructors.notnull, CtorNames.Constructors.nuint, CtorNames.Constructors.on, CtorNames.Constructors.or, CtorNames.Constructors.orderby, CtorNames.Constructors.partial, CtorNames.Constructors.record, CtorNames.Constructors.remove, CtorNames.Constructors.required, CtorNames.Constructors.scoped, CtorNames.Constructors.select, CtorNames.Constructors.unmanaged, CtorNames.Constructors.value, CtorNames.Constructors.when, CtorNames.Constructors.where, CtorNames.Constructors.with];
  var cc := 0;
  for i := 0 to |ctors| {
    match ctors[i]
    case base => cc := cc + 1;
    case byte => cc := cc + 2;
    case catch => cc := cc + 3;
    case checked => cc := cc + 4;
    case decimal => cc := cc + 5;
    case default => cc := cc + 6;
    case delegate => cc := cc + 7;
    case do => cc := cc + 8;
    case double => cc := cc + 9;
    case enum => cc := cc + 10;
    case event => cc := cc + 11;
    case explicit => cc := cc + 12;
    case extern => cc := cc + 13;
    case finally => cc := cc + 14;
    case fixed => cc := cc + 15;
    case float => cc := cc + 16;
    case foreach => cc := cc + 17;
    case goto => cc := cc + 18;
    case implicit => cc := cc + 19;
    case interface => cc := cc + 20;
    case internal => cc := cc + 21;
    case lock => cc := cc + 22;
    case long => cc := cc + 23;
    case namespace => cc := cc + 24;
    case operator => cc := cc + 25;
    case out => cc := cc + 26;
    case override => cc := cc + 27;
    case params => cc := cc + 28;
    case private => cc := cc + 29;
    case protected => cc := cc + 30;
    case public => cc := cc + 31;
    case readonly => cc := cc + 32;
    case ref => cc := cc + 33;
    case sbyte => cc := cc + 34;
    case sealed => cc := cc + 35;
    case short => cc := cc + 36;
    case sizeof => cc := cc + 37;
    case stackalloc => cc := cc + 38;
    case struct => cc := cc + 39;
    case switch => cc := cc + 40;
    case throw => cc := cc + 41;
    case try => cc := cc + 42;
    case typeof => cc := cc + 43;
    case uint => cc := cc + 44;
    case ulong => cc := cc + 45;
    case unchecked => cc := cc + 46;
    case unsafe => cc := cc + 47;
    case ushort => cc := cc + 48;
    case using => cc := cc + 49;
    case virtual => cc := cc + 50;
    case void => cc := cc + 51;
    case volatile => cc := cc + 52;
    case add => cc := cc + 53;
    case allows => cc := cc + 54;
    case alias => cc := cc + 55;
    case and => cc := cc + 56;
    case args => cc := cc + 57;
    case ascending => cc := cc + 58;
    case async => cc := cc + 59;
    case await => cc := cc + 60;
    case descending => cc := cc + 61;
    case dynamic => cc := cc + 62;
    case equals => cc := cc + 63;
    case extension => cc := cc + 64;
    case file => cc := cc + 65;
    case from => cc := cc + 66;
    case get => cc := cc + 67;
    case global => cc := cc + 68;
    case group => cc := cc + 69;
    case init => cc := cc + 70;
    case into => cc := cc + 71;
    case join => cc := cc + 72;
    case let => cc := cc + 73;
    case managed => cc := cc + 74;
    case nameof => cc := cc + 75;
    case nint => cc := cc + 76;
    case not => cc := cc + 77;
    case notnull => cc := cc + 78;
    case nuint => cc := cc + 79;
    case on => cc := cc + 80;
    case or => cc := cc + 81;
    case orderby => cc := cc + 82;
    case partial => cc := cc + 83;
    case record => cc := cc + 84;
    case remove => cc := cc + 85;
    case required => cc := cc + 86;
    case scoped => cc := cc + 87;
    case select => cc := cc + 88;
    case unmanaged => cc := cc + 89;
    case value => cc := cc + 90;
    case when => cc := cc + 91;
    case where => cc := cc + 92;
    case with => cc := cc + 93;
  }
  print "constructors: ", cc, "\n";
  var a := Members.AddAll0(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16) + Members.AddAll1(17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32) + Members.AddAll2(33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48) + Members.AddAll3(49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64) + Members.AddAll4(65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80) + Members.AddAll5(81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93);
  print "in-parameters: ", a, "\n";
  var os := 0;
  var o1, o2, o3, o4, o5, o6, o7, o8, o9, o10, o11, o12, o13, o14, o15, o16 := Members.Outs0();
  os := os + o1 + o2 + o3 + o4 + o5 + o6 + o7 + o8 + o9 + o10 + o11 + o12 + o13 + o14 + o15 + o16;
  var o17, o18, o19, o20, o21, o22, o23, o24, o25, o26, o27, o28, o29, o30, o31, o32 := Members.Outs1();
  os := os + o17 + o18 + o19 + o20 + o21 + o22 + o23 + o24 + o25 + o26 + o27 + o28 + o29 + o30 + o31 + o32;
  var o33, o34, o35, o36, o37, o38, o39, o40, o41, o42, o43, o44, o45, o46, o47, o48 := Members.Outs2();
  os := os + o33 + o34 + o35 + o36 + o37 + o38 + o39 + o40 + o41 + o42 + o43 + o44 + o45 + o46 + o47 + o48;
  var o49, o50, o51, o52, o53, o54, o55, o56, o57, o58, o59, o60, o61, o62, o63, o64 := Members.Outs3();
  os := os + o49 + o50 + o51 + o52 + o53 + o54 + o55 + o56 + o57 + o58 + o59 + o60 + o61 + o62 + o63 + o64;
  var o65, o66, o67, o68, o69, o70, o71, o72, o73, o74, o75, o76, o77, o78, o79, o80 := Members.Outs4();
  os := os + o65 + o66 + o67 + o68 + o69 + o70 + o71 + o72 + o73 + o74 + o75 + o76 + o77 + o78 + o79 + o80;
  var o81, o82, o83, o84, o85, o86, o87, o88, o89, o90, o91, o92, o93 := Members.Outs5();
  os := os + o81 + o82 + o83 + o84 + o85 + o86 + o87 + o88 + o89 + o90 + o91 + o92 + o93;
  print "out-parameters: ", os, "\n";
  var t := Members.First0(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16) + Members.First1(17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32) + Members.First2(33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48) + Members.First3(49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64) + Members.First4(65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80) + Members.First5(81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93);
  print "type parameters: ", t, "\n";
  var base := 1;
  var byte := 2;
  var catch := 3;
  var checked := 4;
  var decimal := 5;
  var default := 6;
  var delegate := 7;
  var do := 8;
  var double := 9;
  var enum := 10;
  var event := 11;
  var explicit := 12;
  var extern := 13;
  var finally := 14;
  var fixed := 15;
  var float := 16;
  var foreach := 17;
  var goto := 18;
  var implicit := 19;
  var interface := 20;
  var internal := 21;
  var lock := 22;
  var long := 23;
  var namespace := 24;
  var operator := 25;
  var out := 26;
  var override := 27;
  var params := 28;
  var private := 29;
  var protected := 30;
  var public := 31;
  var readonly := 32;
  var ref := 33;
  var sbyte := 34;
  var sealed := 35;
  var short := 36;
  var sizeof := 37;
  var stackalloc := 38;
  var struct := 39;
  var switch := 40;
  var throw := 41;
  var try := 42;
  var typeof := 43;
  var uint := 44;
  var ulong := 45;
  var unchecked := 46;
  var unsafe := 47;
  var ushort := 48;
  var using := 49;
  var virtual := 50;
  var void := 51;
  var volatile := 52;
  var add := 53;
  var allows := 54;
  var alias := 55;
  var and := 56;
  var args := 57;
  var ascending := 58;
  var async := 59;
  var await := 60;
  var descending := 61;
  var dynamic := 62;
  var equals := 63;
  var extension := 64;
  var file := 65;
  var from := 66;
  var get := 67;
  var global := 68;
  var group := 69;
  var init := 70;
  var into := 71;
  var join := 72;
  var let := 73;
  var managed := 74;
  var nameof := 75;
  var nint := 76;
  var not := 77;
  var notnull := 78;
  var nuint := 79;
  var on := 80;
  var or := 81;
  var orderby := 82;
  var partial := 83;
  var record := 84;
  var remove := 85;
  var required := 86;
  var scoped := 87;
  var select := 88;
  var unmanaged := 89;
  var value := 90;
  var when := 91;
  var where := 92;
  var with := 93;
  print "locals: ", base + byte + catch + checked + decimal + default + delegate + do + double + enum + event + explicit + extern + finally + fixed + float + foreach + goto + implicit + interface + internal + lock + long + namespace + operator + out + override + params + private + protected + public + readonly + ref + sbyte + sealed + short + sizeof + stackalloc + struct + switch + throw + try + typeof + uint + ulong + unchecked + unsafe + ushort + using + virtual + void + volatile + add + allows + alias + and + args + ascending + async + await + descending + dynamic + equals + extension + file + from + get + global + group + init + into + join + let + managed + nameof + nint + not + notnull + nuint + on + or + orderby + partial + record + remove + required + scoped + select + unmanaged + value + when + where + with, "\n";
}
