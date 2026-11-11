# Translation-table measurements

Build with the repository's .NET SDK and fixed Boogie packages:

```sh
dotnet build .github/measure/TranslationCache -c Release
```

The executable takes a source directory and `cold`, `warm`, or `edit`. For Std:

```sh
dotnet .github/measure/TranslationCache/bin/Release/net10.0/TranslationCache.dll Source/DafnyStandardLibraries/src/Std cold
```

Use `.github/measure/TranslationCache/inputs` for the small multi-module input.
Run each scenario in five fresh processes. The executable adds a separate
`Unrelated` module in temporary storage. Cold starts with an empty process-local
table. Warm and edit populate it before starting the timer; edit then changes
one function body in `Unrelated`. The measured interval resolves a fresh program
and translates every verifiable module with one resolution worker. Wall time and
process CPU include this entire interval. Cache population is excluded from the
warm and edit intervals.

This is a front-end benchmark. It disables the solver-version probe and requests
neither Boogie preparation nor verification, so solver calls are zero in every
scenario. It measures no solver-result savings. `translatedModules` reports which
requests actually invoked the translator; hits still copy and rebind their
Boogie graphs. Source enumeration excludes `TargetSpecific` implementations.
