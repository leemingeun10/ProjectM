namespace Game.Sim.Common
{
    // Sine/cosine of whole degrees as integers scaled by Scale (sin 90° == 10000), read from a
    // hardcoded quarter-wave table so every device gets the same values without touching float.
    public static class FixedTrig
    {
        public const int Scale = 10000;

        // round(sin(d°) * 10000) for d = 0..90; the other quadrants follow by symmetry.
        private static readonly int[] QuarterSine =
        {
            0, 175, 349, 523, 698, 872, 1045, 1219, 1392, 1564,
            1736, 1908, 2079, 2250, 2419, 2588, 2756, 2924, 3090, 3256,
            3420, 3584, 3746, 3907, 4067, 4226, 4384, 4540, 4695, 4848,
            5000, 5150, 5299, 5446, 5592, 5736, 5878, 6018, 6157, 6293,
            6428, 6561, 6691, 6820, 6947, 7071, 7193, 7314, 7431, 7547,
            7660, 7771, 7880, 7986, 8090, 8192, 8290, 8387, 8480, 8572,
            8660, 8746, 8829, 8910, 8988, 9063, 9135, 9205, 9272, 9336,
            9397, 9455, 9511, 9563, 9613, 9659, 9703, 9744, 9781, 9816,
            9848, 9877, 9903, 9925, 9945, 9962, 9976, 9986, 9994, 9998,
            10000,
        };

        // Any integer angle is accepted; it is wrapped into 0..359 first.
        public static int Sin(int degrees)
        {
            int d = WrapDegrees(degrees);
            if (d <= 90) return QuarterSine[d];
            if (d <= 180) return QuarterSine[180 - d];
            if (d <= 270) return -QuarterSine[d - 180];
            return -QuarterSine[360 - d];
        }

        // Wrapped before adding 90 so int.MaxValue cannot overflow.
        public static int Cos(int degrees) => Sin(WrapDegrees(degrees) + 90);

        public static int WrapDegrees(int degrees)
        {
            int d = degrees % 360;
            return d < 0 ? d + 360 : d;
        }
    }
}
