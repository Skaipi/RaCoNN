# AGENTS.md

## Sources Of Truth
- Trust `README.md`, `pyproject.toml`, `MANIFEST.in`, `build.sh`, `build_local.sh`, and the `.csproj` files.
- `MapMinimizer.Tests/` contains the MSTest suite. No CI workflows or pre-commit config exist.

## Repo Shape
- `RKNNC/` is the clustering library. `RKNNC/RKNNC.cs` builds an undirected reciprocal-kNN graph, then runs `MapEquationOptimizer`.
- `MapMinimizer/` is the hierarchical map-equation optimizer. Its public entry point is `MapMinimizer/MapEquationOptimizer.cs`.
- `bindings/NativeRKNNC/` is the native AOT bridge. `bindings/NativeRKNNC/RKNNCExports.cs` exposes the C ABI entrypoint `cluster`.
- The Python package is published/imported as `rknnc`, but its source directory is `bindings/` because `pyproject.toml` maps `rknnc = "bindings"`.
- `bindings/__init__.py` exports `RKNNC` and `cluster`; `build_local.sh` smoke-tests the installed clustering estimator.
- `MapMinimizer/Flow/`, `NodeMoving/`, `Partitioning/`, and `Hierarchy/` separate flow construction, node moves, two-level search, and multilevel search. `docs/architecture.md` maps these components to the paper.

## Build And Packaging
- Prereqs from config/scripts: .NET SDK with `net10.0`, Python `>=3.9`, and the `build` module available for `python -m build`.
- Build all .NET projects with `dotnet build RKNNCluster.slnx`, or target a single `.csproj` when you only changed one layer.
- Build the native library with `./build.sh` or `dotnet publish bindings/NativeRKNNC -c Release -r <RID>`. `dotnet build` alone does not populate the packaged native artifacts.
- Packaging order matters: publish `bindings/NativeRKNNC` first, then run `python -m build`. The wheel/sdist only include files already present under `bindings/artifacts/native/**/*`.
- `./build_local.sh` is the full local packaging loop: native publish, `python -m build`, then `pip install --force-reinstall dist/rknnc-*.whl`.
- `build.sh` and `build_local.sh` publish only the host RID they derive internally. If you need a different RID, publish `bindings/NativeRKNNC` manually with `-r <RID>` first.

## Artifacts
- Do not hand-edit `bindings/artifacts/native/*`. `bindings/NativeRKNNC/NativeRKNNC.csproj` populates that tree in the `CopyNativeToArtifacts` target after `Publish`.
- `bindings/bindings.py` loads `artifacts/native/<rid>/NativeRKNNC.{dll,so,dylib}` via `ctypes`, so imports fail immediately when the matching RID artifact is missing.
- Native artifacts may be absent from a clean checkout. Publish the correct RID before testing packaging or imports.
- Ignore generated outputs in `bin/`, `obj/`, `dist/`, and `rknnc.egg-info/` unless your task is specifically about packaging/build artifacts.
