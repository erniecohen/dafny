"""Inspect actual M2 equations against M1 bodies; check finite definition slices.

This is deliberately a small parser, not a general Boogie-to-SMT translator.
Unsupported syntax fails closed. SAT is a finite integer-only slice claim, not
consistency of the full erased prelude or the negated verification condition.
"""
import functools
from pathlib import Path
import re
import subprocess
import sys

TOKEN = re.compile(r'\s*(==|&&|\|\||<=|>=|:=|[A-Za-z_][\w.#]*|\d+|[()%,;+*/!<>-])')
PREFIX = '_System.IntegerBitwiseModel.'


class Parser:
    precedence = {'||': 1, '&&': 2, '==': 3, '<': 3, '<=': 3, '>': 3, '>=': 3,
                  '+': 4, '-': 4, '*': 5, '/': 5, '%': 5}

    def __init__(self, text):
        self.tokens = []
        while text.strip():
            match = TOKEN.match(text)
            assert match, ('unsupported syntax', text[:100])
            self.tokens.append(match[1])
            text = text[match.end():]
        self.tokens.append('<end>')
        self.at = 0
        self.variables = {}

    def take(self, expected=None):
        token = self.tokens[self.at]
        assert expected is None or token == expected, (expected, token, self.tokens)
        self.at += 1
        return token

    def expr(self, minimum=0):
        token = self.take()
        if token == 'if':
            test = self.expr()
            self.take('then')
            yes = self.expr()
            self.take('else')
            left = ('if', test, yes, self.expr())
        elif token == 'var':
            name = self.take()
            self.take(':=')
            self.variables[name] = self.expr()
            self.take(';')
            left = self.expr()
        elif token == '(':
            left = self.expr()
            self.take(')')
        elif token in ('-', '!'):
            left = ('neg' if token == '-' else 'not', self.expr(6))
        elif token.isdigit():
            left = ('number', int(token))
        elif self.tokens[self.at] == '(':
            self.take('(')
            arguments = [self.expr()]
            while self.tokens[self.at] == ',':
                self.take(',')
                arguments.append(self.expr())
            self.take(')')
            name = token.removeprefix(PREFIX)
            if name == 'LitInt':
                assert len(arguments) == 1
                left = arguments[0]
            elif name in ('Mul', 'Div', 'Mod'):
                assert len(arguments) == 2
                left = ({'Mul': '*', 'Div': '/', 'Mod': '%'}[name], *arguments)
            else:
                left = ('call', name, tuple(arguments))
        else:
            left = self.variables.get(token, ('var', token.split('#')[0]))
        while self.tokens[self.at] in self.precedence and self.precedence[self.tokens[self.at]] >= minimum:
            operator = self.take()
            left = (operator, left, self.expr(self.precedence[operator] + 1))
        return left

    def parse(self):
        result = self.expr()
        self.take('<end>')
        return result


def source_bodies(path):
    text = path.read_text()
    result = {}
    for match in re.finditer(r'ghost function\s+(\w+)\(([^)]*)\)', text):
        name, parameters = match.groups()
        if name in ('bitsNat', 'value', 'bit', 'count', 'flip', 'zipAnd'):
            continue  # ordinary sequence/view functions, not trusted references
        start = text.index('{', match.end())
        depth, end = 1, start + 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        result[name] = (re.findall(r'(\w+)\s*:', parameters), Parser(text[start + 1:end - 1]).parse())
    return result


def emitted_equations(path):
    result = {}
    for block in re.split(r'(?=^procedure )', path.read_text(), flags=re.M):
        match = re.search(r'^procedure [^\n]*Call\$\$_System\.IntegerBitwiseModel\.(unfold\w+)\(', block)
        if not match:
            continue
        equations = re.findall(r'^  ensures (?:\{:[^}]*\}\s*)?([^;]+);', block, re.M)
        assert len(equations) == 1, (match[1], equations)
        expression = Parser(equations[0]).parse()
        assert expression[0] == '==' and expression[1][0] == 'call'
        name = expression[1][1]
        assert name not in result
        guards = re.findall(r'^  requires (?:\{:[^}]*\}\s*)?([^;]+);', block, re.M)
        result[name] = (expression, [Parser(guard).parse() for guard in guards])
    return result


def evaluate(expression, environment, model, record=None):
    operation, *arguments = expression
    sub = lambda value: evaluate(value, environment, model, record)
    if operation == 'number':
        return arguments[0]
    if operation == 'var':
        return environment[arguments[0]]
    if operation == 'if':
        return sub(arguments[1] if sub(arguments[0]) else arguments[2])
    if operation == '&&':
        return sub(arguments[0]) and sub(arguments[1])
    if operation == '||':
        return sub(arguments[0]) or sub(arguments[1])
    if operation == 'call':
        value = model(arguments[0], tuple(sub(arg) for arg in arguments[1]))
        if record is not None:
            record[expression] = value
        return value
    if operation == 'neg':
        return -sub(arguments[0])
    if operation == 'not':
        return not sub(arguments[0])
    a, b = map(sub, arguments)
    if operation in ('/', '%'):
        assert b > 0, ('undefined source arithmetic', expression, environment)
    return {'+': lambda: a+b, '-': lambda: a-b, '*': lambda: a*b,
            '/': lambda: a//b, '%': lambda: a%b, '==': lambda: a == b,
            '<': lambda: a < b, '<=': lambda: a <= b,
            '>': lambda: a > b, '>=': lambda: a >= b}[operation]()


def smt(expression, environment):
    operation, *arguments = expression
    sub = lambda value: smt(value, environment)
    if operation == 'number':
        return str(arguments[0]) if arguments[0] >= 0 else f'(- {-arguments[0]})'
    if operation == 'var':
        return smt(('number', environment[arguments[0]]), environment)
    if operation == 'call':
        return '(' + arguments[0] + ' ' + ' '.join(map(sub, arguments[1])) + ')'
    operators = {'if': 'ite', '&&': 'and', '||': 'or', '==': '=',
                 '/': 'div', '%': 'mod', 'neg': '-'}
    return '(' + operators.get(operation, operation) + ' ' + ' '.join(map(sub, arguments)) + ')'


def check(boogie, source, solver, directory):
    bodies, equations = source_bodies(source), emitted_equations(boogie)
    assert set(bodies) == set(equations) and len(bodies) == 12
    # The smoke input takes no function values, so even handle-application
    # axioms have no reason to mention a raw UF here. In particular, there is
    # no globally available recurrence or assumed consumer range contract.
    emitted = boogie.read_text()
    reference_call = re.compile(re.escape(PREFIX) + '(?:' + '|'.join(bodies) + r')\(')
    for axiom in re.findall(r'^axiom\b.*?;', emitted, re.M | re.S):
        assert not reference_call.search(axiom), ('global reference fact', axiom)
    for name in bodies:
        assert re.search(r'^function ' + re.escape(PREFIX + name) + r'\([^;]*\) : int;', emitted, re.M), name
    for name, (parameters, body) in bodies.items():
        equation = equations[name][0]
        assert equation[1] == ('call', name, tuple(('var', parameter) for parameter in parameters)), name
        assert equation[2] == body, ('emission does not match M1', name, equation[2], body)

    @functools.lru_cache(None)
    def model(name, values):
        parameters, body = bodies[name]
        assert values[0] >= 0
        return evaluate(body, dict(zip(parameters, values)), model)

    declarations = '\n'.join(f'(declare-fun {name} ({" ".join("Int" for _ in parameters)}) Int)'
                             for name, (parameters, _) in bodies.items())
    checks, mutations = 0, 0
    for width, a, b, count in [(0, -1, 0, -1), (1, -1, 1, 1), (3, 5, -3, 0),
                               (8, -1, 3, 8), (16, 5, 3, -1), (32, 5, -3, 33),
                               (64, -1, 3, 64), (12, 16, 3, 8)]:
        environment = dict(n=width, a=a, b=b, k=count)
        values = {}
        active = []
        for equation, guards in equations.values():
            assert all(evaluate(guard, environment, model, values) for guard in guards)
            assert evaluate(equation, environment, model, values)
            active.append(equation)
        assignments = '\n'.join(f'(assert (= {smt(term, environment)} {smt(("number", value), environment)}))'
                                for term, value in values.items())
        prefix = '(set-logic QF_UFNIA)\n(set-option :rlimit 200000)\n' + declarations + '\n' + assignments + '\n'
        def query(expressions, tag, expected):
            nonlocal checks
            text = prefix + '\n'.join('(assert ' + smt(item, environment) + ')' for item in expressions)
            text += '\n(check-sat)\n'
            path = directory / f'slice-{width}-{tag}.smt2'
            path.write_text(text)
            result = subprocess.run([str(solver), '-smt2', str(path)], capture_output=True, text=True, timeout=60)
            assert result.returncode == 0 and result.stdout.strip() == expected and not result.stderr, (path.name, result)
            checks += 1
        query(active, 'model', 'sat')
        if width == 64:
            for index, name in enumerate(equations):
                mutant = list(active)
                _, left, right = mutant[index]
                mutant[index] = ('==', left, ('+', right, ('number', 1)))
                query(mutant, name + '-mutant', 'unsat')
                mutations += 1
    print(f'12 emitted equations match M1; {checks - mutations} finite slices SAT; {mutations} mutations UNSAT')


if __name__ == '__main__':
    boogie, source, solver, directory = map(Path, sys.argv[1:])
    directory.mkdir(parents=True, exist_ok=True)
    check(boogie, source, solver, directory)
