dotnet publish bindings/NativeRKNNC -c Release -r $(python - <<'PY'
import platform, sys
sysname=platform.system(); arch=platform.machine().lower()
print({"Windows":"win-x64","Linux":"linux-x64","Darwin":"osx-arm64" if arch in ("arm64","aarch64") else "osx-x64"}[sysname])
PY
)
python -m build
pip install --force-reinstall dist/rknnc-*.whl
python - <<'PY'
import numpy as np
from rknnc import RKNNC

points = np.array([[0.0, 0.0], [0.1, 0.0], [10.0, 10.0], [10.1, 10.0]])
labels = RKNNC(k=1, min_pts=2).fit_predict(points)
assert labels[0] == labels[1] and labels[2] == labels[3] and labels[0] != labels[2]
assert np.all(labels >= 0)
print("rknnc wheel smoke test passed")
PY
