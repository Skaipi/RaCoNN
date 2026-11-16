# RaCoNN

RKNNC Rank-Consistent Nearest Neighbors (RaCoNN) builds a ranked nearest-neighbor graph and minimizes the hierarchical map equation to produce cluster labels.

## Python

```python
import numpy as np
from rknnc import RKNNC

data = np.random.default_rng(7).normal(size=(1000, 8))
labels = RKNNC(k=20, threshold=2, min_pts=10).fit_predict(data)
```

## MapMinimizer

`MapMinimizer` can also optimize an explicitly supplied undirected weighted graph:

```csharp
using MapMinimizer;

var graph = new UndirectedGraph(4,
[
    new WeightedEdge(0, 1, 2.0),
    new WeightedEdge(1, 2, 0.1),
    new WeightedEdge(2, 3, 2.0),
]);

var optimizer = new MapEquationOptimizer(new MapEquationParameters
{
    Trials = 20,
    RandomSeed = 31337,
});

MapEquationResult result = optimizer.Optimize(graph);
int[] labels = result.GetCoarsestAssignments();
```

## Development

```bash
dotnet test MapMinimizer.Tests/MapMinimizer.Tests.csproj
dotnet build RKNNCluster.slnx
```

Use `./build.sh` to publish the native library into the Python package artifact tree before building a wheel.
