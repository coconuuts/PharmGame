namespace VisualStorage // Your namespace for visual storage
{
    /// <summary>
    /// Defines how a visual item prefab occupies ShelfSlot grid spaces.
    /// </summary>
    public enum ShelfSlotArrangement
    {
        OneByOne, // Occupies a single 1x1 slot
        OneByTwo, // Occupies a 1x2 block of slots (1 row, 2 columns)
        TwoByOne, // Occupies a 2x1 block of slots (2 rows, 1 column)
        TwoByTwo  // Occupies a 2x2 block of slots (2 rows, 2 columns)
    }
}