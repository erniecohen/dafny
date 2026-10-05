#!/usr/bin/env python3
"""Audit emitted Boogie for the finite-support boundary (no solver invocation).

Use the companion Inputs/git-issue-82-encoding.dfy probe. Emit --bprint in two
separate verify runs with --boogie /proc:__NoMatches__: one ordinary run, and
one adding --boogie /printLambdaLifting. Supply an actual verification query
captured separately with --solver-log query-@PROC@.smt2.
The checker parses formula structure, compares alpha-normalized characteristic
maps including their free captures, and runs mutations on the parsed formulas.
It is a regression audit of this encoding fragment, not a proof of the prelude.
"""

import argparse
from collections import Counter
from dataclasses import dataclass, replace
from functools import cached_property
import hashlib
import json
from pathlib import Path
import re
import sys
import unittest


TOKEN = re.compile(r'//[^\n]*|/\*.*?\*/|"(?:\\.|[^"\\])*"|<==>|==>|<==|::|:=|&&|\|\||==|!=|<=|>=|<:|\+\+|[A-Za-z_$#][A-Za-z0-9_$#.?\'\\]*|[0-9]+(?:\.[0-9]+)?|[^\s]', re.S)
PAIRS = {"(": ")", "[": "]", "{": "}"}
CONVERSION = "Set#FromBoogieMap"
MEMBER = "Set#IsMember"
MARKERS = (
    "Encoding82Finite", "Encoding82SimpleISet", "Encoding82EmptyOwn",
    "Encoding82EmptyOuter", "Encoding82EmptyQuantifier", "Encoding82Guarded", "Encoding82Typed",
    "Encoding82Generic", "Encoding82Map", "Encoding82Tuple", "Encoding82TupleKey", "Encoding82ReverseTupleKey",
    "Encoding82IMap", "Encoding82Nested", "Encoding82KeyOnly", "Encoding82Lambda",
    "Encoding82Conditional", "Encoding82ConditionalLambda",
)


def lex(text):
    return [m.group() for m in TOKEN.finditer(text)
            if not m.group().startswith(("//", "/*"))]


def mate(ts, start):
    stack = []
    for i in range(start, len(ts)):
        t = ts[i]
        if t in PAIRS:
            stack.append(PAIRS[t])
        elif stack and t == stack[-1]:
            stack.pop()
            if not stack:
                return i
    raise ValueError("unclosed delimiter at " + str(start))


def tops(ts):
    stack = []
    for i, t in enumerate(ts):
        if not stack:
            yield i, t
        if t in PAIRS:
            stack.append(PAIRS[t])
        elif stack and t == stack[-1]:
            stack.pop()


def split(ts, delimiter):
    positions = [i for i, t in tops(ts) if t == delimiter]
    previous = 0
    result = []
    for i in positions:
        result.append(ts[previous:i])
        previous = i + 1
    result.append(ts[previous:])
    return result


def binders(ts):
    """Term binders only; preserve backend types for the projection audit."""
    pending = []
    result = []
    for part in split(ts, ","):
        colon = next((i for i, t in tops(part) if t == ":"), None)
        if colon is None:
            pending.extend(part)
        else:
            names = pending + part[:colon]
            result.extend((name, "".join(part[colon + 1:])) for name in names
                          if name not in (",", ""))
            pending = []
    return tuple(result)


def parameter_sorts(ts):
    """Boogie permits unnamed function input/output parameters."""
    return tuple("".join(part[next((i + 1 for i, t in tops(part) if t == ":"), 0):])
                 for part in split(ts, ",") if part)


@dataclass(frozen=True)
class Node:
    op: str
    args: tuple = ()
    text: str = ""
    bound: tuple = ()
    triggers: tuple = ()

    @cached_property
    def alpha_key(self):
        return alpha(self, {})

    @cached_property
    def free_names(self):
        return frozenset(free(self, frozenset()))


def parse(ts):
    ts = list(ts)
    while ts and ts[0] == "(" and mate(ts, 0) == len(ts) - 1:
        ts = ts[1:-1]
    if not ts:
        return Node("raw")
    if ts[0] == "var":
        assign = next((i for i, t in tops(ts) if t == ":="), None)
        end = next((i for i, t in tops(ts) if t == ";"), None)
        if assign is None or end is None:
            raise ValueError("let has no := or ;")
        header = ts[1:assign]
        names = binders(header)
        if not names:
            names = tuple(("".join(p), "") for p in split(header, ","))
        rest = ts[end + 1:]
        while rest and rest[0] == "{" and rest[1] == ":":
            rest = rest[mate(rest, 0) + 1:]
        rhss = tuple(parse(p) for p in split(ts[assign + 1:end], ","))
        if len(rhss) != len(names):
            raise ValueError("let binder/RHS arity mismatch")
        return Node("let", rhss + (parse(rest),), bound=names)
    # Quantifiers bind the complete expression following ::, including its
    # top-level connectives. Parse them before applying connective precedence.
    if ts[0] in ("forall", "exists", "lambda"):
        delimiter = next((i for i, t in tops(ts) if t == "::"), None)
        if delimiter is None:
            raise ValueError("quantifier has no ::")
        header = ts[1:delimiter]
        if header and header[0] == "<":
            end = header.index(">")
            header = header[end + 1:]
        rest = ts[delimiter + 1:]
        patterns = []
        while rest and rest[0] == "{":
            end = mate(rest, 0)
            if len(rest) > 1 and rest[1] != ":":
                patterns.append(tuple(parse(p) for p in split(rest[1:end], ",")))
            rest = rest[end + 1:]
        return Node(ts[0], (parse(rest),), bound=binders(header), triggers=tuple(patterns))
    if ts[0] == "if":
        positions = {t: i for i, t in tops(ts) if t in ("then", "else")}
        if set(positions) == {"then", "else"}:
            a, b = positions["then"], positions["else"]
            return Node("if", (parse(ts[1:a]), parse(ts[a + 1:b]), parse(ts[b + 1:])))
    for operators in (("<==>",), ("==>", "<=="), ("||",), ("&&",),
                      ("==", "!=", "<", ">", "<=", ">=", "<:"),
                      ("+", "-", "++"), ("*", "/", "%")):
        positions = [(i, t) for i, t in tops(ts) if t in operators and i > 0]
        if positions:
            i, op = positions[0] if operators == ("==>", "<==") else positions[-1]
            return Node(op, (parse(ts[:i]), parse(ts[i + 1:])))
    if ts[0] in ("!", "-"):
        return Node(ts[0], (parse(ts[1:]),))
    coercion = next((i for i, t in tops(ts) if t == ":"), None)
    if coercion is not None:
        return Node("coerce", (parse(ts[:coercion]),), "".join(ts[coercion + 1:]))
    # Selection is a postfix operation on either a lambda or a lifted map call.
    if ts[-1] == "]":
        starts = [i for i, t in tops(ts) if t == "[" and i > 0]
        if starts and mate(ts, starts[-1]) == len(ts) - 1:
            i = starts[-1]
            return Node("select", (parse(ts[:i]),) + tuple(parse(a) for a in split(ts[i + 1:-1], ",")))
    opens = [i for i, t in tops(ts) if t == "(" and i > 0]
    if opens and mate(ts, opens[0]) == len(ts) - 1:
        i = opens[0]
        arguments = split(ts[i + 1:-1], ",") if i + 1 < len(ts) - 1 else []
        return Node("call", tuple(parse(a) for a in arguments), "".join(ts[:i]))
    if len(ts) == 1:
        return Node("literal" if ts[0] in ("true", "false") or ts[0][0].isdigit() else "id", text=ts[0])
    return Node("raw", text=" ".join(ts))


def walk(node, ancestors=()):
    yield node, ancestors
    for i, child in enumerate(node.args):
        yield from walk(child, ancestors + ((node, i),))


def calls(node, name=None):
    return [n for n, _ in walk(node) if n.op == "call" and (name is None or n.text == name)]


def free(node, environment=None):
    if environment is None:
        return set(node.free_names)
    if node.op == "id":
        return set() if node.text in environment else {node.text}
    if node.op == "let":
        inside = environment | {name for name, _ in node.bound}
        return set().union(*(free(c, environment) for c in node.args[:-1]), free(node.args[-1], inside))
    inside = environment | {name for name, _ in node.bound}
    result = set().union(*(free(c, inside) for c in node.args)) if node.args else set()
    for pattern in node.triggers:
        for term in pattern:
            result.update(free(term, inside))
    return result


def alpha(node, environment=None, next_index=0):
    """Alpha-rename binders; deliberately retain free/captured arguments."""
    if environment is None:
        return node.alpha_key
    env = dict(environment)
    if node.op == "let":
        return alpha(expand_lets(node), env, next_index)
    names = []
    for name, typ in node.bound:
        replacement = ("bound", next_index)
        next_index += 1
        env[name] = replacement
        names.append(typ)
    text = env.get(node.text, node.text) if node.op == "id" else node.text
    return (node.op, text, tuple(names), tuple(alpha(c, env, next_index) for c in node.args),
            tuple(tuple(alpha(t, env, next_index) for t in p) for p in node.triggers))


def expand_lets(node, aliases=None):
    """Resolve simultaneous lexical aliases for semantic comparisons.

    Callers retain the original formulas for raw-trigger legality. Substitution
    respects shadowing, so a same-named inner binder cannot steal a capture.
    """
    aliases = {} if aliases is None else dict(aliases)
    if node.op == "id" and node.text in aliases:
        return aliases[node.text]
    if node.op == "let":
        values = tuple(expand_lets(c, aliases) for c in node.args[:-1])
        aliases.update(zip((n for n, _ in node.bound), values))
        return expand_lets(node.args[-1], aliases)
    for name, _ in node.bound:
        aliases.pop(name, None)
    return replace(node, args=tuple(expand_lets(c, aliases) for c in node.args),
                   triggers=tuple(tuple(expand_lets(t, aliases) for t in p) for p in node.triggers))


def conjunctions(node):
    return sum((conjunctions(child) for child in node.args), []) if node.op == "&&" else [node]


def conjunction(nodes):
    if not nodes:
        return Node("literal", text="true")
    result = nodes[0]
    for node in nodes[1:]:
        result = Node("&&", (result, node))
    return result


def backend_map_sort(sort):
    # The emitted prelude names [Box]bool as ISet. It is a backend predicate
    # map, whereas Set is the independent finite-set representation.
    return sort.startswith("[") or sort == "ISet"


def simplify_witness_exists(node):
    """Normalize exists x :: x == t && P(x) to P(t), keeping all guards."""
    if node.op != "exists":
        return node
    remaining = list(node.bound)
    parts = list(conjunctions(node.args[0]))
    for name, _ in node.bound:
        for index, part in enumerate(parts):
            if part.op != "==":
                continue
            replacement = None
            for variable, term in (part.args, part.args[::-1]):
                if variable.op == "id" and variable.text == name and name not in free(term):
                    replacement = term
                    break
            if replacement is None:
                continue
            parts.pop(index)
            parts = [expand_lets(part, {name: replacement}) for part in parts]
            remaining = [pair for pair in remaining if pair[0] != name]
            break
    body = conjunction(parts)
    return Node("exists", (body,), bound=tuple(remaining)) if remaining else body



def full_witness_body_instance(relation, candidate):
    """Match the complete typed predicate under uniform, capture-free witnesses.

    Track binding identities on both sides: equal printed names do not make an
    inner-bound variable a legal free witness, nor preserve a captured formal.
    This recognizes an instance of the existential body, not a tuple inverse.
    """
    witnesses = dict(relation.bound)
    substitutions = {}

    def known_sort(term):
        if term.op == "coerce":
            return term.text
        if term.op == "literal":
            return "bool" if term.text in ("true", "false") else ("real" if "." in term.text else "int")
        if term.op == "-" and len(term.args) == 1:
            return known_sort(term.args[0])
        if term.op == "call" and re.fullmatch(r"_System\.Tuple[0-9]+\._[0-9]+", term.text):
            return "Box"
        return None

    def match(template, actual, template_scope=None, actual_scope=None, depth=0):
        template_scope = {} if template_scope is None else template_scope
        actual_scope = {} if actual_scope is None else actual_scope
        if template.op == "id":
            if template.text in template_scope:
                return actual.op == "id" and actual_scope.get(actual.text) == template_scope[template.text]
            if template.text in witnesses:
                if free(actual) & (set(witnesses) | set(actual_scope)):
                    return False
                if known_sort(actual) != witnesses[template.text]:
                    return False
                prior = substitutions.setdefault(template.text, actual)
                return alpha(prior) == alpha(actual)
            # A free source capture cannot become bound by an alpha-renamed
            # candidate quantifier, even if its printed name remains identical.
            return (actual.op == "id" and actual.text == template.text and
                    actual.text not in actual_scope)
        if (template.op != actual.op or template.text != actual.text or
                len(template.args) != len(actual.args) or len(template.bound) != len(actual.bound) or
                len(template.triggers) != len(actual.triggers)):
            return False
        nested_template, nested_actual = dict(template_scope), dict(actual_scope)
        for index, ((name, sort), (other, other_sort)) in enumerate(zip(template.bound, actual.bound)):
            if sort != other_sort:
                return False
            identity = (depth, index)
            nested_template[name], nested_actual[other] = identity, identity
        if template.op == "let":
            # Let RHSs are simultaneous and use the preceding lexical scope.
            if not all(match(left, right, template_scope, actual_scope, depth + 1)
                       for left, right in zip(template.args[:-1], actual.args[:-1])):
                return False
            arguments = [(template.args[-1], actual.args[-1])]
        else:
            arguments = zip(template.args, actual.args)
        return (all(match(left, right, nested_template, nested_actual, depth + 1) for left, right in arguments) and
                all(len(left) == len(right) and
                    all(match(a, b, nested_template, nested_actual, depth + 1) for a, b in zip(left, right))
                    for left, right in zip(template.triggers, actual.triggers)))

    return (match(relation.args[0], candidate) and substitutions.keys() == witnesses.keys() and
            not (free(candidate) & set(witnesses)))


def inhabited_source_guard(relation, ancestors):
    target = simplify_witness_exists(Node("exists", relation.args, bound=relation.bound))
    required = {alpha(part) for part in conjunctions(target)}
    available = set()
    for guard in guards(ancestors):
        for part in conjunctions(guard):
            if part.op == "||":
                original, candidate = part.args
                if (alpha(simplify_witness_exists(original)) != alpha(target) or
                        not full_witness_body_instance(relation, candidate)):
                    continue
                # E || P(candidate) equals E only when the complete candidate
                # predicate implies this exact retained source existential.
                part = original
            available.update(alpha(piece) for piece in conjunctions(simplify_witness_exists(part)))
    return required <= available


def fixed_map_aliases(node):
    """Find exact dominating equalities for universally scoped map aliases.

    Only a genuine characteristic/closure lambda (or its lifted factory) may
    fix the alias; a quantified backend map or wrapper around one is rejected.
    The fixed expression cannot depend on any of this quantifier's arguments.
    """
    if node.op != "forall" or node.args[0].op != "==>":
        return {}
    own = {name for name, _ in node.bound}
    maps = {name for name, sort in node.bound if backend_map_sort(sort)}
    result = {}
    for guard in conjunctions(node.args[0].args[0]):
        if guard.op != "==":
            continue
        for alias, actual in (guard.args, guard.args[::-1]):
            if alias.op != "id" or alias.text not in maps:
                continue
            if actual.op != "lambda" and not (actual.op in ("call", "id") and actual.text.startswith("lambda#")):
                continue
            if free(actual) & own:
                continue
            result[alias.text] = (actual, guard)
    return result


def instantiate_fixed_map_aliases(node):
    """Equality substitution for semantic comparisons; patterns stay raw.

    This is the fixed-map instance D(M), not the unrestricted all-map schema.
    The raw formula is retained separately for trigger and guard-drop checks.
    """
    fixed = fixed_map_aliases(node)
    if fixed:
        aliases = {name: actual for name, (actual, _) in fixed.items()}
        discarded = {guard for _, guard in fixed.values()}
        remaining = [g for g in conjunctions(node.args[0].args[0]) if g not in discarded]
        consequent = expand_lets(node.args[0].args[1], aliases)
        if remaining:
            consequent = Node("==>", (expand_lets(conjunction(remaining), aliases), consequent))
        bound = tuple((name, typ) for name, typ in node.bound if name not in fixed)
        if not bound:
            return instantiate_fixed_map_aliases(consequent)
        node = replace(node, bound=bound, args=(consequent,),
                       triggers=tuple(tuple(expand_lets(t, aliases) for t in pattern) for pattern in node.triggers))
    return replace(node, args=tuple(instantiate_fixed_map_aliases(c) for c in node.args))


def closed_false_characteristic(characteristic, rows):
    if free(characteristic):
        return False
    if characteristic.op == "lambda":
        return characteristic.args[0] == Node("literal", text="false")
    if characteristic.op != "call" or not characteristic.text.startswith("lambda#"):
        return False
    # A lifted empty lambda may carry the literal false as a captured parameter.
    # Confirm its actual pointwise defining axiom before treating it as empty.
    for row in rows:
        for equation, ancestors in walk(row.expr):
            if equation.op != "==":
                continue
            for selected, value in (equation.args, equation.args[::-1]):
                if selected.op != "select" or len(selected.args) != 2:
                    continue
                factory, element = selected.args
                if factory.op != "call" or factory.text != characteristic.text or len(factory.args) != len(characteristic.args):
                    continue
                if not all(arg.op == "id" for arg in factory.args) or element.op != "id":
                    continue
                if not any(parent.op == "forall" and (element.text, "Box") in parent.bound for parent, _ in ancestors):
                    continue
                aliases = {formal.text: actual for formal, actual in zip(factory.args, characteristic.args)}
                if expand_lets(value, aliases) == Node("literal", text="false"):
                    return True
    return False


@dataclass
class Statement:
    kind: str
    expr: Node
    context: str = ""
    scope: str = ""

    @cached_property
    def source_contexts(self):
        names = " ".join(c.text for c in calls(self.expr)) + " " + self.context
        return {marker for marker in MARKERS if marker in names}


def statements(text):
    ts = lex(text)
    result = []
    implementation_scopes = []
    for index, token in enumerate(ts):
        if token != "implementation":
            continue
        name_index = index + 1
        while ts[name_index] == "{":
            name_index = mate(ts, name_index) + 1
        name = ts[name_index]
        body = next((name_index + 1 + offset for offset, part in tops(ts[name_index + 1:]) if part == "{"), None)
        if body is not None:
            implementation_scopes.append((body, mate(ts, body), name))
    for i, token in enumerate(ts):
        if token not in ("axiom", "assume", "assert", "requires", "ensures"):
            continue
        rest = ts[i + 1:]
        end = next((j for j, t in tops(rest) if t == ";"), None)
        if end is None:
            raise ValueError("unterminated " + token)
        body = rest[:end]
        while body and body[0] == "{":
            if len(body) < 2 or body[1] != ":":
                break
            body = body[mate(body, 0) + 1:]
        scope = next((name for start, end, name in implementation_scopes if start < i < end), "")
        result.append(Statement(token, parse(body), scope=scope))
    return result


def signatures(text):
    ts = lex(text)
    result = {}
    for i, t in enumerate(ts):
        if t != "function":
            continue
        j = i + 1
        while ts[j] == "{":
            j = mate(ts, j) + 1
        name = ts[j]
        if not name.startswith("map$project"):
            continue
        j += 1
        if ts[j] != "(":
            raise ValueError("unexpected projection declaration " + name)
        end = mate(ts, j)
        parameters = parameter_sorts(ts[j + 1:end])
        j = end + 1
        if ts[j] == "returns":
            j += 1
            end = mate(ts, j)
            outputs = parameter_sorts(ts[j + 1:end])
            returned = outputs[0]
        elif ts[j] == ":":
            end = next(k for k in range(j + 1, len(ts)) if ts[k] in (";", "{"))
            returned = "".join(ts[j + 1:end])
        else:
            raise ValueError("unexpected projection return type " + name)
        result[name] = (parameters, returned)
    return result


def contexts(statement):
    return statement.source_contexts


@dataclass
class Definition:
    statement: Statement
    equation: Node
    member: Node
    characteristic: Node
    element: Node
    ancestors: tuple


def definitions(rows):
    result = []
    for row in rows:
        for equation, ancestors in walk(row.expr):
            if equation.op not in ("<==>", "==") or len(equation.args) != 2:
                continue
            for member, selection in (equation.args, equation.args[::-1]):
                if member.op != "call" or member.text != MEMBER or len(member.args) != 2:
                    continue
                conversion, element = member.args
                if conversion.op != "call" or conversion.text != CONVERSION or len(conversion.args) != 1:
                    continue
                if selection.op != "select" or len(selection.args) != 2:
                    continue
                result.append(Definition(row, equation, member, conversion.args[0], element, ancestors))
                break
    return result


def guards(ancestors):
    return [n.args[0] for n, child in ancestors if n.op == "==>" and child == 1]


def contains(node, predicate):
    return any(predicate(n) for n, _ in walk(node))


def projection(node):
    return node.op == "call" and node.text.startswith("map$project")


def trigger_application(node):
    while node.op == "coerce":
        node = node.args[0]
    return node.op in ("call", "select")


def relation_template(node):
    if node.op == "lambda":
        return node
    if node.op == "select" and len(node.args) == 2 and node.args[0].op == "lambda":
        family = node.args[0]
        if len(family.bound) == 1 and family.bound[0][1] == "Box" and family.args[0].op == "lambda":
            return family.args[0]
    return None


def witness_relation(node):
    template = relation_template(node)
    if template is not None and node.op == "select":
        return expand_lets(template, {node.args[0].bound[0][0]: node.args[1]})
    return template


def subset_constraint(node, own):
    return ((node.op == "==" and bool(free(node) & own) and
             contains(node, lambda z: z.op == "literal" and z.text == "0")) or
            (node.op == "call" and node.text in ("$Is", "$IsBox") and
             bool(free(node) & own) and
             contains(node, lambda z: "Encoding82Tiny" in z.text)))


def boxed_key_equation(relation, ancestors=(), row=None):
    boxed_captures = {name for parent, _ in ancestors for name, typ in parent.bound if typ == "Box"}
    admitted = set()
    if row is not None:
        admitted = {c.args[0].text for c in calls(row.expr, "$IsBox") if c.args and c.args[0].op == "id"}
    return contains(relation.args[0], lambda n: n.op == "==" and any(
        contains(side, lambda c: (c.op == "call" and c.text.startswith("$Box")) or
                 (c.op == "id" and c.text in boxed_captures & admitted)) for side in n.args))


def capture_instance(actual, pattern, captures, substitutions=None):
    """Match a universally scoped source capture to its current typed term.

    Internal binders are alpha-normalized. Function inputs remain exact free
    names. Inner source formals may be instantiated (for example i:int becomes
    Unbox(y):int inside an enclosing set characteristic lambda); repeated uses
    of one source formal must receive the same term.
    """
    substitutions = {} if substitutions is None else substitutions
    if isinstance(pattern, tuple) and len(pattern) == 5 and pattern[0] == "id" and pattern[1] in captures:
        name = pattern[1]
        if name in substitutions:
            return substitutions[name] == actual
        substitutions[name] = actual
        return True
    if not isinstance(pattern, tuple) or not isinstance(actual, tuple):
        return pattern == actual
    return len(actual) == len(pattern) and all(capture_instance(a, p, captures, substitutions) for a, p in zip(actual, pattern))


def matching_definition(actual, definition):
    universal = [n for n, _ in definition.ancestors if n.op == "forall"]
    pinned = set()
    for quantifier in universal:
        for pattern in quantifier.triggers:
            for term in pattern:
                if contains(term, lambda n: n.op == "call" and any(n.text.endswith(marker) for marker in MARKERS)):
                    pinned.update(name for name, _ in quantifier.bound)
    captures = {name for n in universal for name, _ in n.bound
                if name != definition.element.text and name not in pinned}
    return capture_instance(alpha(actual), alpha(definition.characteristic), captures)


def audit(rows, decls, require_probe=True, lifted=False, source_triggers=True):
    findings = []
    evidence = Counter()
    def fail(code, message):
        item = {"code": code, "message": message}
        if item not in findings:
            findings.append(item)
    original_rows = rows
    rows = [Statement(row.kind, instantiate_fixed_map_aliases(expand_lets(row.expr)), row.context, row.scope) for row in rows]
    available = set().union(*(contexts(row) for row in rows)) if rows else set()
    if require_probe:
        for marker in MARKERS:
            if marker not in available:
                fail("input", "missing probe context " + marker)
    defs = definitions(rows)
    by_context = {marker: [d for d in defs if marker in contexts(d.statement)] for marker in MARKERS}
    if source_triggers:
        for row in original_rows:
            for n, _ in walk(row.expr):
                if n.op != "forall":
                    continue
                handles = {name for name, sort in n.bound if sort.startswith("[") and sort.endswith("HandleType")}
                selector_trigger = any(term.op == "call" and term.text.startswith(("Reads", "Requires", "Apply"))
                                       and contains(term, lambda z: (z.op == "select" or z.op == "call" and z.text == "AtLayer")
                                                    and z.args and z.args[0].op == "id" and z.args[0].text in handles)
                                       for pattern in n.triggers for term in pattern)
                if selector_trigger and not handles <= set(fixed_map_aliases(n)):
                    fail("E08", "source arrow-selector family is not fixed to its actual closure lambda")
                own = {name for name, _ in n.bound}
                used = free(n.args[0]) & own
                for pattern in n.triggers:
                    evidence["lifted_trigger_patterns" if lifted else "raw_trigger_patterns"] += 1
                    covered = set().union(*(free(term) for term in pattern)) & own
                    if not used <= covered:
                        fail("E10", "trigger does not bind all used quantifier variables")
                    if any(not trigger_application(term) or contains(term, lambda c: c.op in ("forall", "exists", "lambda", "==", "!=", "<", ">", "<=", ">=", "&&", "||", "==>", "<==>", "if")) for term in pattern):
                        fail("E10", "trigger contains an invalid compound/binder expression before resolution")
    for row in rows:
        for n, ancestors in walk(row.expr):
            if n.op == "raw" and (CONVERSION in n.text or "map$project" in n.text):
                fail("input", "unsupported syntax in finite/projection formula: " + n.text[:100])
            if n.op in ("<==>", "==") and len(n.args) == 2:
                for member, selected in (n.args, n.args[::-1]):
                    if (member.op == "call" and member.text == MEMBER and
                            selected.op == "select" and selected.args[0].op == "id"):
                        arbitrary = selected.args[0].text
                        sort = next((typ for parent, _ in ancestors for name, typ in parent.bound if name == arbitrary), "")
                        if backend_map_sort(sort):
                            fail("E01", "arbitrary-map finite membership preservation, including a renamed wrapper")
            if n.op == "call" and n.text == "SetRef_to_SetBox":
                fail("E01", "reference-map finite bridge remains in a formula")
            if n.op == "call" and n.text == CONVERSION and n.args and n.args[0].op == "id":
                argument = n.args[0].text
                bound_type = next((typ for parent, _ in ancestors for name, typ in parent.bound if name == argument), "")
                if backend_map_sort(bound_type):
                    fail("E01", "finite conversion of an arbitrary quantified backend map")
        evidence["formulas"] += 1
    for d in defs:
        evidence["finite_definitions"] += 1
        selection = d.equation.args[1] if d.equation.args[0] is d.member else d.equation.args[0]
        if alpha(d.characteristic) != alpha(selection.args[0]) or alpha(d.element) != alpha(selection.args[1]):
            fail("E02", "membership definition selects a different characteristic map or element")
        enclosing = [n for n, _ in d.ancestors if n.op == "forall" and any(name == d.element.text for name, _ in n.bound)]
        if not enclosing or not any(alpha(term) == alpha(d.member) for pattern in enclosing[-1].triggers for term in pattern):
            fail("E10", "finite definition lacks its bound membership-access trigger")
    # Compare the actual conversions with definitions in their emitted source
    # context. Free names remain in the fingerprint, so changed captures fail.
    for row in rows:
        markers = contexts(row)
        if not markers:
            continue
        context_defs = [d for d in defs
                        if ((row.scope and d.statement.scope == row.scope)
                            or (contexts(d.statement) & markers and
                                (not row.scope or not d.statement.scope or row.scope == d.statement.scope)))]
        for n, ancestors in walk(row.expr):
            if n.op != "call" or n.text != CONVERSION:
                continue
            if any(parent is d.equation for parent, _ in ancestors for d in defs):
                continue
            evidence["actual_finite_values"] += 1
            if not any(matching_definition(n.args[0], d) for d in context_defs):
                fail("E02", "actual characteristic map/captures have no matching definition in " + ",".join(sorted(markers)))
    if require_probe:
        for marker in ("Encoding82Finite", "Encoding82EmptyOwn", "Encoding82Guarded", "Encoding82Typed", "Encoding82Generic", "Encoding82Map", "Encoding82Tuple", "Encoding82TupleKey", "Encoding82ReverseTupleKey", "Encoding82Nested", "Encoding82KeyOnly", "Encoding82Conditional", "Encoding82ConditionalLambda"):
            if not by_context[marker]:
                fail("E02", "no finite definition observed in " + marker)
    for d in by_context["Encoding82EmptyOwn"]:
        # This probe has no integer argument. An enclosing integer witness binder
        # outside the characteristic lambda would wrongly make the fact vacuous.
        if any(typ == "int" for n, _ in d.ancestors if n.op == "forall" for _, typ in n.bound):
            fail("E03", "empty-own definition was placed under an own integer witness")
    for d in by_context["Encoding82Guarded"]:
        if not any(contains(g, lambda n: n.op in ("!=", "!") and contains(n, lambda z: z.op == "literal" and z.text == "0")) for g in guards(d.ancestors)):
            fail("E03", "guarded source definition escaped its n != 0 path")
    for d in by_context["Encoding82EmptyOuter"]:
        # Function definitions are admitted by canCall, whose source requires
        # predicate is defined under the formal's empty subset constraint.
        if not any(contains(g, lambda n: n.op == "call" and "Encoding82EmptyOuter#canCall" in n.text) for g in guards(d.ancestors)):
            fail("E03", "function-scope definition escaped source availability")
    if require_probe:
        admission = [row for row in rows if "Encoding82EmptyOuter" in contexts(row)
                     and contains(row.expr, lambda n: "Encoding82EmptyOuter#requires" in n.text)]
        if not any(contains(row.expr, lambda n: n.op == "==>" and contains(n.args[0], lambda c: c.op == "literal" and c.text == "false")) for row in admission):
            fail("E03", "empty outer source predicate has no empty-domain admission")
    for d in by_context["Encoding82EmptyQuantifier"]:
        if not any(contains(g, lambda n: n.op == "literal" and n.text == "false") for g in guards(d.ancestors)):
            fail("E03", "definition escaped its empty outer quantified domain")
    for d in by_context["Encoding82Lambda"]:
        if closed_false_characteristic(d.characteristic, rows):
            evidence["closed_empty_characteristic_definitions"] += 1
            continue
        source = next((parent for parent, _ in reversed(d.ancestors)
                       if parent.op == "forall" and any(typ == "Heap" for _, typ in parent.bound)
                       and any(typ == "Box" for _, typ in parent.bound)), None)
        if source is None:
            fail("E04", "reference lambda definition has no current-heap argument scope")
            continue
        arguments = {name for name, typ in source.bound if typ == "Box"}
        heaps = {name for name, typ in source.bound if typ == "Heap"}
        if arguments & free(d.characteristic):
            evidence["reference_lambda_argument_captures"] += 1
        for argument in arguments:
            if not any(contains(g, lambda n: n.op == "call" and n.text == "$IsAllocBox"
                                and len(n.args) == 3 and n.args[0].op == "id" and n.args[0].text == argument
                                and n.args[2].op == "id" and n.args[2].text in heaps)
                       for g in guards(d.ancestors)):
                fail("E04", "reference lambda definition lacks allocation at its current heap")
            else:
                evidence["reference_lambda_allocation_guards"] += 1
    for marker in ("Encoding82SimpleISet", "Encoding82IMap"):
        if by_context[marker]:
            fail("E06", "infinite-only source context received a finite membership definition: " + marker)
    for name, (parameters, _) in decls.items():
        evidence["projection_declarations"] += 1
        if len(parameters) != 1 or not parameters[0].startswith("[") or not parameters[0].endswith("]bool"):
            fail("E08", "projection is keyed by a value instead of its witness relation: " + name)
    for row in rows:
        markers = contexts(row)
        for n, ancestors in walk(row.expr):
            if projection(n):
                evidence["projection_applications"] += 1
                if len(n.args) != 1:
                    fail("E08", "projection does not receive exactly one witness relation")
                elif n.args[0].op != "select" or len(n.args[0].args) != 2:
                    fail("E08", "projection argument is not a current key-indexed relation family")
                elif not lifted and witness_relation(n.args[0]) is None:
                    fail("E08", "raw projection argument has no typed source relation family")
                elif not lifted and markers:
                    relation = witness_relation(n.args[0])
                    if not boxed_key_equation(relation, ancestors, row):
                        fail("E04", "general-map relation lacks equality to the canonical boxed key")
                    if "Encoding82Generic" in markers:
                        own = {name for name, _ in relation.bound}
                        if not contains(relation.args[0], lambda c: c.op == "call" and c.text in ("$Is", "$IsBox") and bool(free(c) & own)):
                            fail("E04", "generic source witness lacks its type predicate")
                    if "Encoding82Typed" in markers:
                        own = {name for name, _ in relation.bound}
                        if not contains(relation.args[0], lambda c: subset_constraint(c, own)):
                            fail("E04", "subset source witness lacks its x == 0 constraint")
            if n.op == "select" and len(n.args) > 1 and all(projection(p) for p in n.args[1:]):
                evidence["coordinated_choices"] += 1
                relation = n.args[0]
                if not all(len(p.args) == 1 and alpha(p.args[0]) == alpha(relation) for p in n.args[1:]):
                    fail("E08", "selected tuple components use different relations")
                normalized = witness_relation(relation)
                if normalized is not None and len(n.args) - 1 != len(normalized.bound):
                    fail("E08", "choice does not select one component for every witness binder")
                if normalized is not None:
                    for (_, typ), p in zip(normalized.bound, n.args[1:]):
                        if p.text in decls and decls[p.text][1] != typ:
                            fail("E04", "choice component does not have its source witness representation")
                    if not inhabited_source_guard(normalized, ancestors):
                        fail("E08", "choice property is not guarded by its exact inhabited source relation")
                activation = next((parent for parent, _ in reversed(ancestors) if parent.op == "forall"), None)
                if activation is None or any(not any(alpha(term) == alpha(p) for pattern in activation.triggers for term in pattern)
                                              for p in n.args[1:]):
                    fail("E08", "choice does not activate on every selected projection access")
            if lifted and n.op == "lambda":
                fail("E10", "lambda remains after lambda lifting")
    return findings, dict(evidence)


def replace_node(node, old, new):
    if node is old:
        return new
    return replace(node, args=tuple(replace_node(c, old, new) for c in node.args),
                   triggers=tuple(tuple(replace_node(c, old, new) for c in p) for p in node.triggers))


def change(rows, statement, old, new):
    return [Statement(row.kind, replace_node(row.expr, old, new), row.context, row.scope) if row is statement else row for row in rows]


def mutations(rows, decls):
    original_rows = rows
    rows = [Statement(row.kind, instantiate_fixed_map_aliases(expand_lets(row.expr)), row.context, row.scope) for row in rows]
    defs = definitions(rows)
    results = []
    def run(name, mutated, code):
        errors, _ = audit(mutated, decls, source_triggers=False)
        codes = sorted({e["code"] for e in errors})
        results.append({"name": name, "expected": code, "detected": codes, "passed": code in codes})
    for name, code, predicate in (
        ("drop-finite-characteristic-alias-equality", "E01", lambda actual: actual.op == "lambda" and actual.bound == ((actual.bound[0][0], "Box"),) and actual.args[0].op != "lambda"),
        ("drop-map-witness-family-alias-equality", "E08", lambda actual: actual.op == "lambda" and actual.bound == ((actual.bound[0][0], "Box"),) and actual.args[0].op == "lambda"),
        ("drop-arrow-selector-family-alias-equality", "E08", lambda actual: actual.op == "lambda" and any(sort == "LayerType" for _, sort in actual.bound)),
    ):
        found = next(((row, guard) for row in original_rows for quantifier, _ in walk(row.expr)
                      for actual, guard in fixed_map_aliases(quantifier).values() if predicate(actual)), None)
        if found:
            errors, _ = audit(change(original_rows, found[0], found[1], Node("literal", text="true")), decls)
            codes = sorted({e["code"] for e in errors})
            results.append({"name": name, "expected": code, "detected": codes, "passed": code in codes})
    bridge = parse(lex("(forall m: [Box]bool, b: Box :: {Set#IsMember(Set#FromBoogieMap(m), b)} Set#IsMember(Set#FromBoogieMap(m), b) <==> m[b])"))
    run("restore-universal-bridge", rows + [Statement("axiom", bridge)], "E01")
    reference = parse(lex("(forall references: [ref]bool, b: Box :: {Set#IsMember(RenamedReferenceConversion(references), b)} Set#IsMember(RenamedReferenceConversion(references), b) <==> references[$Unbox(b): ref])"))
    run("restore-renamed-reference-bridge", rows + [Statement("axiom", reference)], "E01")
    inline = None
    for row in original_rows:
        for quantifier, ancestors in walk(row.expr):
            if quantifier.op != "forall":
                continue
            aliases = {}
            for parent, child in ancestors:
                if parent.op == "let" and child == len(parent.args) - 1:
                    values = [expand_lets(c, aliases) for c in parent.args[:-1]]
                    aliases.update(zip((name for name, _ in parent.bound), values))
            aliases.update({name: actual for name, (actual, _) in fixed_map_aliases(quantifier).items()})
            for pattern in quantifier.triggers:
                for term in pattern:
                    resolved = expand_lets(term, aliases)
                    if contains(resolved, lambda n: n.op == "lambda"):
                        inline = row, term, resolved
                        break
                if inline:
                    break
            if inline:
                break
        if inline:
            break
    if inline:
        errors, _ = audit(change(original_rows, *inline), decls)
        codes = sorted({e["code"] for e in errors})
        results.append({"name": "inline-characteristic-lambda-in-trigger", "expected": "E10", "detected": codes, "passed": "E10" in codes})
    else:
        results.append({"name": "inline-characteristic-lambda-in-trigger", "passed": False, "reason": "scoped trigger alias absent"})
    for marker, code, name, predicate in (
        ("Encoding82Typed", "E04", "drop-source-subset-guard", lambda n: subset_constraint(n, free(n))),
        ("Encoding82Generic", "E04", "drop-source-type-guard", lambda n: n.op == "call" and n.text in ("$Is", "$IsBox")),
        ("Encoding82Map", "E04", "drop-canonical-key-equation", lambda n: n.op == "==" and any(c.op == "call" and c.text.startswith("$Box") for c, _ in walk(n))),
    ):
        found = None
        for row in rows:
            if marker not in contexts(row):
                continue
            for p, _ in walk(row.expr):
                if projection(p) and p.args and relation_template(p.args[0]) is not None:
                    target = next((n for n, _ in walk(relation_template(p.args[0]).args[0]) if predicate(n)), None)
                    if target:
                        found = row, target
                        break
            if found:
                break
        if found:
            run(name, change(rows, found[0], found[1], Node("literal", text="true")), code)
        else:
            results.append({"name": name, "expected": code, "passed": False, "reason": "mutation target absent"})
    for marker in ("Encoding82TupleKey", "Encoding82ReverseTupleKey"):
        found = None
        for row in rows:
            if marker not in contexts(row):
                continue
            for selected, ancestors in walk(row.expr):
                if selected.op != "select" or len(selected.args) < 3 or not all(projection(p) for p in selected.args[1:]):
                    continue
                relation = witness_relation(selected.args[0])
                if relation is None:
                    continue
                expected = simplify_witness_exists(Node("exists", relation.args, bound=relation.bound))
                for guard in guards(ancestors):
                    for part in conjunctions(guard):
                        if (part.op == "||" and alpha(simplify_witness_exists(part.args[0])) == alpha(expected) and
                                full_witness_body_instance(relation, part.args[1])):
                            found = row, part
                            break
                    if found:
                        break
                if found:
                    break
            if found:
                break
        if not found:
            results.append({"name": marker + "-candidate-instance", "passed": False,
                            "reason": "retained source existential and complete candidate instance absent"})
            continue
        row, disjunction = found
        candidate = disjunction.args[1]
        for suffix, predicate in (
            ("drop-candidate-range", lambda c: c.op in ("<", "<=", ">", ">=")),
            ("drop-candidate-canonical-key", lambda c: c.op == "==" and contains(c, lambda n: n.op == "call" and n.text.startswith("$Box"))),
        ):
            conjunct = next((c for c in conjunctions(candidate) if predicate(c)), None)
            if conjunct is None:
                results.append({"name": marker + "-" + suffix, "passed": False, "reason": "candidate conjunct absent"})
            else:
                weakened = replace(disjunction, args=(disjunction.args[0], replace_node(candidate, conjunct, Node("literal", text="true"))))
                run(marker + "-" + suffix, change(rows, row, disjunction, weakened), "E08")
        run(marker + "-drop-retained-existential",
            change(rows, row, disjunction, replace(disjunction, args=(Node("literal", text="true"), candidate))), "E08")
    guarded = next((d for d in defs if "Encoding82Guarded" in contexts(d.statement)), None)
    if guarded:
        ancestor = next((n for n, child in reversed(guarded.ancestors) if n.op == "==>" and child == 1 and contains(n.args[0], lambda c: c.op == "!=")), None)
        if ancestor:
            run("move-definition-above-source-path", change(rows, guarded.statement, ancestor, ancestor.args[1]), "E03")
        else:
            results.append({"name": "move-definition-above-source-path", "passed": False, "reason": "guard target absent"})
    own = next((d for d in defs if "Encoding82EmptyOwn" in contexts(d.statement)), None)
    if own:
        enclosing = next((n for n, _ in reversed(own.ancestors) if n.op == "forall" and any(name == own.element.text for name, _ in n.bound)), None)
        if enclosing:
            hidden = Node("forall", (Node("==>", (Node("literal", text="false"), enclosing)),), bound=(("mutatedOwnWitness", "int"),))
            run("put-definition-under-empty-own-witness", change(rows, own.statement, enclosing, hidden), "E03")
    outer = next((d for d in defs if "Encoding82EmptyQuantifier" in contexts(d.statement)), None)
    if outer:
        ancestor = next((n for n, child in reversed(outer.ancestors) if n.op == "==>" and child == 1
                         and contains(n.args[0], lambda c: c.op == "literal" and c.text == "false")), None)
        if ancestor:
            run("move-definition-above-empty-outer-domain", change(rows, outer.statement, ancestor, ancestor.args[1]), "E03")
    allocation = next((d for d in defs if "Encoding82Lambda" in contexts(d.statement) and not closed_false_characteristic(d.characteristic, rows)), None)
    if allocation:
        target = next((c for g in guards(allocation.ancestors) for c in calls(g, "$IsAllocBox")), None)
        if target:
            run("drop-reference-lambda-allocation-guard", change(rows, allocation.statement, target, Node("literal", text="true")), "E04")
    if defs:
        d = defs[0]
        selection = d.equation.args[1] if d.equation.args[0] is d.member else d.equation.args[0]
        altered = replace(selection, args=(Node("id", text="mutatedCharacteristicCapture"), selection.args[1]))
        run("change-definition-characteristic-map", change(rows, d.statement, selection, altered), "E02")
    choice = next(((row, n) for row in rows for n, _ in walk(row.expr)
                   if n.op == "select" and len(n.args) > 2 and all(projection(p) for p in n.args[1:])), None)
    if choice:
        row, selected = choice
        component = selected.args[2]
        relation = component.args[0]
        template = relation_template(relation)
        changed_relation = replace_node(relation, template.args[0], Node("literal", text="true"))
        changed_component = replace(component, args=(changed_relation,))
        run("split-coordinated-witness-relations", change(rows, row, component, changed_component), "E08")
    if defs:
        d = defs[0]
        quantifier = next((n for n, _ in reversed(d.ancestors) if n.op == "forall" and any(name == d.element.text for name, _ in n.bound)), None)
        if quantifier:
            stale = replace(quantifier, triggers=((d.member.args[0],),))
            run("drop-membership-trigger-binder", change(rows, d.statement, quantifier, stale), "E10")
    return results


class ParserTests(unittest.TestCase):
    def test_alpha_preserves_captures(self):
        a = parse(lex("(lambda x: int :: x == n)"))
        b = parse(lex("(lambda y: int :: y == n)"))
        c = parse(lex("(lambda y: int :: y == m)"))
        self.assertEqual(alpha(a), alpha(b))
        self.assertNotEqual(alpha(a), alpha(c))

    def test_complete_witness_instances_preserve_all_guards(self):
        relation = parse(lex("(lambda x: int, y: bool :: $Is(x, TInt) && 0 <= x && $IsAllocBox($Box(x), TInt, heap) && Range(x, y, n) && key == $Box(Tuple($Box(x), $Box(y))))"))
        values = {"x": parse(lex("$Unbox(Field1($Unbox(key): DatatypeType)): int")),
                  "y": parse(lex("$Unbox(Field0($Unbox(key): DatatypeType)): bool"))}
        candidate = expand_lets(relation.args[0], values)
        self.assertTrue(full_witness_body_instance(relation, candidate))
        original = Node("exists", relation.args, bound=relation.bound)
        guard = Node("||", (original, candidate))
        ancestors = ((Node("==>", (guard, Node("literal", text="true"))), 1),)
        self.assertTrue(inhabited_source_guard(relation, ancestors))
        for conjunct in conjunctions(candidate):
            weakened = replace_node(candidate, conjunct, Node("literal", text="true"))
            self.assertFalse(full_witness_body_instance(relation, weakened))
            bad_guard = replace(guard, args=(original, weakened))
            self.assertFalse(inhabited_source_guard(relation, ((Node("==>", (bad_guard, Node("literal", text="true"))), 1),)))
        wrong_sort = expand_lets(relation.args[0], {**values, "x": parse(lex("$Unbox(Field1($Unbox(key): DatatypeType)): bool"))})
        self.assertFalse(full_witness_body_instance(relation, wrong_sort))
        wrong_original = replace(original, args=(Node("literal", text="true"),))
        self.assertFalse(inhabited_source_guard(relation, ((Node("==>", (replace(guard, args=(wrong_original, candidate)), Node("literal", text="true"))), 1),)))

    def test_witness_instance_rejects_inconsistent_and_residual_witnesses(self):
        relation = parse(lex("(lambda x: int :: F(x, x))"))
        self.assertTrue(full_witness_body_instance(relation, parse(lex("F(1, 1)"))))
        self.assertFalse(full_witness_body_instance(relation, parse(lex("F(1, 2)"))))
        self.assertFalse(full_witness_body_instance(relation, parse(lex("F(x, x)"))))

    def test_witness_instance_rejects_nested_capture(self):
        relation = parse(lex("(lambda x: int :: (forall z: int :: F(x, z, n)))"))
        self.assertTrue(full_witness_body_instance(relation, parse(lex("(forall w: int :: F(1, w, n))"))))
        self.assertFalse(full_witness_body_instance(relation, parse(lex("(forall z: int :: F(z, z, n))"))))
        self.assertFalse(full_witness_body_instance(relation, parse(lex("(forall n: int :: F(1, n, n))"))))
        shadowed = parse(lex("(lambda x: int :: (forall z: int :: (forall w: int :: F(x, z, w))))"))
        self.assertFalse(full_witness_body_instance(shadowed, parse(lex("(forall a: int :: (forall a: int :: F(1, a, a)))"))))

    def test_witness_instance_respects_simultaneous_let_and_patterns(self):
        relation = parse(lex("(lambda x: int :: (var z: int := n; F(z, x)))"))
        self.assertTrue(full_witness_body_instance(relation, parse(lex("(var n: int := n; F(n, 1))"))))
        quantified = parse(lex("(lambda x: int :: (forall z: int :: {F(x, z)} F(x, z)))"))
        self.assertTrue(full_witness_body_instance(quantified, parse(lex("(forall w: int :: {F(1, w)} F(1, w))"))))
        self.assertFalse(full_witness_body_instance(quantified, parse(lex("(forall w: int :: {F(2, w)} F(1, w))"))))

    def test_quantifier_scope_and_trigger(self):
        q = parse(lex("(forall b: Box :: {:weight 3} {M(C(n), b)} n != 0 ==> M(C(n), b))"))
        self.assertEqual(q.op, "forall")
        self.assertEqual(q.args[0].op, "==>")
        self.assertEqual(q.triggers[0][0].text, "M")
        self.assertEqual(free(q), {"n"})

    def test_map_selection_and_tuple_relation(self):
        q = parse(lex("(lambda x: int, y: bool :: x == n && y == b)[p(r), q(r)]"))
        self.assertEqual(q.op, "select")
        self.assertEqual(q.args[0].bound, (("x", "int"), ("y", "bool")))
        self.assertEqual(len(q.args), 3)

    def test_let_alias_and_coercion(self):
        a = parse(lex("(var m: [Box]bool := (lambda x: Box :: $Unbox(x): int == n); (forall b: Box :: {M(m, b)} m[b]))"))
        self.assertEqual(a.op, "let")
        resolved = expand_lets(a)
        self.assertEqual(resolved.args[0].args[0].op, "lambda")
        self.assertEqual(free(resolved), {"n"})

    def test_projection_unnamed_parameters(self):
        self.assertEqual(signatures("function map$project#0([int,Box]bool): int;"),
                         {"map$project#0": (("[int,Box]bool",), "int")})

    def test_renamed_reference_bridge_is_structural(self):
        expr = "axiom (forall s: [ref]bool, b: Box :: {Set#IsMember(Renamed(s), b)} Set#IsMember(Renamed(s), b) <==> s[$Unbox(b): ref]);"
        errors, _ = audit(statements(expr), {}, require_probe=False)
        self.assertIn("E01", {e["code"] for e in errors})

    def test_smt_set_valued_reads_lookup_is_not_a_bridge(self):
        text = """(assert (forall ((rd Map) (h Heap) (arg Box) (b Box))
          (! (= (Set#IsMember (Reads (Handle rd) h arg) b)
                (Set#IsMember (MapType1Select HeapType BoxType SetType rd h arg) b))
             :pattern ((Reads (Handle rd) h arg) (Set#IsMember (MapType1Select HeapType BoxType SetType rd h arg) b)))))
          (check-sat)"""
        errors, _ = audit_smt(text)
        self.assertNotIn("E01", {e["code"] for e in errors})

    def test_smt_boolean_conversion_wrapper_is_a_bridge(self):
        text = """(assert (forall ((rd Map) (b Box))
          (! (= (Set#IsMember (RenamedReferenceWrapper rd) b)
                (U_2_bool (MapSelect rd (Unbox b))))
             :pattern ((Set#IsMember (RenamedReferenceWrapper rd) b)))))
          (check-sat)"""
        errors, _ = audit_smt(text)
        self.assertIn("E01", {e["code"] for e in errors})

    def test_equality_guarded_finite_alias_has_only_the_fixed_source_instance(self):
        text = "axiom (forall m: [Box]bool :: {Set#FromBoogieMap(m)} m == (lambda x: Box :: false) ==> (forall b: Box :: {Set#IsMember(Set#FromBoogieMap(m), b)} Set#IsMember(Set#FromBoogieMap(m), b) <==> m[b]));"
        rows = statements(text)
        errors, _ = audit(rows, {}, require_probe=False)
        self.assertNotIn("E01", {e["code"] for e in errors})
        quantifier = rows[0].expr
        guard = fixed_map_aliases(quantifier)["m"][1]
        dropped = change(rows, rows[0], guard, Node("literal", text="true"))
        errors, _ = audit(dropped, {}, require_probe=False)
        self.assertIn("E01", {e["code"] for e in errors})

    def test_witness_equality_elimination_retains_subset_and_allocation(self):
        relation = parse(lex("(lambda x: int :: x == v && 0 <= x && $IsAllocBox($Box(x), TInt, heap) && key == $Box(x))"))
        guard = parse(lex("0 <= v && $IsAllocBox($Box(v), TInt, heap) && key == $Box(v)"))
        ancestors = ((Node("==>", (guard, Node("literal", text="true"))), 1),)
        self.assertTrue(inhabited_source_guard(relation, ancestors))
        dropped = parse(lex("0 <= v && key == $Box(v)"))
        self.assertFalse(inhabited_source_guard(relation, ((Node("==>", (dropped, Node("literal", text="true"))), 1),)))
        self_reference = parse(lex("(exists x: int :: x == x + 1 && key == $Box(x))"))
        self.assertEqual(alpha(simplify_witness_exists(self_reference)), alpha(self_reference))
        captured = parse(lex("(exists x: int :: x == v && F((lambda x: int :: x + v), x))"))
        expected = parse(lex("F((lambda x: int :: x + v), v)"))
        self.assertEqual(alpha(simplify_witness_exists(captured)), alpha(expected))

    def test_arrow_family_atlayer_requires_actual_closure_guard(self):
        text = "axiom (forall f: [LayerType]HandleType, l: LayerType, h: Heap, b: Box :: {Reads1(TInt, TInt, h, AtLayer(f, l), b)} f == (lambda layer: LayerType :: Handle(layer)) ==> Reads1(TInt, TInt, h, AtLayer(f, l), b) == Empty());"
        rows = statements(text)
        errors, _ = audit(rows, {}, require_probe=False)
        self.assertNotIn("E08", {e["code"] for e in errors})
        guard = fixed_map_aliases(rows[0].expr)["f"][1]
        errors, _ = audit(change(rows, rows[0], guard, Node("literal", text="true")), {}, require_probe=False)
        self.assertIn("E08", {e["code"] for e in errors})

    def test_implementation_scope_preserves_local_capture_identity(self):
        text = """implementation First() {
          assume (forall m: ISet :: {Set#FromBoogieMap(m)} m == (lambda b: Box :: b == local)
            ==> (forall x: Box :: {Set#IsMember(Set#FromBoogieMap(m), x)} Set#IsMember(Set#FromBoogieMap(m), x) <==> m[x]));
          assume ResultEncoding82Conditional() == Set#FromBoogieMap((lambda b: Box :: b == local));
        }
        implementation Second() { assume Set#FromBoogieMap((lambda b: Box :: b == local)) == Other(); }
        """
        rows = statements(text)
        self.assertEqual([row.scope for row in rows], ["First", "First", "Second"])
        errors, _ = audit(rows, {}, require_probe=False)
        self.assertNotIn("E02", {e["code"] for e in errors})
        changed = change(rows, rows[1], calls(rows[1].expr, CONVERSION)[0].args[0].args[0].args[1], Node("id", text="changedCapture"))
        errors, _ = audit(changed, {}, require_probe=False)
        self.assertIn("E02", {e["code"] for e in errors})

    def test_named_iset_backend_alias_requires_its_fixed_map_equality(self):
        text = "axiom (forall m: ISet :: {Set#FromBoogieMap(m)} m == (lambda x: Box :: false) ==> (forall b: Box :: {Set#IsMember(Set#FromBoogieMap(m), b)} Set#IsMember(Set#FromBoogieMap(m), b) <==> m[b]));"
        errors, _ = audit(statements(text), {}, require_probe=False)
        self.assertNotIn("E01", {e["code"] for e in errors})
        unguarded = text.replace("m == (lambda x: Box :: false) ==> ", "")
        errors, _ = audit(statements(unguarded), {}, require_probe=False)
        self.assertIn("E01", {e["code"] for e in errors})

    def test_alias_equal_to_arbitrary_map_is_still_rejected(self):
        text = "axiom (forall arbitrary: [Box]bool, m: [Box]bool, b: Box :: {Set#IsMember(Set#FromBoogieMap(m), b)} m == arbitrary ==> (Set#IsMember(Set#FromBoogieMap(m), b) <==> m[b]));"
        errors, _ = audit(statements(text), {}, require_probe=False)
        self.assertIn("E01", {e["code"] for e in errors})

    def test_smt_fixed_alias_and_guard_drop(self):
        text = """(assert (forall ((m Map)) (!
          (=> (= m (lambda#12 false))
            (forall ((b Box)) (! (= (Set#IsMember (Set#FromBoogieMap m) b) (MapSelect m b))
                                :pattern ((Set#IsMember (Set#FromBoogieMap m) b)))))
          :pattern ((Set#FromBoogieMap m))))) (check-sat)"""
        errors, _ = audit_smt(text)
        self.assertNotIn("E01", {e["code"] for e in errors})
        errors, _ = audit_smt(text.replace("(= m (lambda#12 false))", "true"))
        self.assertIn("E01", {e["code"] for e in errors})

    def test_bridge_is_structural(self):
        expr = "axiom (forall arbitrary: [Box]bool, b: Box :: {Set#IsMember(Set#FromBoogieMap(arbitrary), b)} Set#IsMember(Set#FromBoogieMap(arbitrary), b) <==> arbitrary[b]);"
        errors, _ = audit(statements(expr), {}, require_probe=False)
        self.assertIn("E01", {e["code"] for e in errors})


def smt_forms(text):
    tokens = re.findall(r';[^\n]*|"(?:""|[^"\\]|\\.)*"|\|[^|]*\||[()]|[^\s()]+', text)
    stack = []
    result = []
    for token in tokens:
        if token.startswith(";"):
            continue
        if token == "(":
            stack.append([])
        elif token == ")":
            if not stack:
                raise ValueError("unexpected SMT closing delimiter")
            form = stack.pop()
            (stack[-1] if stack else result).append(form)
        elif stack:
            stack[-1].append(token)
        else:
            result.append(token)
    if stack:
        raise ValueError("unclosed SMT expression")
    return result


def smt_walk(form):
    yield form
    if isinstance(form, list):
        for child in form:
            yield from smt_walk(child)


def smt_variables(form, candidates):
    if isinstance(form, str):
        return {form} & candidates
    if not form:
        return set()
    if form[0] in ("forall", "exists") and len(form) == 3:
        return smt_variables(form[2], candidates - {p[0] for p in form[1]})
    if form[0] == "let" and len(form) == 3:
        return set().union(*(smt_variables(p[1], candidates) for p in form[1]),
                           smt_variables(form[2], candidates - {p[0] for p in form[1]}))
    return set().union(*(smt_variables(child, candidates) for child in form))


def smt_conjunctions(form):
    return sum((smt_conjunctions(c) for c in form[1:]), []) if isinstance(form, list) and form and form[0] == "and" else [form]


def smt_fixed_aliases(quantifier):
    if not isinstance(quantifier, list) or len(quantifier) != 3 or quantifier[0] != "forall":
        return {}
    own = {p[0] for p in quantifier[1]}
    body = quantifier[2]
    if isinstance(body, list) and body and body[0] == "!":
        body = body[1]
    if not isinstance(body, list) or len(body) != 3 or body[0] != "=>":
        return {}
    result = {}
    for guard in smt_conjunctions(body[1]):
        if not isinstance(guard, list) or len(guard) != 3 or guard[0] != "=":
            continue
        for alias, actual in ((guard[1], guard[2]), (guard[2], guard[1])):
            if not isinstance(alias, str) or alias not in own:
                continue
            factory = actual[0] if isinstance(actual, list) and actual else actual
            if not isinstance(factory, str) or not factory.strip("|").startswith("lambda#"):
                continue
            if alias in smt_variables(actual, {alias}):
                continue
            result[alias] = (actual, guard)
    return result


def audit_smt(text):
    findings = []
    patterns = 0
    quantifiers = 0
    queries = 0
    named_conversions = 0
    forbidden = {"forall", "exists", "let", "and", "or", "not", "=>", "=", "distinct", "ite", "<", ">", "<=", ">="}
    def resolve(form, aliases):
        if isinstance(form, str):
            return aliases.get(form, form)
        return [resolve(child, aliases) for child in form]
    def characteristic_selections(form):
        # Membership in a Set-valued lookup is already finite-set membership.
        # Its nested lookup is not a Boolean characteristic-map reinterpretation.
        # In particular, the arrow reads axiom compares two such memberships.
        if isinstance(form, list) and form and isinstance(form[0], str) and form[0].strip("|") == MEMBER:
            return
        yield form
        if isinstance(form, list):
            for child in form:
                yield from characteristic_selections(child)
    def boundary(form, bound=frozenset(), aliases=None):
        nonlocal named_conversions
        aliases = {} if aliases is None else aliases
        if not isinstance(form, list) or not form or not isinstance(form[0], str):
            return
        name = form[0].strip("|")
        if name in ("forall", "exists") and len(form) == 3:
            shadow = {p[0] for p in form[1]}
            inside = {k: v for k, v in aliases.items() if k not in shadow}
            inside.update({name: resolve(actual, inside) for name, (actual, _) in smt_fixed_aliases(form).items()})
            boundary(form[2], bound | shadow, inside)
            return
        if name == "let" and len(form) == 3:
            expanded = dict(aliases)
            for parameter, value in form[1]:
                boundary(value, bound, aliases)
                expanded[parameter] = resolve(value, aliases)
            boundary(form[2], bound, expanded)
            return
        if name == CONVERSION:
            named_conversions += 1
            if len(form) == 2:
                argument = resolve(form[1], aliases)
                if isinstance(argument, str) and argument in bound:
                    findings.append({"code": "E01", "message": "actual SMT converts an arbitrary quantified backend map"})
        if name == "SetRef_to_SetBox":
            findings.append({"code": "E01", "message": "actual SMT retains reference-map preservation"})
        if name == "=" and len(form) == 3:
            for left, right in ((form[1], form[2]), (form[2], form[1])):
                if not isinstance(left, list) or len(left) != 3 or left[0].strip("|") != MEMBER:
                    continue
                converted = resolve(left[1], aliases)
                element = resolve(left[2], aliases)
                for selected in characteristic_selections(resolve(right, aliases)):
                    if not isinstance(selected, list) or len(selected) < 3 or not isinstance(selected[0], str):
                        continue
                    selected_map = selected[-2]
                    if ("select" in selected[0].lower() and isinstance(selected_map, str) and selected_map in bound
                            and selected_map != element and selected_map in smt_variables(converted, set(bound))):
                        findings.append({"code": "E01", "message": "actual SMT preserves arbitrary-map membership through a conversion wrapper"})
        for child in form[1:]:
            boundary(child, bound, aliases)
    for form in smt_forms(text):
        boundary(form)
        for node in smt_walk(form):
            if not isinstance(node, list) or not node:
                continue
            if node[0] in ("check-sat", "check-sat-assuming"):
                queries += 1
            if node[0] != "forall" or len(node) != 3:
                continue
            quantifiers += 1
            own = {p[0] for p in node[1]}
            annotated = node[2]
            if not isinstance(annotated, list) or not annotated or annotated[0] != "!":
                continue
            used = smt_variables(annotated[1], own)
            attributes = annotated[2:]
            for i in range(0, len(attributes) - 1, 2):
                if attributes[i] != ":pattern":
                    continue
                patterns += 1
                pattern = attributes[i + 1]
                covered = smt_variables(pattern, own)
                if not used <= covered:
                    findings.append({"code": "E10", "message": "actual SMT pattern does not cover all used quantified variables"})
                if (not isinstance(pattern, list) or any(not isinstance(term, list) or not term or
                                                       (isinstance(term[0], str) and term[0] in forbidden) for term in pattern)
                        or any(isinstance(term, list) and term and isinstance(term[0], str) and term[0] in forbidden
                               for term in smt_walk(pattern))):
                    findings.append({"code": "E10", "message": "actual SMT pattern contains an invalid Boolean or binder expression"})
    if not queries:
        findings.append({"code": "input", "message": "SMT artifact has no actual solver query"})
    if not patterns:
        findings.append({"code": "input", "message": "SMT artifact has no quantified trigger evidence"})
    return list({(f["code"], f["message"]): f for f in findings}.values()), {
        "actual_solver_queries": queries, "quantifiers": quantifiers, "trigger_patterns": patterns,
        "named_finite_conversions": named_conversions}


def smt_mutations(text):
    forms = smt_forms(text)
    chosen = next((node for form in forms for node in smt_walk(form)
                   if isinstance(node, list) and len(node) == 3 and node[0] == "forall"
                   and isinstance(node[2], list) and node[2] and node[2][0] == "!"
                   and ":pattern" in node[2]), None)
    if chosen is None:
        return [{"name": "actual-smt-trigger-mutations", "passed": False, "reason": "actual SMT pattern absent"}]
    annotated = chosen[2]
    index = annotated.index(":pattern") + 1
    own = {p[0] for p in chosen[1]}
    def alter(form, old, new):
        if form is old:
            return new
        return [alter(child, old, new) for child in form] if isinstance(form, list) else form
    def render(form):
        return "(" + " ".join(render(c) for c in form) + ")" if isinstance(form, list) else form
    def drop(form):
        if isinstance(form, str):
            return "mutatedUnboundCapture" if form in own else form
        return [drop(child) for child in form]
    output = []
    for name, new in (("drop-actual-smt-pattern-bound-variable", drop(annotated[index])),
                      ("put-boolean-equality-in-actual-smt-pattern", [["=", next(iter(own)), next(iter(own))]])):
        mutated = "\n".join(render(alter(form, annotated[index], new)) for form in forms)
        errors, _ = audit_smt(mutated)
        codes = sorted({e["code"] for e in errors})
        output.append({"name": name, "expected": "E10", "detected": codes, "passed": "E10" in codes})
    fixed = next(((alias, guard) for form in forms for quantifier in smt_walk(form)
                  for alias, (_, guard) in smt_fixed_aliases(quantifier).items()
                  if any(isinstance(node, list) and len(node) == 2 and isinstance(node[0], str)
                         and node[0].strip("|") == CONVERSION and node[1] == alias
                         for node in smt_walk(quantifier[2]))), None)
    if fixed:
        mutated = "\n".join(render(alter(form, fixed[1], "true")) for form in forms)
        errors, _ = audit_smt(mutated)
        codes = sorted({e["code"] for e in errors})
        output.append({"name": "drop-actual-smt-characteristic-alias-equality", "expected": "E01", "detected": codes, "passed": "E01" in codes})
    if audit_smt(text)[1]["named_finite_conversions"]:
        for name, bridge in (
            ("restore-actual-smt-universal-bridge", "(assert (forall ((m Map) (b Box)) (= (Set#IsMember (Set#FromBoogieMap m) b) (MapSelect m b))))"),
            ("restore-actual-smt-renamed-reference-bridge", "(assert (forall ((references Map) (b Box)) (= (Set#IsMember (RenamedReferenceWrapper references) b) (MapSelect references (Unbox b)))))"),
        ):
            errors, _ = audit_smt(text + "\n" + bridge)
            codes = sorted({e["code"] for e in errors})
            output.append({"name": name, "expected": "E01", "detected": codes, "passed": "E01" in codes})
    return output


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--boogie", type=Path, help="pre-lifting --bprint output of the companion probe")
    p.add_argument("--lifted", type=Path, help="post-lifting /printLambdaLifting output of the same probe")
    p.add_argument("--output", type=Path, help="write the JSON report here")
    p.add_argument("--smt", type=Path, action="append", default=[], help="actual solver query log (repeatable)")
    p.add_argument("--self-test", action="store_true")
    a = p.parse_args()
    if a.self_test:
        suite = unittest.defaultTestLoader.loadTestsFromTestCase(ParserTests)
        return 0 if unittest.TextTestRunner().run(suite).wasSuccessful() else 1
    if not a.boogie or not a.lifted or not a.smt:
        p.error("--boogie, --lifted and at least one --smt are required; missing pipeline evidence is not a pass")
    report = {"scope": "finite-support translation fragment; no solver or whole-prelude consistency claim", "inputs": {}, "checks": {}, "mutations": []}
    failed = False
    try:
        raw = a.boogie.read_text()
        lifted = a.lifted.read_text()
        for label, path, content in (("raw", a.boogie, raw), ("lifted", a.lifted, lifted)):
            report["inputs"][label] = {"file": path.name, "sha256": hashlib.sha256(content.encode()).hexdigest()}
        raw_rows = statements(raw)
        errors, evidence = audit(raw_rows, signatures(raw))
        report["checks"]["raw"] = {"failures": errors, "evidence": evidence}
        failed |= bool(errors)
        lifted_errors, lifted_evidence = audit(statements(lifted), signatures(lifted), lifted=True)
        report["checks"]["lifted"] = {"failures": lifted_errors, "evidence": lifted_evidence}
        failed |= bool(lifted_errors)
        report["checks"]["smt"] = []
        for path in a.smt:
            content = path.read_text()
            smt_errors, smt_evidence = audit_smt(content)
            report["checks"]["smt"].append({"file": path.name, "sha256": hashlib.sha256(content.encode()).hexdigest(),
                                           "failures": smt_errors, "evidence": smt_evidence})
            failed |= bool(smt_errors)
            if not smt_errors:
                report["mutations"].extend(smt_mutations(content))
        if not any(check["evidence"]["named_finite_conversions"] for check in report["checks"]["smt"]):
            report["checks"]["smt"].append({"failures": [{"code": "input", "message": "supply a named actual query (--boogie /normalizeNames:0) for the E01 boundary audit"}]})
            failed = True
        if not errors:
            report["mutations"].extend(mutations(raw_rows, signatures(raw)))
            failed |= not report["mutations"] or any(not m["passed"] for m in report["mutations"])
    except (ValueError, OSError, IndexError) as exc:
        report["input_error"] = str(exc)
        failed = True
    report["passed"] = not failed
    rendered = json.dumps(report, indent=2)
    if a.output:
        a.output.parent.mkdir(parents=True, exist_ok=True)
        a.output.write_text(rendered + "\n")
    print(rendered)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
