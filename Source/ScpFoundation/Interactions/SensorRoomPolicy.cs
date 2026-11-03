using System;
using System.Collections.Generic;
using System.Linq;

namespace ScpFoundation.Interactions
{
    public static class SensorRoomPolicy
    {
        public static T Unique<T>(IEnumerable<T> candidates, Func<T, bool> inRoom, out bool ambiguous) where T : class
        {
            T[] matches = candidates.Where(inRoom).Take(2).ToArray();
            ambiguous = matches.Length > 1;
            return matches.Length == 1 ? matches[0] : null;
        }
    }
}
