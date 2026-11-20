using System.Collections.Generic;
using Game.Sim.Technique;

namespace Game.Sim.Stroke
{
    // The two length units strokes exist in, and the one conversion between them.
    //
    // Frame centimetres: what the player draws and what a technique slot stores. Origin at the
    // drawing-frame centre, y up, raw = cm * Fixed.Scale.
    // Canvas units: what battles are computed in (BattleSimulator's contact and Thrust damage) and
    // what AiTechniqueGenerator produces. Same origin and axes, raw = units * Fixed.Scale.
    //
    // Saved player steps are converted once, right before a battle (StageSelectController's slot
    // load). AI-generated steps are already canvas units and must never be passed through here.
    public static class StrokeUnits
    {
        // Width (and height) of the square drawing frame, in cm * Fixed.Scale. The only number to
        // change to resize the frame: the client's on-screen frame, the stroke length limit and
        // the AI's drawing area and stroke sizes all derive from it.
        public const long FrameWidthCmRaw = 4500;

        // Canvas units per centimetre, as an exact ratio: 640 / 3 = 213.33..., which maps the
        // 4.5 cm frame onto 960 canvas units.
        public const long CanvasPerCmNumerator = 640;
        public const long CanvasPerCmDenominator = 3;

        // Path length one turn's stroke may have: frame width * 8 / 3 (12 cm for a 4.5 cm frame).
        // The same for every character; the client stops a drawn stroke here and the AI sizes its
        // strokes as a share of it.
        public const long MaxStrokeLengthCmRaw = FrameWidthCmRaw * 8 / 3;

        // The frame and the length limit in canvas units * Fixed.Scale (480,000 from the centre to
        // each edge and 2,560,000 for a 4.5 cm frame). Same rounding as CmToCanvasRaw.
        public const long FrameHalfCanvasRaw =
            (FrameWidthCmRaw / 2 * CanvasPerCmNumerator * 2 + CanvasPerCmDenominator) / (2 * CanvasPerCmDenominator);
        public const long MaxStrokeLengthCanvasRaw =
            (MaxStrokeLengthCmRaw * CanvasPerCmNumerator * 2 + CanvasPerCmDenominator) / (2 * CanvasPerCmDenominator);

        // cm * Fixed.Scale -> canvas units * Fixed.Scale. Integer only; rounds to nearest with
        // halves away from zero, applied to the magnitude so that f(-x) == -f(x).
        public static long CmToCanvasRaw(long cmRaw)
        {
            long magnitude = cmRaw < 0 ? -cmRaw : cmRaw;
            long scaled = (magnitude * CanvasPerCmNumerator * 2 + CanvasPerCmDenominator) / (2 * CanvasPerCmDenominator);
            return cmRaw < 0 ? -scaled : scaled;
        }

        public static StrokePoint CmToCanvas(StrokePoint cmPoint)
            => new StrokePoint(CmToCanvasRaw(cmPoint.X.Raw), CmToCanvasRaw(cmPoint.Y.Raw));

        public static StrokePoint[] CmToCanvas(IReadOnlyList<StrokePoint> cmPoints)
        {
            int count = cmPoints?.Count ?? 0;
            var converted = new StrokePoint[count];
            for (int i = 0; i < count; i++)
                converted[i] = CmToCanvas(cmPoints[i]);
            return converted;
        }

        // One saved step in battle units. The points are scaled; the saved classification (Slash /
        // Thrust), counter flag and hidden-technique fields are kept as they are, so a turn fights
        // as what the player saw when saving it. A Slash's length is scaled the same way and its
        // directions kept; a Thrust's area is measured again on the scaled points.
        public static TechniqueStep CmToCanvas(TechniqueStep cmStep)
        {
            StrokePoint[] points = CmToCanvas(cmStep.RawPoints);
            StrokeResult saved = cmStep.Stroke;
            StrokeResult stroke = saved.Type == StrokeType.Thrust
                ? StrokeResult.Thrust(StrokeAnalyzer.ClosedArea(points))
                : StrokeResult.Slash(Fixed.FromRaw(CmToCanvasRaw(saved.Length.Raw)),
                    saved.StartDirectionX, saved.StartDirectionY, saved.EndDirectionX, saved.EndDirectionY);
            return new TechniqueStep(cmStep.TurnIndex, cmStep.IsCounter, stroke, points, cmStep.HiddenTechniqueId, cmStep.HiddenMove);
        }

        // A whole saved sequence in battle units; the input list is left untouched.
        public static List<TechniqueStep> CmToCanvas(IReadOnlyList<TechniqueStep> cmSteps)
        {
            int count = cmSteps?.Count ?? 0;
            var converted = new List<TechniqueStep>(count);
            for (int i = 0; i < count; i++)
                converted.Add(CmToCanvas(cmSteps[i]));
            return converted;
        }
    }
}
