# Module Effect System Documentation

## Overview
The Module Effect System allows modules to apply stat modifications to various game systems when selected at game start. The system is designed to be expandable and supports multiple stat types.

## Architecture

### Core Components

1. **ModuleStatType** - Enumeration of all available stat types
2. **ModuleStatModifier** - Data structure for a single stat modification
3. **ModuleEffectData** - ScriptableObject that defines stat modifications for a module
4. **ModuleEffectManager** - Singleton that applies module effects at game start
5. **StatModifierReceiver_*** - Components that receive and apply modifiers to specific systems
6. **ActiveStatDisplay** - UI component that displays active stat icons

## Stat Types

Currently supported stat types:
- `StorageCapacity` - Increases storage building capacity
- `UnitMoveSpeed` - Increases unit movement speed
- `UnitWorkSpeed` - Increases unit work/processing speed
- `BuildingHP` - Increases building maximum health
- `ResourceGenerationRate` - Increases resource generation rate
- `TurretAttackDamage` - Increases turret attack damage

## Usage

### 1. Creating a Module Effect

1. Create a new `ModuleEffectData` ScriptableObject:
   - Right-click in Project window
   - Select `Module > Module Effect Data`
   - Name it appropriately (e.g., "SpeedBoostEffect")

2. Configure the effect:
   - Add stat modifiers in the `Stat Modifiers` list
   - Set the `modifierValue` (e.g., 0.25 = 25% increase)
   - Assign a sprite icon for each stat modifier
   - Optionally set a custom description

3. Assign to ModuleRecipe:
   - Open your ModuleRecipe asset
   - Assign the ModuleEffectData to the `Effect Data` field

### 2. Setting Up the ModuleEffectManager

1. Create an empty GameObject in your game scene
2. Add the `ModuleEffectManager` component
3. The manager will automatically apply effects from all modules in BaseInventoryManager at Start

### 3. Setting Up Active Stat Display

1. Create a UI GameObject (e.g., Canvas child)
2. Add the `ActiveStatDisplay` component
3. Assign a stat icon prefab (should have Image component)
4. Set the icon container (where icons will be displayed)
5. Configure spacing and size as needed

### 4. Adding Stat Modifier Receivers

The following components automatically apply modifiers:
- `StatModifierReceiver_Storage` - For BaseStorage components
- `StatModifierReceiver_UnitMovement` - For UnitMovement components
- `StatModifierReceiver_UnitWorkSpeed` - For Unit_Drone components
- `StatModifierReceiver_BuildingHP` - For Damageable components
- `StatModifierReceiver_ResourceGeneration` - For ResourceGenerator components
- `StatModifierReceiver_TurretDamage` - For Turret components

**Note:** These components should be added to prefabs or added dynamically to game objects that need stat modifications.

## Extending the System

### Adding a New Stat Type

1. Add the new stat type to `ModuleStatType` enum
2. Create a new `StatModifierReceiver_*` component for the stat type
3. Implement the receiver to:
   - Store the original value
   - Apply modifiers from ModuleEffectManager
   - Reset to original value on destroy (if needed)

### Example: Adding a New Stat Type

```csharp
// 1. Add to ModuleStatType enum
public enum ModuleStatType
{
    // ... existing types
    NewStatType
}

// 2. Create receiver component
public class StatModifierReceiver_NewStat : MonoBehaviour
{
    private YourComponent _component;
    private float _originalValue;
    
    private void Start()
    {
        _component = GetComponent<YourComponent>();
        _originalValue = _component.value;
        ApplyModifiers();
    }
    
    public void ApplyModifiers()
    {
        if (_component == null || ModuleEffectManager.Instance == null) return;
        
        float modifier = ModuleEffectManager.Instance.GetStatModifier(ModuleStatType.NewStatType);
        _component.value = _originalValue * (1f + modifier);
    }
}
```

## How It Works

1. **Game Start:**
   - ModuleEffectManager loads all modules from BaseInventoryManager
   - For each module, it applies the ModuleEffectData stat modifiers
   - Modifiers are stored in ModuleEffectManager's dictionary

2. **Object Creation:**
   - When objects with StatModifierReceiver components are created, they:
     - Store their original stat values
     - Query ModuleEffectManager for modifiers
     - Apply modifiers to their stats

3. **UI Display:**
   - ActiveStatDisplay queries ModuleEffectManager for active modifiers
   - Creates icon UI elements for each active stat
   - Displays icons with sprites from ModuleEffectData

## Notes

- Modifiers stack additively (multiple modules with the same stat type will combine)
- Modifiers are applied as percentage increases (0.25 = 25% increase)
- The system automatically handles objects created after game start
- Stat icons are pulled from the first module that has that stat type with an icon

