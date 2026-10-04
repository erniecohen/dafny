# B3 source component

This is a source snapshot of the latest contribution to [dafny-lang/b3](https://github.com/dafny-lang/b3), pinned to the unmerged domains contribution at `ea6e8a18dfe9e317d313de769291f989957dc5f2`. The source manifest identifies each vendored path and its digest. The MIT license is retained in `LICENSE`.

`integration.patch` records the separable structured-library and process-transport changes relative to that exact upstream commit. The generated library lives in a separate worker process so its generated Dafny runtime does not share DafnyCore's runtime. Build it with `Scripts/build-b3-worker.sh` from the repository root.

The initial structured entrypoint rejects domains, custom literals, reach expressions, closures and signature sets. In particular, the newest upstream domain code remains present in this snapshot but is not advertised as usable; unfinished domain-instantiation paths must not silently remove obligations. The separate upstream soundness contribution is not merged into this executing dependency. See `doc/worker-library.md` for the runtime trust boundary and transport checks.
