"""Pure source generators; these never invoke a compiler or solver."""

def nesting_fixture(arm, depth):
    names = "T0, T1"
    declaration_names = "T0(0), T1(0)"
    nested = "int"
    for _ in range(depth):
        nested = f"seq<{nested}>"
    lines = [f"datatype Pair<{declaration_names}> = P(first: T0, second: T1)"]
    if arm == "B":
        lines.append(f"type Value<{declaration_names}> = Pair<{names}>")
        encode, decode = "b", "v"
    elif arm == "N":
        lines.append(f"newtype Value<{declaration_names}> = Pair<{names}>")
        encode, decode = f"b as Value<{names}>", f"v as Pair<{names}>"
    else:
        lines.append(f"datatype Value<{declaration_names}> = Wrap(value: Pair<{names}>)")
        encode, decode = "Wrap(b)", "v.value"
    lines.append(f"lemma GenericRoundTrip<{declaration_names}>(b: Pair<{names}>) {{")
    for index in range(8):
        current_decode = f"v{index}" if arm == "B" else f"v{index} as Pair<{names}>" if arm == "N" else f"v{index}.value"
        lines.append(f"  var v{index}: Value<{names}> := {encode}; assert ({current_decode}) == b;")
    lines += ["}", f"lemma ConcreteWitness() {{ GenericRoundTrip<{nested}, int>(P([], 0)); }}"]
    return "\n".join(lines) + "\n"

def depth_fixture(arm, depth):
    lines = ["type Pair = (int, int)"]
    for index in range(depth):
        previous = "Pair" if index == 0 else f"Layer{index - 1}"
        if arm == "B":
            lines.append(f"type Layer{index} = {previous}")
        elif arm == "N":
            lines.append(f"newtype Layer{index} = {previous}")
        else:
            lines.append(f"datatype Layer{index} = Wrap{index}(value: {previous})")
    expr = "b"
    for index in range(depth):
        if arm != "B":
            expr = f"({expr} as Layer{index})" if arm == "N" else f"Wrap{index}({expr})"
    view = "v" if arm == "B" else "v as Pair" if arm == "N" else "v" + ".value" * depth
    lines += [f"lemma RoundTrip(b: Pair) {{ var v: Layer{depth - 1} := {expr}; assert ({view}) == b; }}",
              "lemma ConcreteWitness() { RoundTrip((0, 1)); }"]
    return "\n".join(lines) + "\n"
