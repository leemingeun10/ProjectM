namespace Game.Sim.Shape
{
    // Built-in hidden-technique shapes, already in ShapeRecognizer's normalized form (64 points,
    // centroid at the origin, RMS radius 500, y up). Generated offline from ideal shapes run
    // through the same integer Resample + Normalize; ShapeTemplateTests regenerates them to check.
    //   Triangle: equilateral, apex up, starting at the apex and going anticlockwise.
    //   Flower:   five-petal plum blossom outline r = 1 + 0.5 cos(5θ), starting at the top
    //             petal tip and going anticlockwise.
    //   Sword:    outline of a straight sword, tip up: tip, blade, crossguard, grip (grip 0.6 wide
    //             each side and 2 long, guard 3 wide and 1 high). Two templates share SwordId so
    //             both stubby and long blades pass: blade 5.5 long and 1.2 wide, and 8 by 0.9.
    //   Crescent: crescent-moon blade opening to the right: an outer unit-circle arc from 50°
    //             (or 55°) round to the matching point below, back along an inner arc through
    //             (-0.6, 0) (or (-0.15, 0)). Two templates share CrescentId, full and thin.
    public static class ShapeTemplates
    {
        public const string TriangleId = "triangle";
        public const string FlowerId = "flower";
        public const string SwordId = "sword";
        public const string CrescentId = "crescent";

        private static ShapePoint P(int x, int y) => new ShapePoint(x, y);

        private static readonly ShapePoint[] TrianglePoints =
        {
            P(0, 692), P(-28, 642), P(-57, 592), P(-87, 541),
            P(-116, 491), P(-145, 440), P(-174, 390), P(-203, 340),
            P(-232, 289), P(-261, 239), P(-290, 188), P(-320, 138),
            P(-349, 87), P(-378, 37), P(-407, -13), P(-436, -63),
            P(-465, -114), P(-494, -164), P(-523, -214), P(-553, -265),
            P(-582, -315), P(-600, -359), P(-543, -359), P(-485, -359),
            P(-428, -359), P(-371, -359), P(-314, -359), P(-257, -359),
            P(-200, -359), P(-142, -359), P(-85, -359), P(-28, -359),
            P(28, -359), P(85, -359), P(142, -359), P(199, -359),
            P(257, -359), P(314, -359), P(371, -359), P(428, -359),
            P(485, -359), P(542, -359), P(600, -359), P(582, -315),
            P(553, -265), P(524, -215), P(494, -164), P(466, -114),
            P(436, -63), P(407, -13), P(378, 36), P(349, 87),
            P(320, 137), P(291, 188), P(262, 238), P(232, 289),
            P(203, 339), P(174, 389), P(145, 440), P(116, 490),
            P(87, 541), P(58, 591), P(29, 641), P(0, 692),
        };

        private static readonly ShapePoint[] FlowerPoints =
        {
            P(0, 672), P(-75, 630), P(-116, 552), P(-136, 465),
            P(-142, 376), P(-137, 288), P(-128, 200), P(-191, 189),
            P(-272, 225), P(-357, 253), P(-445, 271), P(-533, 271),
            P(-616, 243), P(-653, 167), P(-612, 89), P(-543, 33),
            P(-464, -6), P(-379, -36), P(-292, -56), P(-216, -89),
            P(-274, -157), P(-331, -225), P(-381, -299), P(-418, -379),
            P(-437, -466), P(-415, -550), P(-334, -577), P(-252, -544),
            P(-182, -489), P(-123, -422), P(-73, -349), P(-29, -271),
            P(28, -270), P(73, -348), P(123, -421), P(182, -488),
            P(251, -544), P(334, -577), P(414, -550), P(438, -466),
            P(419, -380), P(381, -300), P(332, -226), P(274, -157),
            P(217, -89), P(292, -56), P(379, -36), P(463, -7),
            P(542, 32), P(611, 89), P(652, 167), P(616, 243),
            P(533, 271), P(445, 271), P(358, 254), P(273, 226),
            P(192, 190), P(128, 199), P(137, 288), P(142, 376),
            P(136, 464), P(116, 551), P(75, 630), P(0, 672),
        };

        private static readonly ShapePoint[] SwordShortPoints =
        {
            P(0, 885), P(-16, 811), P(-32, 736), P(-48, 661),
            P(-64, 586), P(-81, 511), P(-97, 436), P(-114, 361),
            P(-130, 286), P(-147, 211), P(-163, 136), P(-179, 61),
            P(-196, -13), P(-212, -88), P(-277, -104), P(-354, -104),
            P(-430, -104), P(-507, -104), P(-540, -148), P(-540, -225),
            P(-522, -284), P(-445, -284), P(-369, -284), P(-292, -284),
            P(-216, -284), P(-139, -284), P(-108, -330), P(-108, -406),
            P(-108, -483), P(-108, -560), P(-108, -637), P(-38, -644),
            P(37, -644), P(108, -638), P(108, -561), P(108, -484),
            P(108, -407), P(108, -330), P(138, -284), P(215, -284),
            P(291, -284), P(368, -284), P(445, -284), P(522, -284),
            P(540, -225), P(540, -148), P(507, -104), P(430, -104),
            P(354, -104), P(277, -104), P(213, -90), P(196, -15),
            P(180, 60), P(163, 135), P(147, 210), P(131, 285),
            P(115, 360), P(98, 435), P(82, 510), P(66, 585),
            P(49, 660), P(33, 735), P(16, 810), P(0, 885),
        };

        private static readonly ShapePoint[] SwordLongPoints =
        {
            P(0, 934), P(-8, 861), P(-16, 787), P(-24, 713),
            P(-32, 639), P(-41, 565), P(-49, 491), P(-58, 417),
            P(-66, 343), P(-74, 269), P(-82, 195), P(-91, 122),
            P(-99, 48), P(-107, -25), P(-116, -99), P(-124, -173),
            P(-152, -225), P(-226, -225), P(-301, -225), P(-375, -225),
            P(-435, -240), P(-435, -315), P(-416, -370), P(-342, -370),
            P(-267, -370), P(-193, -370), P(-118, -370), P(-87, -412),
            P(-87, -487), P(-87, -561), P(-87, -636), P(-37, -661),
            P(36, -661), P(87, -636), P(87, -562), P(87, -487),
            P(87, -413), P(118, -370), P(192, -370), P(267, -370),
            P(341, -370), P(416, -370), P(435, -315), P(435, -241),
            P(376, -225), P(301, -225), P(227, -225), P(152, -225),
            P(125, -174), P(116, -100), P(108, -26), P(100, 47),
            P(91, 121), P(83, 194), P(74, 268), P(66, 342),
            P(58, 416), P(50, 490), P(42, 564), P(33, 638),
            P(25, 712), P(16, 786), P(8, 860), P(0, 934),
        };

        private static readonly ShapePoint[] CrescentFullPoints =
        {
            P(493, 427), P(435, 469), P(371, 504), P(304, 531),
            P(234, 549), P(163, 558), P(92, 558), P(21, 549),
            P(-49, 530), P(-115, 504), P(-178, 469), P(-236, 426),
            P(-288, 375), P(-333, 319), P(-371, 257), P(-401, 192),
            P(-421, 123), P(-433, 51), P(-436, -19), P(-431, -90),
            P(-415, -160), P(-391, -229), P(-359, -293), P(-318, -353),
            P(-270, -407), P(-216, -455), P(-156, -495), P(-92, -527),
            P(-24, -551), P(46, -566), P(118, -572), P(189, -569),
            P(260, -557), P(329, -536), P(395, -506), P(456, -468),
            P(469, -451), P(401, -475), P(329, -488), P(258, -491),
            P(187, -484), P(117, -465), P(50, -436), P(-10, -398),
            P(-66, -351), P(-113, -297), P(-152, -236), P(-181, -170),
            P(-201, -100), P(-209, -29), P(-208, 41), P(-195, 113),
            P(-172, 181), P(-139, 246), P(-97, 304), P(-47, 356),
            P(10, 400), P(73, 435), P(141, 460), P(212, 475),
            P(284, 479), P(355, 472), P(425, 455), P(493, 427),
        };

        private static readonly ShapePoint[] CrescentThinPoints =
        {
            P(443, 467), P(391, 500), P(335, 526), P(276, 547),
            P(216, 562), P(155, 570), P(94, 571), P(33, 566),
            P(-27, 555), P(-86, 537), P(-144, 512), P(-198, 482),
            P(-248, 446), P(-294, 405), P(-336, 360), P(-373, 310),
            P(-404, 256), P(-429, 200), P(-448, 141), P(-461, 79),
            P(-467, 18), P(-467, -42), P(-460, -103), P(-446, -163),
            P(-426, -222), P(-400, -278), P(-368, -331), P(-331, -380),
            P(-289, -426), P(-241, -466), P(-191, -501), P(-136, -530),
            P(-78, -554), P(-19, -571), P(41, -582), P(102, -586),
            P(163, -584), P(224, -575), P(284, -560), P(343, -538),
            P(398, -510), P(435, -481), P(374, -467), P(315, -447),
            P(259, -418), P(207, -384), P(160, -341), P(119, -293),
            P(85, -241), P(58, -184), P(39, -125), P(28, -64),
            P(24, -3), P(28, 57), P(41, 119), P(61, 177),
            P(90, 233), P(124, 285), P(166, 333), P(214, 374),
            P(266, 408), P(323, 435), P(382, 455), P(443, 467),
        };

        // Declared after the point arrays: static initializers run in source order.
        public static readonly ShapeTemplate Triangle = new ShapeTemplate(TriangleId, TrianglePoints);
        public static readonly ShapeTemplate Flower = new ShapeTemplate(FlowerId, FlowerPoints);

        public static readonly ShapeTemplate SwordShort = new ShapeTemplate(SwordId, SwordShortPoints);
        public static readonly ShapeTemplate SwordLong = new ShapeTemplate(SwordId, SwordLongPoints);
        public static readonly ShapeTemplate CrescentFull = new ShapeTemplate(CrescentId, CrescentFullPoints);
        public static readonly ShapeTemplate CrescentThin = new ShapeTemplate(CrescentId, CrescentThinPoints);

        // Several templates may share an Id (variants of one shape); Recognize reports the Id.
        // Every built-in shape, including those of inactive hidden techniques (tests use these).
        public static readonly ShapeTemplate[] All = { Triangle, Flower, SwordShort, SwordLong, CrescentFull, CrescentThin };

        // The shapes the game recognises: those of active hidden techniques (HiddenTechniques).
        // 매화/제왕/월영 stay in All but are inactive for now, so only 삼재 (Triangle) is here.
        public static readonly ShapeTemplate[] Active = { Triangle };
    }
}
