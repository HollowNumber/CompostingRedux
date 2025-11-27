# Moisture Manager System

## Overview

The `MoistureManager` class is a modular, reusable component that handles all moisture-related logic for composting systems. It was extracted from the `CompostProcessor` to follow the DRY (Don't Repeat Yourself) principle and improve code maintainability.

## Architecture

### Location
```
CompostingRedux/BlockEntities/Helpers/Moisture/MoistureManager.cs
```

### Responsibilities

The `MoistureManager` encapsulates:
- **Moisture tracking**: Maintains current moisture level (0.0 to 1.0)
- **Environmental updates**: Handles rain and evaporation based on climate
- **Manual control**: Supports adding water or dry materials
- **State assessment**: Provides moisture state checks (too dry, optimal, too wet)
- **Decomposition modifiers**: Calculates how moisture affects decomposition rate
- **Serialization**: Saves and loads moisture state

## Usage

### Initialization

```csharp
// Create a new moisture manager
var moistureManager = new MoistureManager();

// Set API reference (required for environmental updates)
moistureManager.SetApi(api);

// Set block position (required for rain detection)
moistureManager.SetBlockPos(blockPos);
```

### Environmental Updates

The moisture manager automatically handles atmospheric conditions:

```csharp
// Called from your Update() method
double currentTime = api.World.Calendar.TotalHours;
moistureManager.UpdateEnvironmental(currentTime);
```

This method:
1. Checks once per hour (performance optimization)
2. Detects if block is exposed to rain
3. Gets climate data (temperature, rainfall)
4. Adds moisture during rain (up to 0.1/hour in heavy rain)
5. Removes moisture via evaporation (faster in warm weather)

### Manual Moisture Control

```csharp
// Add water (e.g., from bucket or water portion)
moistureManager.AddWater(0.2f); // Adds 20% moisture

// Add dry material (e.g., browns when too wet)
moistureManager.AddDryMaterial(0.15f); // Removes 15% moisture

// Set directly
moistureManager.SetLevel(0.5f); // Set to optimal
```

### Checking Moisture State

```csharp
// Get current level (0.0 to 1.0)
float level = moistureManager.Level;

// Boolean state checks
bool isOptimal = moistureManager.IsOptimal;     // 0.4 - 0.6
bool isTooDry = moistureManager.IsTooDry;       // < 0.3
bool isTooWet = moistureManager.IsTooWet;       // > 0.7
bool isBoneDry = moistureManager.IsBoneDry;     // < 0.2
bool isWaterlogged = moistureManager.IsWaterlogged; // > 0.85

// Get descriptive state string
string state = moistureManager.State; // "Bone Dry", "Too Dry", "Optimal", etc.
```

### Decomposition Modifier

```csharp
// Get multiplier for decomposition rate (0.1 to 1.0)
float modifier = moistureManager.GetDecompositionModifier();

// Apply to your decomposition calculation
float baseRate = 1.0f / hoursToComplete;
float actualRate = baseRate * modifier;
```

### Serialization

```csharp
// Save to tree attributes
public void ToTreeAttributes(ITreeAttribute tree)
{
    moistureManager.ToTreeAttributes(tree);
}

// Load from tree attributes
public void FromTreeAttributes(ITreeAttribute tree)
{
    moistureManager.FromTreeAttributes(tree);
}
```

## Moisture Levels

| Level | Range | State | Decomposition Modifier | Description |
|-------|-------|-------|----------------------|-------------|
| 0.0 - 0.2 | Bone Dry | Critical | 0.1x | Microbes can't function |
| 0.2 - 0.3 | Too Dry | Poor | 0.5x | Microbial activity severely limited |
| 0.3 - 0.4 | Slightly Dry | Fair | 0.8x | Minor slowdown |
| 0.4 - 0.6 | Optimal | Excellent | 1.0x | Ideal conditions |
| 0.6 - 0.7 | Slightly Wet | Fair | 0.8x | Minor slowdown |
| 0.7 - 0.85 | Too Wet | Poor | 0.4x | Anaerobic conditions |
| 0.85 - 1.0 | Waterlogged | Critical | 0.2x | May putrefy instead of compost |

## Environmental Mechanics

### Rain Detection

```csharp
bool isRaining = api.World.BlockAccessor.GetRainMapHeightAt(blockPos.X, blockPos.Z) <= blockPos.Y;
```

The system checks if the block's Y position is at or below the rain map height, meaning it's exposed to rain.

### Rain Gain

```csharp
float rainGain = rainfall * 0.1f; // Max 0.1 per hour
```

Rain adds moisture based on climate rainfall value (0-1 scale).

### Evaporation

```csharp
float evaporationRate = 0.02f; // Base rate
if (temperature > 0)
{
    evaporationRate += temperature * 0.001f; // More in warm weather
}
if (isRaining)
{
    evaporationRate *= 0.1f; // Much slower when raining
}
```

Evaporation increases with temperature and decreases during rain.

## Configuration Constants

All tunable values are defined as constants at the top of the class:

```csharp