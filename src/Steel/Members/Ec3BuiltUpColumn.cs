using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Sections;

namespace GPC.Checkers.Steel.Members
{
    /// <summary>Figure 6.9 from left to right. PanelLength is the dimension a
    /// in that figure: a diagonal spans a/2 in ZigZag and a in the other systems.</summary>
    public enum LacingSystem { ZigZag, ZigZagWithPosts, WithPosts }
    public enum BuiltUpLoadDiagram { Sinusoidal, UniformTransverseLoad }
    public enum BuiltUpAnalysisStatus { Evaluated, Unstable }

    /// <summary>EN 1993-1-1:2005/AC:2009 6.4.1-6.4.3. Two identical parallel
    /// chords, pin-ended in the analysed plane, uniform modules (at least three),
    /// two corresponding lacing/batten planes. N, mm, MPa, Nmm throughout.
    /// Requires end tie panels and load introduction/restraints per 6.4.2.2/6.4.3.2.
    /// This is a plane model; global out-of-plane/torsional checks remain separate.</summary>
    public sealed class Ec3BuiltUpColumn
    {
        public double Length { get; }
        public double PanelLength { get; }
        public double ChordSpacing { get; }
        public double ChordArea { get; }
        public double EffectiveInertia { get; }
        public double ShearStiffness { get; }
        public double Efficiency { get; }
        public double EulerForce { get; }
        public double CriticalForce { get; }
        public double DiagonalLength { get; }
        public bool IsBattened { get; }
        public LacingSystem? Lacing { get; }
        public int Planes => 2;

        private Ec3BuiltUpColumn(double e, double length, int panels, double spacing,
            double chordArea, double inertia, double shear, double efficiency, bool battened, LacingSystem? lacing, double diagonal)
        {
            Length=length; PanelLength=length/panels; ChordSpacing=spacing; ChordArea=chordArea;
            EffectiveInertia=FinitePositive(inertia,"Ieff"); ShearStiffness=FinitePositive(shear,"Sv");
            Efficiency=efficiency; IsBattened=battened; Lacing=lacing; DiagonalLength=diagonal;
            EulerForce=FinitePositive(Math.PI*Math.PI*e*inertia/(length*length),"NcrEuler");
            CriticalForce=FinitePositive(1/(1/EulerForce+1/shear),"NcrIncludingShear");
        }

        public static Ec3BuiltUpColumn Laced(double e,double length,int panels,double chordSpacing,
            double chordArea,double diagonalArea,LacingSystem system,double postArea=0)
        {
            Geometry(e,length,panels,chordSpacing,chordArea);
            Positive(diagonalArea,nameof(diagonalArea));
            if(!Enum.IsDefined(typeof(LacingSystem),system))throw new ArgumentOutOfRangeException(nameof(system));
            if(system==LacingSystem.WithPosts)Positive(postArea,nameof(postArea));
            else if(postArea!=0)throw new ArgumentException("Post area only belongs to the WithPosts system.",nameof(postArea));
            double a=length/panels,h=chordSpacing;
            double run=system==LacingSystem.ZigZag?a/2:a;
            double d=Math.Sqrt(h*h+run*run);
            double sv=2*e*diagonalArea*a*h*h/(d*d*d);
            if(system==LacingSystem.ZigZag)sv/=2;
            if(system==LacingSystem.WithPosts)sv/=1+diagonalArea*h*h*h/(postArea*d*d*d);
            return new Ec3BuiltUpColumn(e,length,panels,h,chordArea,.5*h*h*chordArea,sv,0,false,system,d);
        }

        public static Ec3BuiltUpColumn Battened(double e,double length,int panels,double chordSpacing,
            double chordArea,double chordInertia,double battenInertia)
        {
            Geometry(e,length,panels,chordSpacing,chordArea);
            Positive(chordInertia,nameof(chordInertia));Positive(battenInertia,nameof(battenInertia));
            double a=length/panels,h=chordSpacing;
            double i1=.5*h*h*chordArea+2*chordInertia;
            double lambda=length/Math.Sqrt(i1/(2*chordArea));
            double mu=lambda<=75?1:lambda>=150?0:2-lambda/75;
            double sv=24*e*chordInertia/(a*a*(1+chordInertia*h/(battenInertia*a)));
            sv=Math.Min(sv,2*Math.PI*Math.PI*e*chordInertia/(a*a));
            return new Ec3BuiltUpColumn(e,length,panels,h,chordArea,.5*h*h*chordArea+2*mu*chordInertia,sv,mu,true,null,0);
        }

        /// <summary>Both bow signs are enveloped; compression N >= 0. The sign of
        /// the first-order moment therefore does not reduce the imperfection.
        /// UniformTransverseLoad uses the SCI/CTICM SBE_SS6 shear correction;
        /// Sinusoidal uses 6.70. Other load/moment diagrams are not represented.</summary>
        public BuiltUpColumnResponse Analyze(double compression,double firstOrderMidspanMoment=0,
            BuiltUpLoadDiagram diagram=BuiltUpLoadDiagram.Sinusoidal)
        {
            Nonnegative(compression,nameof(compression));
            if(double.IsNaN(firstOrderMidspanMoment)||double.IsInfinity(firstOrderMidspanMoment))throw new ArgumentOutOfRangeException(nameof(firstOrderMidspanMoment));
            if(!Enum.IsDefined(typeof(BuiltUpLoadDiagram),diagram))throw new ArgumentOutOfRangeException(nameof(diagram));
            double denominator=1-compression/EulerForce-compression/ShearStiffness;
            // No post-critical negative moment, infinity or false resistance pass.
            if(compression>=CriticalForce || denominator<=0)
                return new BuiltUpColumnResponse(this,BuiltUpAnalysisStatus.Unstable,compression,denominator,null,null,null,null,null,null,null,null);
            double initial=compression*Length/500,m1=Math.Abs(firstOrderMidspanMoment);
            double moment=(initial+m1)/denominator;
            double shear=(Math.PI*initial+(diagram==BuiltUpLoadDiagram.Sinusoidal?Math.PI:4)*m1)/(denominator*Length);
            double increment=moment*ChordSpacing*ChordArea/(2*EffectiveInertia);
            double nmax=compression/2+increment,nmin=compression/2-increment;
            double diagonal=IsBattened?0:shear*DiagonalLength/(Planes*ChordSpacing);
            // Conservative internal-post envelope; the middle Figure6.9 system
            // neglects post strain energy, not the need to design its posts.
            double post=!IsBattened && Lacing!=LacingSystem.ZigZag?2*shear/Planes:0;
            double chordMoment=IsBattened?shear*PanelLength/4:0;
            double battenMoment=IsBattened?shear*PanelLength/(2*Planes):0;
            double battenShear=IsBattened?shear*PanelLength/(Planes*ChordSpacing):0;
            foreach(double value in new[]{moment,shear,nmax,diagonal,post,chordMoment,battenMoment,battenShear})
                if(double.IsNaN(value)||double.IsInfinity(value))throw new ArithmeticException("Built-up analysis exceeds finite numerical range.");
            return new BuiltUpColumnResponse(this,BuiltUpAnalysisStatus.Evaluated,compression,denominator,moment,shear,nmax,nmin,diagonal,post,chordMoment,
                new BattenActions(battenMoment,battenShear));
        }

        /// <summary>Table 6.9, first row only (Figure 6.12, bolts/welds through
        /// packing plates or chords in contact). Geometry/contact suitability is
        /// a prerequisite; passing this spacing limit is not a buckling check.</summary>
        public static bool MayIgnoreShearDeformation(double connectionPitch,double minimumChordRadius)
        {
            Positive(connectionPitch,nameof(connectionPitch));Positive(minimumChordRadius,nameof(minimumChordRadius));
            return connectionPitch/minimumChordRadius<=15;
        }

        private static void Geometry(double e,double length,int panels,double h,double area)
        {
            Positive(e,nameof(e));Positive(length,nameof(length));Positive(h,nameof(h));Positive(area,nameof(area));
            if(panels<3)throw new ArgumentOutOfRangeException(nameof(panels),"At least three equal modules are required by 6.4.1(2).");
        }
        private static void Positive(double v,string n)=>Ec3AngleCompression.Positive(v,n);
        private static void Nonnegative(double v,string n){if(!(v>=0)||double.IsInfinity(v))throw new ArgumentOutOfRangeException(n);}
        private static double FinitePositive(double v,string n){if(!(v>0)||double.IsInfinity(v))throw new ArithmeticException(n+" must be finite and positive.");return v;}
    }

    public sealed class BattenActions
    {
        /// <summary>One batten and its end connection, Nmm.</summary>
        public double Moment { get; }
        /// <summary>One batten and its end connection, N.</summary>
        public double Shear { get; }
        internal BattenActions(double moment,double shear){Moment=moment;Shear=shear;}
    }

    public sealed class BuiltUpColumnResponse
    {
        public Ec3BuiltUpColumn Column { get; }
        public BuiltUpAnalysisStatus Status { get; }
        public double Compression { get; }
        public double AmplificationDenominator { get; }
        public double? Moment { get; }
        public double? Shear { get; }
        public double? ChordCompression { get; }
        /// <summary>Signed axial force in the less compressed chord; negative = tension.</summary>
        public double? OtherChordForce { get; }
        /// <summary>Absolute axial envelope, same compression/tension magnitude.</summary>
        public double? DiagonalForce { get; }
        public double? PostForce { get; }
        public double? LocalChordMoment { get; }
        public BattenActions Batten { get; }
        internal BuiltUpColumnResponse(Ec3BuiltUpColumn column,BuiltUpAnalysisStatus status,double n,double den,
            double? m,double? v,double? nc,double? other,double? nd,double? np,double? mc,BattenActions batten)
        {Column=column;Status=status;Compression=n;AmplificationDenominator=den;Moment=m;Shear=v;ChordCompression=nc;OtherChordForce=other;DiagonalForce=nd;PostForce=np;LocalChordMoment=mc;Batten=batten;}

        /// <summary>All component design checks still required. Demands are computed;
        /// no capacity is inferred from the global geometric section.</summary>
        public IReadOnlyList<string> RequiredChecks => Array.AsReadOnly(Column.IsBattened
            ? new[]{"ChordInPlaneNMVAndBuckling","ChordOutOfPlaneAndTorsion","BattenMV","BattenConnections","EndPanelsAndLoadIntroduction"}
            : new[]{"ChordCompressionInPlane","ChordOutOfPlaneAndTorsion","ChordTensionIfPresent","DiagonalCompression","DiagonalTension","LacingConnections","EndPanelsAndLoadIntroduction"}
                .Concat(Column.Lacing!=LacingSystem.ZigZag?new[]{"PostCompression","PostTension","PostConnections"}:new string[0]).ToArray());
    }

    /// <summary>Individual axial checks on a laced column. Supplied resistances must
    /// be design values for the actual buckling lengths/restraints/net section/joints.
    /// This result intentionally has no whole-member Passed flag: connections, load
    /// introduction and missing mechanisms must also be checked.</summary>
    public static class Ec3BuiltUpAxialChecks
    {
        public static double CompressionUtilization(double force,double resistance)
        {
            if(!(force>=0)||double.IsInfinity(force))throw new ArgumentOutOfRangeException(nameof(force));
            Ec3AngleCompression.Positive(resistance,nameof(resistance));
            double ratio=force/resistance;
            if(double.IsInfinity(ratio))throw new ArithmeticException("Utilization exceeds numerical range.");
            return ratio;
        }

        public static double ChordUtilization(BuiltUpColumnResponse response,double inPlaneResistance,double outOfPlaneAndTorsionResistance)
        {
            if(response==null)throw new ArgumentNullException(nameof(response));
            if(response.Status!=BuiltUpAnalysisStatus.Evaluated)throw new InvalidOperationException("Column is unstable.");
            if(response.Column.IsBattened)throw new NotSupportedException("Battened chords require N-M-V interaction and member buckling verification.");
            return Math.Max(CompressionUtilization(response.ChordCompression.Value,inPlaneResistance),
                CompressionUtilization(response.ChordCompression.Value,outOfPlaneAndTorsionResistance));
        }
    }
}
