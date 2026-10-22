using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Steel.Inputs;
using GPC.Checkers.Steel.ModelAdapters;
using GPC.Checkers.Steel.Selection;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

namespace GPC.Checkers.Steel.EffectiveSections
{
    public enum EffectivePlateKind { Internal, Outstand }
    /// <summary>Normative decomposition of a Model section; no parallel Model geometry.
    /// For an outstand, Start is supported. Coordinates are geometric section X/Y, mm.</summary>
    public sealed class EffectiveSectionPlate
    {
        public string Id { get; }
        public EffectivePlateKind Kind { get; }
        public double StartX { get; }
        public double StartY { get; }
        public double EndX { get; }
        public double EndY { get; }
        public double Thickness { get; }
        public double Length { get; }
        public double BucklingWidth { get; }
        public double DesignWidth { get; }
        internal EffectiveSectionPlate(string id,EffectivePlateKind kind,double x1,double y1,double x2,double y2,double t,double? bucklingWidth=null,double? designWidth=null)
        {
            Id=id;Kind=kind;StartX=x1;StartY=y1;EndX=x2;EndY=y2;Thickness=t;
            Length=Math.Sqrt((x2-x1)*(x2-x1)+(y2-y1)*(y2-y1));BucklingWidth=bucklingWidth??Length;
            DesignWidth=designWidth??BucklingWidth;
            SteelInputValidation.Positive(Length,"flat plate length");SteelInputValidation.Positive(t,"plate thickness");
            SteelInputValidation.Positive(BucklingWidth,"buckling width");
            if(BucklingWidth<Length-1e-10)throw new ArgumentException("Buckling width cannot be shorter than the mapped flat strip.");
            SteelInputValidation.Positive(DesignWidth,"design width");
            if(DesignWidth<Length-1e-10||DesignWidth>BucklingWidth+1e-10)throw new ArgumentException("Design width must lie between the physical and buckling widths.");
        }
    }

    /// <summary>Geometric effective section obtained by subtracting non-overlapping flat
    /// rectangles from the exact gross Model integrals. Fillets/corners remain in gross geometry.
    /// Area mm2, centroid mm, inertias mm4. No stiffness or torsional properties are inferred.</summary>
    public sealed class SteelEffectiveGeometry
    {
        public SteelSectionInput Section { get; }
        public IReadOnlyList<EffectiveSectionPlate> Plates { get; }
        public double GrossX { get; }
        public double GrossY { get; }
        public double GrossIxx { get; }
        public double GrossIyy { get; }
        public double GrossIxy { get; }
        public double MinX { get; }
        public double MaxX { get; }
        public double MinY { get; }
        public double MaxY { get; }
        private SteelEffectiveGeometry(SteelSectionInput input,IEnumerable<EffectiveSectionPlate> plates)
        {
            Section=input;Plates=Array.AsReadOnly(plates.ToArray());var s=input.CreateSectionCopy();
            GrossX=s.Centroid.X;GrossY=s.Centroid.Y;GrossIxx=s.Jxx;GrossIyy=s.Jyy;GrossIxy=s.Jxy;
            var points=s.SectionShape is SectionBuiltUp assembled
                ?assembled.Parts.SelectMany(part=>part.Section.GetSectionPoints().Select(v=>part.Place(v.X,v.Y))).ToArray()
                :s.SectionShape.GetSectionPoints().ToArray();
            MinX=points.Min(p=>p.X);MaxX=points.Max(p=>p.X);MinY=points.Min(p=>p.Y);MaxY=points.Max(p=>p.Y);
        }
        public static SteelEffectiveGeometry Create(SteelSectionInput input)
        {
            if(input==null)throw new ArgumentNullException(nameof(input));
            var s=input.CreateSectionCopy();SteelMethodSelector.ValidateGeometry(s);
            if(SteelMethodSelector.ColdFormedOpen(s))throw new NotSupportedException("Cold-formed open sections require distortional/local qualification to EN1993-1-3.");
            var shape=s.SectionShape;var type=shape.GetType();var p=new List<EffectiveSectionPlate>();
            void Add(string id,EffectivePlateKind kind,double x1,double y1,double x2,double y2,double t,double? b=null,double? designWidth=null)
                =>p.Add(new EffectiveSectionPlate(id,kind,x1,y1,x2,y2,t,b,designWidth));
            const EffectivePlateKind I=EffectivePlateKind.Internal,O=EffectivePlateKind.Outstand;
            if(type==typeof(SectionBuiltUp)) {
                var pair=(SectionBuiltUp)shape;ModelBuiltUpGeometry.Create(pair);
                for(int index=0;index<pair.Parts.Count;index++) {
                    var part=pair.Parts[index];
                    var component=new SteelSectionInput(new SteelSection(part.Section,s.SteelMaterial,s.SectionType,s.FormedType),input.Unperforated);
                    foreach(var plate in Create(component).Plates) {
                        var start=part.Place(plate.StartX,plate.StartY);var end=part.Place(plate.EndX,plate.EndY);
                        // Reflection preserves the physical supported edge: never sort endpoints.
                        Add("Chord"+(index+1)+"."+plate.Id,plate.Kind,start.X,start.Y,end.X,end.Y,
                            plate.Thickness,plate.BucklingWidth,plate.DesignWidth);
                    }
                }
            } else if(type==typeof(SectionH)) {
                var h=(SectionH)shape;double x=Math.Max(h.LenghtTopFlange,h.LenghtBottomFlange)/2;
                Add("Web",I,x,h.ThicknessBottomFlange+h.R,x,h.Height-h.ThicknessTopFlange-h.R,h.ThicknessWeb);
                void Flange(string id,double b,double t,double y) {
                    Add(id+"Left",O,x-h.ThicknessWeb/2-h.R,y,x-b/2,y,t);
                    Add(id+"Right",O,x+h.ThicknessWeb/2+h.R,y,x+b/2,y,t);
                }
                Flange("Top",h.LenghtTopFlange,h.ThicknessTopFlange,h.Height-h.ThicknessTopFlange/2);
                Flange("Bottom",h.LenghtBottomFlange,h.ThicknessBottomFlange,h.ThicknessBottomFlange/2);
            } else if(type==typeof(SectionT)) {
                var t=(SectionT)shape;double x=t.LenghtFlange/2,y=t.Height-t.ThicknessFlange/2;
                Add("Stem",O,x,t.HeightWeb-t.R,x,0,t.ThicknessWeb);
                Add("FlangeLeft",O,x-t.ThicknessWeb/2-t.R,y,0,y,t.ThicknessFlange);
                Add("FlangeRight",O,x+t.ThicknessWeb/2+t.R,y,t.LenghtFlange,y,t.ThicknessFlange);
            } else if(type==typeof(SectionC)) {
                var c=(SectionC)shape;
                if(c.R2!=0)throw new NotSupportedException("Channel toe radii require explicit clipped removal geometry.");
                Add("Web",I,c.ThicknessWeb/2,c.ThicknessBottom+c.R1,c.ThicknessWeb/2,c.Height-c.ThicknessTop-c.R1,c.ThicknessWeb);
                Add("Top",O,c.ThicknessWeb+c.R1,c.Height-c.ThicknessTop/2,c.LengthTop,c.Height-c.ThicknessTop/2,c.ThicknessTop);
                Add("Bottom",O,c.ThicknessWeb+c.R1,c.ThicknessBottom/2,c.LengthBottom,c.ThicknessBottom/2,c.ThicknessBottom);
            } else if(type==typeof(SectionL)) {
                var l=(SectionL)shape;
                if(l.R2!=0)throw new NotSupportedException("Angle toe radii require explicit clipped removal geometry.");
                // EN1993-1-5 4.4(2): h (long leg) controls both leg slendernesses.
                // Inactive area still uses each complete leg width (SCI P363 A-11).
                double h=Math.Max(l.HorizontalLegLength,l.VerticalLegLength);
                Add("Horizontal",O,l.VerticalLegThickness+l.R,l.HorizontalLegThickness/2,l.HorizontalLegLength,l.HorizontalLegThickness/2,l.HorizontalLegThickness,h,l.HorizontalLegLength);
                Add("Vertical",O,l.VerticalLegThickness/2,l.HorizontalLegThickness+l.R,l.VerticalLegThickness/2,l.VerticalLegLength,l.VerticalLegThickness,h,l.VerticalLegLength);
            } else if(type==typeof(SectionRHSRoundedCorners)) {
                var r=(SectionRHSRoundedCorners)shape;double t=r.Thickness;
                // Use only rectangles entirely inside the straight constant-thickness
                // wall. The larger classification width conservatively controls rho.
                double corner=Math.Max(r.OutsideRadius,t+r.InsideRadius),calculationCorner=Math.Min(r.OutsideRadius,t+r.InsideRadius);
                Add("Top",I,corner,r.Height-t/2,r.Base-corner,r.Height-t/2,t,Math.Max(r.Base-3*t,r.Base-2*calculationCorner));
                Add("Bottom",I,corner,t/2,r.Base-corner,t/2,t,Math.Max(r.Base-3*t,r.Base-2*calculationCorner));
                Add("Left",I,t/2,corner,t/2,r.Height-corner,t,Math.Max(r.Height-3*t,r.Height-2*calculationCorner));
                Add("Right",I,r.Base-t/2,corner,r.Base-t/2,r.Height-corner,t,Math.Max(r.Height-3*t,r.Height-2*calculationCorner));
            } else if(type==typeof(SectionRHS)&&((SectionRHS)shape).R==0) {
                var r=(SectionRHS)shape;
                Add("Top",I,r.ThicknessWebLeft,r.Height-r.ThicknessTop/2,r.Base-r.ThicknessWebRight,r.Height-r.ThicknessTop/2,r.ThicknessTop);
                Add("Bottom",I,r.ThicknessWebLeft,r.ThicknessBottom/2,r.Base-r.ThicknessWebRight,r.ThicknessBottom/2,r.ThicknessBottom);
                Add("Left",I,r.ThicknessWebLeft/2,r.ThicknessBottom,r.ThicknessWebLeft/2,r.Height-r.ThicknessTop,r.ThicknessWebLeft);
                Add("Right",I,r.Base-r.ThicknessWebRight/2,r.ThicknessBottom,r.Base-r.ThicknessWebRight/2,r.Height-r.ThicknessTop,r.ThicknessWebRight);
            } else throw new NotSupportedException("Effective flat-plate geometry requires exact H/T/C/L/RHS or identical mirrored 2C/2L. CHS needs a shell method.");
            return new SteelEffectiveGeometry(input,p);
        }
        public EffectiveSectionState Gross()=>WithWidths(Plates.SelectMany(p=>new[]{p.Length/2,p.Length/2}).ToArray());
        /// <summary>Two retained lengths per plate, Start then End, in mm. Does not assert
        /// their normative validity. Used by the effective-width solver and geometry tests.</summary>
        public EffectiveSectionState WithWidths(IReadOnlyList<double> widths)
        {
            if(widths==null||widths.Count!=2*Plates.Count)throw new ArgumentException("Two retained widths per plate required.");
            double area=Section.Area,sx=0,sy=0,ixx=GrossIxx,iyy=GrossIyy,ixy=GrossIxy;
            var owned=widths.ToArray();
            for(int j=0;j<Plates.Count;j++) {
                var p=Plates[j];double start=owned[2*j],end=owned[2*j+1];
                SteelInputValidation.Finite(start,"retained width");SteelInputValidation.Finite(end,"retained width");
                if(start<0||end<0||start+end>p.Length+1e-10*p.Length)throw new ArgumentOutOfRangeException(nameof(widths));
                double length=Math.Max(0,p.Length-start-end);if(length==0)continue;
                double ux=(p.EndX-p.StartX)/p.Length,uy=(p.EndY-p.StartY)/p.Length;
                double x=p.StartX+ux*(start+length/2)-GrossX,y=p.StartY+uy*(start+length/2)-GrossY;
                double a=length*p.Thickness;
                area-=a;sx-=a*x;sy-=a*y;
                ixx-=a*((length*length*uy*uy+p.Thickness*p.Thickness*ux*ux)/12+y*y);
                iyy-=a*((length*length*ux*ux+p.Thickness*p.Thickness*uy*uy)/12+x*x);
                ixy-=a*((length*length-p.Thickness*p.Thickness)*ux*uy/12+x*y);
            }
            SteelInputValidation.Positive(area,"effective area");double dx=sx/area,dy=sy/area;
            return new EffectiveSectionState(this,owned,area,GrossX+dx,GrossY+dy,ixx-area*dy*dy,iyy-area*dx*dx,ixy-area*dx*dy);
        }
    }

    public sealed class EffectiveSectionState
    {
        public SteelEffectiveGeometry Geometry { get; }
        public IReadOnlyList<double> Widths { get; }
        public double Area { get; }
        public double X { get; }
        public double Y { get; }
        public double Ixx { get; }
        public double Iyy { get; }
        public double Ixy { get; }
        public double CentroidShiftX=>X-Geometry.GrossX;
        public double CentroidShiftY=>Y-Geometry.GrossY;
        public double PrincipalI1 { get; }
        public double PrincipalI2 { get; }
        public double PrincipalAxisAngle { get; }
        internal EffectiveSectionState(SteelEffectiveGeometry geometry,double[] widths,double area,double x,double y,double ixx,double iyy,double ixy)
        {
            foreach(var value in new[]{area,ixx,iyy})SteelInputValidation.Positive(value,"effective section property");
            foreach(var value in new[]{x,y,ixy})SteelInputValidation.Finite(value,"effective section property");
            if(1-ixy/ixx*(ixy/iyy)<=1e-12)throw new ArithmeticException("Effective inertia tensor is singular or ill-conditioned.");
            Geometry=geometry;Widths=Array.AsReadOnly(widths);Area=area;X=x;Y=y;Ixx=ixx;Iyy=iyy;Ixy=ixy;
            double scale=Math.Max(ixx,iyy),nx=ixx/scale,ny=iyy/scale,nxy=ixy/scale;
            double radius=Math.Sqrt((nx-ny)*(nx-ny)+4*nxy*nxy);
            PrincipalI1=scale*((nx+ny+radius)/2);
            PrincipalI2=scale*((nx*ny-nxy*nxy)/((nx+ny+radius)/2));
            SteelInputValidation.Positive(PrincipalI1,"principal effective inertia");SteelInputValidation.Positive(PrincipalI2,"principal effective inertia");
            PrincipalAxisAngle=.5*Math.Atan2(-2*ixy,ixx-iyy);
        }
        /// <summary>Normal stress at geometric X/Y. N tension positive; M1/M2 right-hand
        /// moments about gross principal axes, applied at the gross centroid. Includes
        /// -N*dY and +N*dX in the moments about the effective centroid. MPa.</summary>
        public double Stress(double x,double y,double n,double m1,double m2)
        {
            foreach(var v in new[]{x,y,n,m1,m2})SteelInputValidation.Finite(v,"stress argument");
            double c=Math.Cos(Geometry.Section.PrincipalAxisAngle),s=Math.Sin(Geometry.Section.PrincipalAxisAngle);
            double mx=c*m1-s*m2-n*CentroidShiftY,my=s*m1+c*m2+n*CentroidShiftX;
            double d=1-Ixy/Ixx*(Ixy/Iyy);
            double a=(-my/Iyy-mx/Ixx*(Ixy/Iyy))/d,b=(mx/Ixx+my/Iyy*(Ixy/Ixx))/d;
            double result=n/Area+a*(x-X)+b*(y-Y);SteelInputValidation.Finite(result,"normal stress");return result;
        }
        /// <summary>Conservative stress bound over the gross bounding rectangle, including
        /// any deleted/non-material corners. No claim that the controlling point is material.</summary>
        public double MaximumAbsoluteStress(double n,double m1,double m2)
            =>new[]{Geometry.MinX,Geometry.MaxX}.SelectMany(x=>new[]{Geometry.MinY,Geometry.MaxY}.Select(y=>Math.Abs(Stress(x,y,n,m1,m2)))).Max();
        /// <summary>Elastic modulus about the original principal axis, using the full
        /// coupled inertia tensor and a conservative gross bounding rectangle. mm3.</summary>
        public double Modulus1=>1/MaximumAbsoluteStress(0,1,0);
        public double Modulus2=>1/MaximumAbsoluteStress(0,0,1);
    }
}
