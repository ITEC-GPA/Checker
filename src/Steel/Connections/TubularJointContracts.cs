using System;

namespace GPC.Checkers.Steel.Connections
{
    /// <summary>Partial factors of the EN1993-1-8:2005 section7 workflow. GammaM5 (Table2.1, joints in
    /// hollow section lattice girders) is not part of ConnectionDesignCode; the source records the
    /// national value used, it does not certify it. Plates.GammaM0 and GammaM2Connection serve the
    /// full-strength weld comparison of 7.3.1(4) only.</summary>
    public sealed class TubularJointCode
    {
        public ConnectionDesignCode Connection { get; }
        public double GammaM5 { get; }
        public string GammaM5Source { get; }
        public bool IsNtc => Connection.IsNtc;
        private TubularJointCode(ConnectionDesignCode connection, double gammaM5, string gammaM5Source)
        {
            Connection = connection; GammaM5 = gammaM5; GammaM5Source = gammaM5Source;
        }

        public static TubularJointCode Eurocode3(ConnectionDesignCode connection, double gammaM5, string gammaM5Source)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (connection.IsNtc) throw new ArgumentException("The NTC route has its own factors: use TubularJointCode.Ntc2018().", nameof(connection));
            ConnectionValidation.Positive(gammaM5, nameof(gammaM5));
            if (string.IsNullOrWhiteSpace(gammaM5Source)) throw new ArgumentException("An explicit gammaM5 source (National Annex) is required.", nameof(gammaM5Source));
            return new TubularJointCode(connection, gammaM5, gammaM5Source);
        }

        /// <summary>NTC2018 4.2.8 and Circolare 2019 C4.2 contain no rules for hollow section lattice
        /// joints. NTC2018 chapter12 admits the CEN Eurocodes with the Italian National Annexes: the
        /// Italian NA to UNI EN1993-1-8 (DM 31/07/2012) adopts in 2.2(2) the recommended values of
        /// Table2.1, gammaM5 = 1.0. Members keep the NTC factors (gammaM0 1.05, gammaM2 1.25).</summary>
        public static TubularJointCode Ntc2018()
            => new TubularJointCode(ConnectionDesignCode.Ntc2018(), 1.0,
                "NTC2018 cap.12 -> UNI EN1993-1-8:2005 with the Italian National Annex (DM 31/07/2012), 2.2(2): recommended Table2.1 values, gammaM5 = 1.0");
    }

    public enum TubularSectionShape { Circular, Rectangular }

    /// <summary>EN10210 hot finished or EN10219 cold formed product (EN1993-1-8 7.1.1(3)).</summary>
    public enum TubularFabrication { HotFinished, ColdFormed }

    /// <summary>Structural hollow section of a lattice joint member; mm, mm2, mm3, MPa.
    /// Rectangular: Depth h lies in the plane of the lattice, Width b is normal to it (for the chord,
    /// b0 is the width of the face that carries the braces). Circular: Depth = Width = d, and the area
    /// and elastic modulus of the annulus are computed. Rectangular: Area and the elastic moduli are
    /// the actual product values (rounded corners), not above the sharp-corner values.
    /// NominalGrade is the nominal yield strength of the end product (7.1.1(4)); Fy and Fu are the
    /// values for the product and thickness used in design.</summary>
    public sealed class TubularJointSection
    {
        public TubularSectionShape Shape { get; }
        public double Depth { get; }
        public double Width { get; }
        public double Thickness { get; }
        public double Area { get; }
        public double ElasticModulusInPlane { get; }
        public double ElasticModulusOutOfPlane { get; }
        public int NominalGrade { get; }
        public double Fy { get; }
        public double Fu { get; }
        public TubularFabrication Fabrication { get; }
        private TubularJointSection(TubularSectionShape shape, double depth, double width, double thickness, double area,
            double inPlane, double outOfPlane, int nominalGrade, double fy, double fu, TubularFabrication fabrication)
        {
            Shape = shape; Depth = depth; Width = width; Thickness = thickness; Area = area;
            ElasticModulusInPlane = inPlane; ElasticModulusOutOfPlane = outOfPlane;
            NominalGrade = nominalGrade; Fy = fy; Fu = fu; Fabrication = fabrication;
        }

        public static TubularJointSection Circular(double diameter, double thickness, int nominalGrade, double fy, double fu,
            TubularFabrication fabrication)
        {
            Material(nominalGrade, fy, fu, fabrication);
            ConnectionValidation.Positive(diameter, nameof(diameter)); ConnectionValidation.Positive(thickness, nameof(thickness));
            if (2 * thickness >= diameter) throw new ArgumentException("The wall thickness must be below half the diameter.");
            double inner = diameter - 2 * thickness;
            double area = Math.PI / 4 * (diameter * diameter - inner * inner);
            double modulus = Math.PI * (Math.Pow(diameter, 4) - Math.Pow(inner, 4)) / (32 * diameter);
            return new TubularJointSection(TubularSectionShape.Circular, diameter, diameter, thickness, area, modulus, modulus,
                nominalGrade, fy, fu, fabrication);
        }

        public static TubularJointSection Rectangular(double depth, double width, double thickness, double area,
            double elasticModulusInPlane, double elasticModulusOutOfPlane, int nominalGrade, double fy, double fu,
            TubularFabrication fabrication)
        {
            Material(nominalGrade, fy, fu, fabrication);
            ConnectionValidation.Positive(depth, nameof(depth)); ConnectionValidation.Positive(width, nameof(width));
            ConnectionValidation.Positive(thickness, nameof(thickness)); ConnectionValidation.Positive(area, nameof(area));
            ConnectionValidation.Positive(elasticModulusInPlane, nameof(elasticModulusInPlane));
            ConnectionValidation.Positive(elasticModulusOutOfPlane, nameof(elasticModulusOutOfPlane));
            if (2 * thickness >= Math.Min(depth, width)) throw new ArgumentException("The wall thickness must be below half the smaller side.");
            // Sharp-corner bounds: rounded corners only remove material from the outer rectangle.
            double hi = depth - 2 * thickness, bi = width - 2 * thickness, slack = 1 + 1e-9;
            double sharpArea = depth * width - hi * bi;
            double sharpInPlane = (width * Math.Pow(depth, 3) - bi * Math.Pow(hi, 3)) / (6 * depth);
            double sharpOutOfPlane = (depth * Math.Pow(width, 3) - hi * Math.Pow(bi, 3)) / (6 * width);
            if (area > sharpArea * slack || elasticModulusInPlane > sharpInPlane * slack || elasticModulusOutOfPlane > sharpOutOfPlane * slack)
                throw new ArgumentException("Area and elastic moduli cannot exceed the sharp-corner values of the declared dimensions.");
            return new TubularJointSection(TubularSectionShape.Rectangular, depth, width, thickness, area,
                elasticModulusInPlane, elasticModulusOutOfPlane, nominalGrade, fy, fu, fabrication);
        }

        private static void Material(int nominalGrade, double fy, double fu, TubularFabrication fabrication)
        {
            if (nominalGrade <= 0) throw new ArgumentOutOfRangeException(nameof(nominalGrade));
            ConnectionValidation.Positive(fy, nameof(fy)); ConnectionValidation.Positive(fu, nameof(fu));
            if (fu < fy) throw new ArgumentException("fu must not be below fy.");
            if (!Enum.IsDefined(typeof(TubularFabrication), fabrication)) throw new ArgumentOutOfRangeException(nameof(fabrication));
        }
    }

    public enum TubularWeldKind { FullPenetrationButt, Fillet }

    /// <summary>Weld of one brace around its entire perimeter (7.3.1(2)), to the chord and, for an
    /// overlapping brace, to the overlapped brace. Fillet: effective throat a in mm. ExecutionSource
    /// records consumables matching the parent metal (4.2), the all-round execution and inspection;
    /// it is evidence supplied by the caller, not certified here.</summary>
    public sealed class TubularBraceWeld
    {
        public TubularWeldKind Kind { get; }
        public double Throat { get; }
        public string ExecutionSource { get; }
        private TubularBraceWeld(TubularWeldKind kind, double throat, string executionSource)
        { Kind = kind; Throat = throat; ExecutionSource = executionSource; }
        public static TubularBraceWeld FullPenetration(string executionSource)
            => new TubularBraceWeld(TubularWeldKind.FullPenetrationButt, 0, executionSource);
        public static TubularBraceWeld Fillet(double throat, string executionSource)
        {
            ConnectionValidation.Positive(throat, nameof(throat));
            return new TubularBraceWeld(TubularWeldKind.Fillet, throat, executionSource);
        }
    }

    /// <summary>One brace of the joint. AngleDegrees is the included angle with the chord (0..90].
    /// AxialForce N &gt; 0 is tension. The moments act at the point where the brace centre-line meets
    /// the chord face (7.4.2(3)), Nmm; only T, Y and X joints between CHS members accept them.</summary>
    public sealed class TubularBrace
    {
        public TubularJointSection Section { get; }
        public double AngleDegrees { get; }
        public double AxialForce { get; }
        public double InPlaneMoment { get; }
        public double OutOfPlaneMoment { get; }
        public TubularBraceWeld Weld { get; }
        public TubularBrace(TubularJointSection section, double angleDegrees, double axialForce, TubularBraceWeld weld,
            double inPlaneMoment = 0, double outOfPlaneMoment = 0)
        {
            Section = section ?? throw new ArgumentNullException(nameof(section));
            Weld = weld ?? throw new ArgumentNullException(nameof(weld));
            if (!(angleDegrees > 0) || angleDegrees > 90) throw new ArgumentOutOfRangeException(nameof(angleDegrees));
            ConnectionNumbers.Finite(axialForce, nameof(axialForce)); ConnectionNumbers.Finite(inPlaneMoment, nameof(inPlaneMoment));
            ConnectionNumbers.Finite(outOfPlaneMoment, nameof(outOfPlaneMoment));
            AngleDegrees = angleDegrees; AxialForce = axialForce; InPlaneMoment = inPlaneMoment; OutOfPlaneMoment = outOfPlaneMoment;
        }
    }

    /// <summary>Chord actions in the cut on one side of the joint, from the same combination as the
    /// brace forces; N, Nmm. N &gt; 0 is tension. InPlaneShear and InPlaneMoment act in the plane of the
    /// lattice, OutOfPlaneMoment normal to it; their signs are not used. The moments include the
    /// eccentricity moments assigned to the chord (5.1.5(6) and (8)).</summary>
    public sealed class TubularChordActions
    {
        public double N { get; }
        public double InPlaneShear { get; }
        public double InPlaneMoment { get; }
        public double OutOfPlaneMoment { get; }
        public TubularChordActions(double n, double inPlaneShear = 0, double inPlaneMoment = 0, double outOfPlaneMoment = 0)
        {
            ConnectionNumbers.Finite(n, nameof(n)); ConnectionNumbers.Finite(inPlaneShear, nameof(inPlaneShear));
            ConnectionNumbers.Finite(inPlaneMoment, nameof(inPlaneMoment)); ConnectionNumbers.Finite(outOfPlaneMoment, nameof(outOfPlaneMoment));
            N = n; InPlaneShear = inPlaneShear; InPlaneMoment = inPlaneMoment; OutOfPlaneMoment = outOfPlaneMoment;
        }
    }

    /// <summary>Figure7.1 uniplanar types. T: one brace at 90 degrees; Y: one inclined brace;
    /// X: two identical coaxial braces on opposite chord faces carrying the same force; K: two
    /// inclined braces; N: two braces, one of them at 90 degrees. Gap or overlap follows from the
    /// sign of the gap.</summary>
    public enum TubularJointType { T, Y, X, K, N }
    public enum TubularOverlappingBrace { A, B }

    /// <summary>One uniplanar welded lattice joint between hollow sections, one concomitant ULS
    /// combination. Geometry convention: chord side A and side B are the two chord cuts next to the
    /// joint; BraceA leans from the chord towards side A, BraceB (K and N joints only) towards side
    /// B, so that the braces diverge from the chord face (Figure1.3). For an X joint BraceA is the
    /// brace on the loaded face; the coaxial brace on the opposite face is identical and carries the
    /// same force. Gap: g of Figure1.3(a) for K and N joints, mm, measured along the chord face
    /// between the brace toes; a negative value is the overlap q (Figure1.3(b), 7.4.2(6)). It must be
    /// zero for T, Y and X joints. ActionSource records that chord and brace forces come from the
    /// same combination; GlobalAnalysisSource records the lattice analysis of 5.1.5 (pinned or
    /// continuous chords, system length to depth not below the applicable minimum, eccentricity
    /// moments carried by the chord design). ThroughThicknessSource is needed only for a chord wall
    /// over 25 mm (7.1.1(6)). Multiplanar, reinforced and fatigue situations are declared so that
    /// they can be refused.</summary>
    public sealed class TubularJointInput
    {
        public TubularJointCode Code { get; }
        public TubularJointType Type { get; }
        public TubularJointSection Chord { get; }
        public TubularChordActions ChordSideA { get; }
        public TubularChordActions ChordSideB { get; }
        public TubularBrace BraceA { get; }
        public TubularBrace BraceB { get; }
        public double Gap { get; }
        public TubularOverlappingBrace OverlappingBrace { get; }
        /// <summary>Overlap joints: the hidden seam of the overlapped brace is welded (7.1.2(6), 7.3.1(2)).</summary>
        public bool HiddenSeamWelded { get; }
        public string ActionSource { get; }
        public string GlobalAnalysisSource { get; }
        public string ThroughThicknessSource { get; }
        public bool Multiplanar { get; }
        public bool Reinforced { get; }
        public bool FatigueLoading { get; }
        public TubularJointInput(TubularJointCode code, TubularJointType type, TubularJointSection chord,
            TubularChordActions chordSideA, TubularChordActions chordSideB, TubularBrace braceA, TubularBrace braceB,
            double gap, string actionSource, string globalAnalysisSource,
            TubularOverlappingBrace overlappingBrace = TubularOverlappingBrace.A, bool hiddenSeamWelded = false,
            string throughThicknessSource = null, bool multiplanar = false, bool reinforced = false, bool fatigueLoading = false)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Chord = chord ?? throw new ArgumentNullException(nameof(chord));
            ChordSideA = chordSideA ?? throw new ArgumentNullException(nameof(chordSideA));
            ChordSideB = chordSideB ?? throw new ArgumentNullException(nameof(chordSideB));
            BraceA = braceA ?? throw new ArgumentNullException(nameof(braceA));
            if (!Enum.IsDefined(typeof(TubularJointType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            if (!Enum.IsDefined(typeof(TubularOverlappingBrace), overlappingBrace)) throw new ArgumentOutOfRangeException(nameof(overlappingBrace));
            ConnectionNumbers.Finite(gap, nameof(gap));
            bool two = type == TubularJointType.K || type == TubularJointType.N;
            if (two && braceB == null) throw new ArgumentNullException(nameof(braceB), "K and N joints have two braces.");
            if (!two && braceB != null) throw new ArgumentException("T, Y and X joints are described by BraceA only.", nameof(braceB));
            if (!two && gap != 0) throw new ArgumentException("The gap applies to K and N joints only.", nameof(gap));
            Type = type; BraceB = braceB; Gap = gap; OverlappingBrace = overlappingBrace; HiddenSeamWelded = hiddenSeamWelded;
            ActionSource = actionSource; GlobalAnalysisSource = globalAnalysisSource; ThroughThicknessSource = throughThicknessSource;
            Multiplanar = multiplanar; Reinforced = reinforced; FatigueLoading = fatigueLoading;
        }
    }
}
