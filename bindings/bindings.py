"""
ctypes bindings for a C# RKNNC implementation 
"""
import ctypes as ct
import platform
import sys
from pathlib import Path

import numpy as np
import numpy.typing as npt

def _get_runtime_id():
    sysname = platform.system()
    arch = platform.machine().lower()
    if sysname == "Windows":
        return "win-arm64" if "arm" in arch else "win-x64"
    if sysname == "Linux":
        return "linux-arm64" if arch in ("aarch64", "arm64") else "linux-x64"
    if sysname == "Darwin":
        return "osx-arm64" if arch in ("arm64", "aarch64") else "osx-x64"
    raise RuntimeError(f"Unsupported platform: {sysname} {arch}")


def _get_lib_filename():
    if sys.platform.startswith("win"):
        return "NativeRKNNC.dll"
    if sys.platform == "darwin":
        return "NativeRKNNC.dylib"
    return "NativeRKNNC.so"


def _load_lib():
    rid = _get_runtime_id()
    _base_path = Path(__file__).resolve().parent
    _lib_path = _base_path / "artifacts" / "native" / rid / _get_lib_filename()
    if not _lib_path.exists():
        raise FileNotFoundError(f"Native library missing {_lib_path}")
    return ct.CDLL(str(_lib_path))

def _as_2d_d64(x: npt.ArrayLike, dim_expected=None):
    a = np.asarray(x, dtype=np.double)
    if a.ndim == 1:
        a = a.reshape(1, -1)
    if a.ndim != 2:
        raise ValueError("expected a 2D array of shape (n, dim) or a 1D vector")
    if dim_expected is not None and a.shape[1] != dim_expected:
        raise ValueError(f"expected dim={dim_expected}, got {a.shape[1]}")
    return a if a.flags["C_CONTIGUOUS"] else np.ascontiguousarray(a)

# Application Binary Interface
lib = _load_lib()
lib.cluster.restype = ct.c_int
lib.cluster.argtypes = [
    ct.POINTER(ct.c_double), #data
    ct.POINTER(ct.c_int), # cluster assignment vector
    ct.c_int, # n
    ct.c_int, # dim
    ct.c_int, # k
    ct.c_int, # t
    ct.c_int, # min_pts
]

# Older native libraries remain usable for unit weights.
_cluster_weighted = getattr(lib, "cluster_weighted", None)
if _cluster_weighted is not None:
    _cluster_weighted.restype = ct.c_int
    _cluster_weighted.argtypes = lib.cluster.argtypes + [ct.c_int]


def _cluster_array(x: np.ndarray, k: int, threshold: int, min_pts: int, weighting: str = "unit"):
    n = int(x.shape[0])
    dim = int(x.shape[1])
    if k <= 0:
        raise ValueError("k must be positive")
    if threshold < 0:
        raise ValueError("threshold must be non-negative")
    if min_pts <= 0:
        raise ValueError("min_pts must be positive")
    if weighting not in ("unit", "rank_taper"):
        raise ValueError("weighting must be 'unit' or 'rank_taper'")
    if weighting == "rank_taper" and _cluster_weighted is None:
        raise RuntimeError("The native library does not support rank_taper; rebuild or upgrade rknnc")

    result = np.empty(n, dtype=np.int32)
    arguments = (
        x.ctypes.data_as(ct.POINTER(ct.c_double)),
        result.ctypes.data_as(ct.POINTER(ct.c_int)),
        n,
        dim,
        k,
        threshold,
        min_pts
    )
    status = (
        lib.cluster(*arguments)
        if weighting == "unit"
        else _cluster_weighted(*arguments, 1)
    )
    if status < 0:
        raise RuntimeError("Native cluster failed")
    return result


def cluster(data: npt.ArrayLike, k: int, threshold: int = 2, min_pts: int = 2, weighting: str = "unit"):
    """Cluster data using unit or rank_taper edge weights."""
    x = _as_2d_d64(data)
    return _cluster_array(x, k, threshold, min_pts, weighting)


class RKNNC:
    """Scikit-learn-style estimator for RKNNC clustering."""

    def __init__(self, k: int, threshold: int = 2, min_pts: int = 2, weighting: str = "unit"):
        self.k = k
        self.threshold = threshold
        self.min_pts = min_pts
        self.weighting = weighting

    def fit(self, X: npt.ArrayLike, y=None):
        """Cluster ``X`` and store the resulting labels on ``labels_``."""
        x = _as_2d_d64(X)
        self.labels_ = _cluster_array(x, self.k, self.threshold, self.min_pts, self.weighting)
        self.n_features_in_ = int(x.shape[1])
        return self

    def fit_predict(self, X: npt.ArrayLike, y=None):
        """Cluster ``X`` and return the assigned labels."""
        return self.fit(X, y).labels_

    def get_params(self, deep: bool = True):
        """Return estimator parameters for compatibility with sklearn utilities."""
        return {
            "k": self.k,
            "threshold": self.threshold,
            "min_pts": self.min_pts,
            "weighting": self.weighting,
        }

    def set_params(self, **params):
        """Set estimator parameters for compatibility with sklearn utilities."""
        valid_params = self.get_params(deep=False)
        for name, value in params.items():
            if name not in valid_params:
                raise ValueError(f"Invalid parameter {name!r} for estimator {type(self).__name__}.")
            setattr(self, name, value)
        return self
