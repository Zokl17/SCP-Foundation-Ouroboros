namespace ScpFoundation.Interactions
{
    public static class DecontaminationPolicy
    {
        public const int CycleTicks = 600;
        public const int MaximumRoomCells = 64;

        public static bool RoomReady(bool exists, bool touchesMapEdge, int cellCount, int openRoofCount, bool openDoor)
        {
            return exists && !touchesMapEdge && cellCount > 0 && cellCount <= MaximumRoomCells
                && openRoofCount == 0 && !openDoor;
        }

        public static int Advance(int ticksRemaining, bool powered, bool roomReady)
        {
            if (ticksRemaining <= 0) return 0;
            return powered && roomReady ? ticksRemaining - 1 : 0;
        }

        public static bool StructureIntact(int expectedDoors, int survivingDoors, int expectedCells, int currentCells, bool sameCells)
        {
            return expectedDoors == survivingDoors && expectedCells > 0
                && expectedCells == currentCells && sameCells;
        }
    }
}
