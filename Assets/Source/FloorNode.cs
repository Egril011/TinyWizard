using Quinn.DungeonGeneration;
using UnityEngine;

namespace Quinn
{
    public class FloorNode
    {
        public FloorSO floor;
        public FloorNode next;

        public FloorNode(FloorSO floor)
        {
            this.floor = floor;
            this.next = null;
        }
    }
}
