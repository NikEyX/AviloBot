using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Bot
{
    internal class Buffs
    {
        //you can get all these values from the stableid.json file (just search for it on your PC)
        
        public static readonly HashSet<uint> CarryMinerals = new HashSet<uint> {
            271, 
            272
        };
        
        
        public static readonly HashSet<uint> CarryVespene = new HashSet<uint> {
            273, 
            274,
            275
        };
    }
}
