public enum QuestCheckType
{
    ResourceRequirement,      // Existing resource checking (keep current behavior)
    BuildingConstructed,      // Check if specific building(s) are constructed
    UnitProduced,             // Check if specific unit(s) are produced
    BeaconPlacedForScout,     // Check if beacon is placed to send unit_scout
    ScoutEnteredLocation,     // Check if unit_scout enters specific map location
    ModulePlacedOnCore,       // Check if module is placed on core during resource transfer
    Default                   // Auto-completable when returning to BaseScene after accepting quest
}
