using System;

namespace Game.Sim.Cultivation
{
    // Thrown by CultivationJsonSerializer.Deserialize for malformed JSON, schema violations,
    // or a version this build does not know how to read.
    public class CultivationJsonException : Exception
    {
        public CultivationJsonException(string message) : base(message)
        {
        }
    }
}
