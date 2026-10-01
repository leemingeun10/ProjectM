using System;

namespace Game.Sim.Pvp
{
    // Thrown by PvpJsonSerializer.Deserialize for malformed JSON, schema violations,
    // or a version this build does not know how to read.
    public class PvpJsonException : Exception
    {
        public PvpJsonException(string message) : base(message)
        {
        }
    }
}
