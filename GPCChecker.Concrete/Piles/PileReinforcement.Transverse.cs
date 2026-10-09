using System;
using System.Linq;

namespace GPC.Checkers.Concrete.Piles
{
    public sealed class PileTransversePoint
    {
        public double Depth { get; set; } // m
        public double X { get; set; } // mm, projection of the centreline
        public bool Front { get; set; }
    }
    public sealed class PileTransverseDetail
    {
        public PileSeismicLinks Kind { get; set; }
        public double Start { get; set; }
        public double End { get; set; }
        public double Diameter { get; set; }
        public double Radius { get; set; }
        public double MaximumPitch { get; set; }
        public double ActualPitch { get; set; }
        public int Count { get; set; }
        public int Turns { get; set; }
        public double GeometricLength { get; set; } // m; excludes closures, end anchorage and joints
        public double[] Stations { get; set; } = new double[0];
        public PileTransversePoint[] Projection { get; set; } = new PileTransversePoint[0];
        public string Note => "Geometria nominale all'asse; lunghezza geometrica senza ganci, chiusure, ancoraggi, giunti o sfridi. Non è una lunghezza di taglio esecutiva. La spirale non è equiparata alle staffe singole nel verificatore.";
    }
    public sealed partial class PileReinforcement
    {
        public static PileTransverseDetail TransverseDetail(PileSeismicLinks kind,double start,double end,double diameterMm,double coverMm,double barDiameterMm,double maximumPitchMm,bool last)
        {
            if(!Enum.IsDefined(typeof(PileSeismicLinks),kind)||start<0)throw new ArgumentException("Tipo o quota armatura trasversale non validi.");
            double radius=CircularLinkRadius(diameterMm,coverMm,barDiameterMm);
            var stations=LinkStations(start,end,maximumPitchMm,last);
            var full=LinkStations(start,end,maximumPitchMm,true);int turns=full.Length-1;
            var r=new PileTransverseDetail{Kind=kind,Start=start,End=end,Diameter=barDiameterMm,Radius=radius,MaximumPitch=maximumPitchMm,ActualPitch=(end-start)*1000/turns};
            if(kind==PileSeismicLinks.Unspecified)return r;
            if(kind==PileSeismicLinks.SingleHoops)
            {
                r.Stations=stations;r.Count=stations.Length;r.GeometricLength=stations.Length*2*Math.PI*radius/1000;return r;
            }
            r.Count=1;r.Turns=turns;
            double circumference=2*Math.PI*radius/1000;
            r.GeometricLength=Math.Sqrt(Math.Pow(turns*circumference,2)+Math.Pow(end-start,2));
            // 24 samples per turn describe the geometry, independently of the drawing scale or FEM mesh.
            r.Projection=Enumerable.Range(0,checked(turns*24+1)).Select(i=>new PileTransversePoint{Depth=start+(end-start)*i/(turns*24),X=radius*Math.Cos(i*Math.PI/12),Front=Math.Sin((i-.5)*Math.PI/12)>=0}).ToArray();
            return r;
        }
    }
}
