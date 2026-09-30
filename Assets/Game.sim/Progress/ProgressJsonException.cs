using System;

namespace Game.Sim.Progress
{
    // Thrown by ProgressJsonSerializer.Deserialize for malformed JSON, schema violations,
    // or a version this build does not know how to read.
    public class ProgressJsonException : Exception
    {
        public ProgressJsonException(string message) : base(message)
        {
        }
    }
}
