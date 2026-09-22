using System;

namespace Game.Sim.Technique
{
    // Thrown by TechniqueJsonSerializer.Deserialize for malformed JSON, schema violations,
    // or a version this build does not know how to read.
    public class TechniqueJsonException : Exception
    {
        public TechniqueJsonException(string message) : base(message)
        {
        }
    }
}
