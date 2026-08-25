# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

An agent-based simulation of African elephants roaming Kruger National Park (South Africa), built on
the **MARS** framework (`Mars.Life.Simulations`, referenced as a NuGet package, source not in this repo).
Elephants move around a shared geospatial environment, competing for water and vegetation (biomass) under
climate and tourism pressures, form herds, reproduce, age, and die. The simulation reads real GIS data
(rasters + vector geometry) and produces CSV/GeoJSON output.

## Solution layout

- `KrugerNationalPark/` — the model library (agents, layers, output adapters). No `Main`.
- `KrugerNationalParkBox/` — the runnable "simulation box": `Program.cs` wires up layers/agents and
  starts the simulation, plus `config.json` and `resources/` (GIS input data, CSVs for agent init).
- `KNPElephant.sln` — Visual Studio solution tying the two projects together.

Both projects target `net10.0`.

## Common commands

Run from `KrugerNationalParkBox/`:

```bash
# Run the simulation with the default config.json
dotnet run --sm config.json

# Build the whole solution
dotnet build ../KNPElephant.sln

# Produce self-contained release builds for macOS/Windows/Linux, zipped
sh ./build.sh
# then, e.g.:
cd KrugerNationalParkBase/KrugerNationalParkBase_MACOSX/
./KrugerNationalParkBox --sm config.json
```

There is no test project in this repo — nothing to `dotnet test`.

Note: `Program.cs` currently checks for the arg literal `-sm` while the README/build output use
`--sm`; either form is accepted because the check is `args.Any(s => s.Equals("-sm"))` combined with
`--sm` not matching — in practice just follow the README/build.sh usage above. If no `-sm`/`--sm` arg
is given, it falls back to reading `config.json` in the working directory.

On macOS, a freshly downloaded/built box may need Gatekeeper quarantine removed before its
`*.dylib`/`*.dll` files are trusted:
```bash
xattr -d com.apple.quarantine ./KrugerNationalParkBox ./*.dll ./*.dylib
```

## Configuration

`config.json` (in `KrugerNationalParkBox/`) drives everything: simulation time window/step size
(`globals`), which GIS layers to load and from which file (`layers`), and which agent types to spawn,
from which init CSV, at what count/frequency (`agents`). Input files live in `resources/` (zipped
raster GIS data, a water-body GeoJSON, elephant/car init CSVs) and are copied to the output dir on
build. `-l` on the command line raises the MARS logger to `Info`.

## Architecture

The simulation follows MARS's layer/agent model:

- **Layers** (`KrugerNationalPark/Layers/`) are the environment. Static GIS-backed layers
  (`RasterTempLayer`, `RasterFenceLayer`, `RasterShadeLayer`, `RasterVegetationLayer`,
  `VectorWaterLayer`) wrap `RasterLayer`/`VectorLayer` from MARS and add domain-specific query helpers
  (e.g. `IsPointInside`, `ExploreClosestFullPotentialField`, `HasFullPotential`). `RasterVegetationLayer`
  is mutable — `Reduce(x, y, amount)` is called when an elephant eats, depleting biomass at a cell.
  `ElephantLayer` is the *active* layer: it owns the `ConcurrentDictionary<Guid, Elephant>` of all
  elephants, spawns the initial population from `AgentInitConfig`, groups elephants into herds
  (`ElephantHerd`) by `HerdId`, and exposes `SpawnCalf`/herd lookup to agents. `Program.cs` registers
  layers in dependency order (data layers first, `ElephantLayer` last, since it depends on the others
  via constructor injection).

- **Agents** (`KrugerNationalPark/Agents/`) — `Elephant` is the only live agent type (`Tourist`/car
  agents referenced in `config.json` have no corresponding class in this repo — that config block is
  effectively inert until such an agent type is added). Its per-tick logic lives in `Reason()`, called
  once per simulation step: track hydration/dehydration (temperature- and shade-dependent), starve/die
  after too long without food or water, handle pregnancy/birth (spawns a calf via `ElephantLayer`), age
  yearly and transition life stage (`ElephantLifePeriod`: Calf → Adolescent → Adult) and determine
  sex/type (`ElephantType`) on reaching adulthood, then branch on hour-of-day for sleep/shade-seeking/
  eat-drink-move behavior. Herd behavior: the `Leading` elephant in a herd pathfinds toward known water
  sources (`WaterSources`) or the best nearby vegetation cell; followers move toward their herd's
  leading elephant (looked up via `ElephantLayer.GetLeadingElephantByHerd`) or random-walk. Movement is
  constrained by `RasterFenceLayer` (park boundary) and executed through the shared
  `GeoHashEnvironment<Elephant>`. `Elephant.Die(reason)` records a `MattersOfDeath` and removes the
  agent from `ElephantLayer.Entities`.

- **Output** (`KrugerNationalPark/Output/`) — every `Elephant` implements `ITripSavingAgent` and
  accumulates its positions each tick into a `TripsCollection`. After the simulation ends,
  `Program.cs` calls `TripsOutputAdapter.PrintTripResult` to serialize all agents' trips into a
  timestamped `trips_*.geojson` file (via `TripsLineConverter`/`TripPositionCoordinateConverter`), in
  addition to whatever CSV output MARS itself produces per `config.json`'s `output`/`outputFrequency`
  settings.

- **Misc** (`KrugerNationalPark/Misc/`) — `NormalDistributionGenerator` (Box-Muller, clamped to
  ±3σ) is used to draw initial elephant ages; `EnumerableExtensions.Shuffle` is a Fisher–Yates shuffle.

- **Interactions** (`KrugerNationalPark/Interactions/`) — marula-tree eating/pushing actions
  (`EatMarulaFruitsAction`, `EatLeavesAction`, `EatSeedlingAction`, `PushTreeAction`, `PoopAction`).
  These are currently **dead code**: the only place that would use them (`Elephant.TryToEatFromMarula`)
  is commented out in favor of the simpler DGVM vegetation-layer feeding model
  (`TryToEatFromVegetationLayer`). Historical elephant-culling logic (1989–1994 quotas) is likewise
  commented out in `ElephantLayer.PostTick`. Don't be surprised these exist unused — they represent an
  earlier/alternate model design, not something to wire up unprompted.
