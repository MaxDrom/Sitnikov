# Builds:

[Linux-x64 build](https://github.com/MaxDrom/Sitnikov/releases/download/latest/linux-x64.zip)

[Windows-x64 build](https://github.com/MaxDrom/Sitnikov/releases/download/latest/win-x64.zip)

[Macos-arm64 build](https://github.com/MaxDrom/Sitnikov/releases/download/latest/osx-arm64.zip)

# Getting started:

Unzip archive and run:

```bash
./Sitnikov
```

# Config structure:

## Keys:

```yaml
e: 0.12 ---# Eccentricity

SizeX: 70 ---# Number of initial points coloumns
SizeY: 70 ---# Number of initial points rows

RangeX: ---# The size of the initial points region along coordinate
    Item1: 0 ---# min coordinate
    Item2: 0.5 ---# max coordinate
RangeY: ---# The size of the initial points region along velocity
    Item1: 0 
    Item2: 0.5

Integrator: ---# Integrator parameters (optional)
    Order: 4 ---# Integrator order (must be even)
    Timestep: 0.01 ---# Integrator step

Visualization: ---# Visualization parameters (optional)
    OnGPU: true ---# Do update on gpu instead of cpu
    Fade: 3 ---# 
    RangeX: ---# The size of the visualization domain along coordinate
        Item1: -3
        Item2: 3
    RangeY: ---# The size of the visualization domain along velocity
        Item1: -3
        Item2: 3

Poincare: ---# Poincare map parameters (optional, if not specified, 
                if not specified, a phase portrait visualization will 
                be run instead of map generation; 
                if specified, 
                the phase portrait visualization will not be run)
    Periods: 100 ---# Number of iteration
```
![image](plot.png)
