#!/usr/bin/env python3
"""Executable specification of module-local translation state, not a Dafny test.

No third-party packages. Run: python3 model_naming.py
The oracle always translates one module from fresh state. The tested runner
shares input AST objects across module permutations and optionally reuses the
legacy mutable names/counters to show the historical failure.
"""
from dataclasses import dataclass, field
from itertools import permutations
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import unittest


class Generator:
    def __init__(self, prefix=''):
        self.prefix = prefix
        self.counts = [{}]
        self.tip = []
        self.children = 0

    def fresh(self, prefix):
        count = self.counts[-1].get(prefix, 0)
        self.counts[-1][prefix] = count + 1
        suffix = '_'.join(map(str, [*self.tip, count]))
        return self.prefix + prefix + suffix

    def nested(self, prefix):
        return Generator(self.fresh(prefix))

    def push(self):
        self.tip.append(self.children)
        self.children = 0
        self.counts.append({})

    def pop(self):
        self.children = self.tip.pop() + 1
        self.counts.pop()


@dataclass(eq=False)
class Decl:
    name: str
    generator: Generator = field(default_factory=Generator)


@dataclass(eq=False)
class Var:
    name: str
    unique: str | None = None


@dataclass(eq=False)
class Module:
    identity: str
    visible: tuple
    count: int = 2


class State:
    def __init__(self, history_free):
        self.history_free = history_free
        self.global_dep = 0
        self.generators = {}
        self.names = {}
        self.deps = {}
        self.match_lists = {}

    def translate(self, module):
        generators = {} if self.history_free else self.generators
        names = {} if self.history_free else self.names
        rows = []
        for decl, var in module.visible:
            gen = generators.setdefault(decl, Generator()) if self.history_free else decl.generator
            if self.history_free:
                name = names.setdefault(var, '')
                if not name:
                    name = gen.fresh(var.name + '#')
                    names[var] = name
            else:
                if var.unique is None:
                    var.unique = gen.fresh(var.name + '#')
                name = var.unique
            # Models imported-function bare counters and nested handle counters.
            finite = gen.fresh('$finiteMap#')
            handle = gen.nested('$fh$').fresh('#heap#')
            rows.append((decl.name, name, finite, handle))
        ids = []
        for index in range(module.count):
            if self.history_free:
                dep = 'id' + module.identity.encode().hex().upper() + '_' + str(index)
            else:
                dep = 'id' + str(self.global_dep)
                self.global_dep += 1
            ids.append(dep)
        self.deps[module.identity] = ids
        return tuple(rows), tuple(ids)


def fixture():
    f, g = Decl('A.f'), Decl('A.g')
    x, y = Var('x'), Var('y')
    return [Module('A', ((f, x), (g, y))), Module('B', ((f, x),)),
            Module('C', ((f, x), (g, y))), Module('Unrelated', ())]


def oracle(module):
    # Independent declarative oracle: all declarations begin at zero, and a
    # variable's first occurrence is assigned index zero for this fixture.
    rows = tuple((decl.name, var.name + '#0', '$finiteMap#0', '$fh$0#heap#0')
                 for decl, var in module.visible)
    ids = tuple('id' + module.identity.encode().hex().upper() + '_' + str(i)
                for i in range(module.count))
    return rows, ids


class NamingTests(unittest.TestCase):
    def test_all_module_permutations_match_isolated_oracle(self):
        modules = fixture()
        for order in permutations(modules):
            state = State(True)
            for module in order:
                with self.subTest(order=[m.identity for m in order], module=module.identity):
                    self.assertEqual(oracle(module), state.translate(module))

    def test_unrelated_insertion_preserves_every_existing_output(self):
        a, b, c, unrelated = fixture()
        state = State(True)
        before = {m.identity: state.translate(m) for m in (a, b, c)}
        for order in ((unrelated, a, b, c), (a, unrelated, b, c), (a, b, c, unrelated)):
            state = State(True)
            after = {m.identity: state.translate(m) for m in order}
            self.assertEqual(before, {name: after[name] for name in before})

    def test_legacy_imported_generator_is_a_negative_control(self):
        a, b, c, _ = fixture()
        state = State(False)
        state.translate(a)
        contaminated = state.translate(b)
        self.assertNotEqual(oracle(b), contaminated)
        self.assertEqual('$fh$1#heap#0', contaminated[0][0][3])

    def test_preexisting_ast_memos_are_ignored(self):
        a, b, c, _ = fixture()
        old = State(False)
        for _ in range(7):
            old.translate(a)
        self.assertEqual(oracle(b), State(True).translate(b))
        # The new mode must not mutate the off-mode names/counters.
        saved = (a.visible[0][1].unique, dict(a.visible[0][0].generator.counts[0]))
        State(True).translate(a)
        self.assertEqual(saved, (a.visible[0][1].unique, a.visible[0][0].generator.counts[0]))

    def test_concurrent_translations_share_only_immutable_input(self):
        modules = fixture()
        jobs = modules * 32
        with ThreadPoolExecutor(max_workers=4) as pool:
            results = list(pool.map(lambda m: State(True).translate(m), jobs))
        self.assertEqual([oracle(m) for m in jobs], results)

    def test_proof_labels_are_injective_and_preserve_boogie_separator(self):
        identities = ('A', 'A_B', 'A$B', 'A/B', 'A.B', 'λ', '', '_')
        labels = [State(True).translate(Module(s, ()))[1][i]
                  for s in identities for i in range(2)]
        self.assertEqual(len(labels), len(set(labels)))
        self.assertTrue(all('$' not in label for label in labels))
        for label in labels:
            compound = label + '$' + label + '$requires'
            self.assertEqual([label, label, 'requires'], compound.split('$'))

    def test_retranslation_replaces_old_module_contribution(self):
        state = State(True)
        m = Module('M', (), count=3)
        state.translate(m)
        state.translate(Module('N', (), count=2))
        state.translate(Module('M', (), count=1))
        self.assertEqual({'M': ['id4D_0'], 'N': ['id4E_0', 'id4E_1']}, state.deps)

    def test_reporting_uses_only_the_current_module_dependency_map(self):
        state = State(True)
        a, b, c, unrelated = fixture()
        state.translate(b)
        before = tuple(state.deps[b.identity])
        for m in (a, c, unrelated):
            state.translate(m)
        self.assertEqual(before, tuple(state.deps[b.identity]))
        self.assertEqual(oracle(b)[1], before)
        # The aggregate is intentionally different and is not report authority.
        aggregate = tuple(x for values in state.deps.values() for x in values)
        self.assertNotEqual(before, aggregate)

    def test_nested_generator_is_prefix_preserving(self):
        gen = Generator()
        self.assertEqual('$finiteMap#0', gen.fresh('$finiteMap#'))
        self.assertTrue(gen.fresh('$finiteMap#').startswith('$finiteMap#'))
        child = gen.nested('$l#')
        self.assertEqual('$l#0#heap#0', child.fresh('#heap#'))
        self.assertEqual('$l#0#heap#1', child.fresh('#heap#'))

    def test_push_pop_separates_sibling_scopes(self):
        gen = Generator()
        values = [gen.fresh('x#')]
        gen.push()
        values.append(gen.fresh('x#'))
        gen.push()
        values.append(gen.fresh('x#'))
        gen.pop()
        gen.pop()
        gen.push()
        values.append(gen.fresh('x#'))
        gen.pop()
        self.assertEqual(['x#0', 'x#0_0', 'x#0_0_0', 'x#1_0'], values)
        self.assertEqual(len(values), len(set(values)))

    def test_dependency_digest_must_not_prefix_all_names(self):
        # The observed dependency signature is unchanged by a private-body edit.
        signature = 'function f(x:int):int ensures result == x'
        resolution_keys = [hashlib.sha256((signature + body).encode()).hexdigest()
                           for body in ('{ x }', '{ x + 0 }')]
        self.assertNotEqual(*resolution_keys)
        a = Module('Client', ())
        output1 = State(True).translate(a)
        output2 = State(True).translate(a)
        self.assertEqual(output1, output2)
        # A bad design would gratuitously break otherwise identical task bytes.
        bad = [json.dumps((key, output1)) for key in resolution_keys]
        self.assertNotEqual(*bad)

    def test_missing_match_cases_are_local(self):
        shared = []
        constructors = ('A', 'B', 'C')
        def translate(cases):
            missing = list(shared)
            if not missing:
                missing.extend(ctor for ctor in constructors if ctor not in cases)
            return missing
        self.assertEqual(['B', 'C'], translate(('A',)))
        self.assertEqual(['A', 'C'], translate(('B',)))
        self.assertEqual([], shared)


if __name__ == '__main__':
    unittest.main(verbosity=2)
