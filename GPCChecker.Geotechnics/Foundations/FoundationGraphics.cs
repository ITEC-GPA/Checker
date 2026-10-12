namespace GPC.Checkers.Geotechnics.Foundations;

/// <summary>Coordinates in mm, pressures in MPa. X/Y in plan; Y is depth in a mechanism section.</summary>
public sealed class FoundationPlotPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Pressure { get; set; }
    public FoundationPlotPoint() { }
    internal FoundationPlotPoint(double x,double y,double p=0) { X=x;Y=y;Pressure=p; }
}

public sealed class FoundationMechanism
{
    public string Axis { get; internal set; } = "";
    public double EffectiveWidth { get; internal set; }
    public double Centre { get; internal set; }
    public List<FoundationPlotPoint> Wedge { get; } = new();
    public List<FoundationPlotPoint> Left { get; } = new();
    public List<FoundationPlotPoint> Right { get; } = new();
    public List<FoundationPlotPoint> Fan { get; } = new();
}

public sealed class FoundationGraphicsResult
{
    public double Width { get; internal set; }
    public double Length { get; internal set; }
    public bool Circular { get; internal set; }
    public double EffectiveWidthX { get; internal set; }
    public double EffectiveLengthY { get; internal set; }
    public double CentreX { get; internal set; }
    public double CentreY { get; internal set; }
    public double GeometricArea { get; internal set; }
    public double CompressedArea { get; internal set; }
    public double MinimumPressure { get; internal set; }
    public double MaximumPressure { get; internal set; }
    public double MeanPressure { get; internal set; }
    public double EquilibriumError { get; internal set; }
    public string ContactDescription { get; internal set; } = "";
    public string MechanismDescription { get; internal set; } = "Schema di Prandtl per striscia su terreno privo di peso: cuneo, ventaglio logaritmico e zona passiva. Riferimento illustrativo, non superficie critica calcolata per il metodo selezionato; non rappresenta effetti di H, tridimensionalità, peso del terreno o profondità di posa.";
    public List<FoundationPlotPoint> ContactPolygon { get; } = new();
    public List<FoundationPlotPoint> Grid { get; } = new();
    public List<FoundationPlotPoint> Corners { get; } = new();
    public List<FoundationPlotPoint> SectionX { get; } = new();
    public List<FoundationPlotPoint> SectionY { get; } = new();
    public FoundationMechanism MechanismX { get; internal set; } = new();
    public FoundationMechanism MechanismY { get; internal set; } = new();
}

/// <summary>
/// Conventional affine contact pressure, no tensile reaction: p=max(0,a+b x+c y).
/// Exact polygon moments enforce N, Mx, My; this is not an elastic half-space solution.
/// A' for bearing capacity remains separate from the compressed contact polygon.
/// Prandtl coordinates are illustrative, not an optimization of the actual failure surface.
/// </summary>
public static class FoundationGraphics
{
    internal static FoundationGraphicsResult Create(ShallowFoundationInput x,ShallowFoundationResult bearing)
    {
        double b=x.Width,l=x.Shape==FoundationShape.Strip?1000:x.Shape==FoundationShape.Circle?b:x.Length;
        double ex=bearing.EccentricityX,ey=bearing.EccentricityY;
        var r=new FoundationGraphicsResult { Width=b,Length=l,Circular=x.Shape==FoundationShape.Circle,
            EffectiveWidthX=b-2*Math.Abs(ex),EffectiveLengthY=l-2*Math.Abs(ey),CentreX=ex,CentreY=ey,
            GeometricArea=x.Shape==FoundationShape.Circle?Math.PI*b*b/4:b*l };
        double mean=x.Normal/r.GeometricArea;
        r.MeanPressure=mean;
        double[] p={1,12*ex/b,12*ey/l};
        var target=new[]{1d,ex/b,ey/l};
        List<FoundationPlotPoint> polygon;
        if(r.Circular)
        {
            if(ex!=0 || ey!=0)throw new NotSupportedException("Contatto circolare eccentrico non implementato.");
            polygon=Enumerable.Range(0,96).Select(i=>new FoundationPlotPoint(.5*Math.Cos(i*Math.PI/48),.5*Math.Sin(i*Math.PI/48))).ToList();
            r.CompressedArea=r.GeometricArea;
        }
        else
        {
            // Normalize both coordinates and pressure: the tolerance is independent of dimensions and units.
            bool converged=false;double error=double.PositiveInfinity;
            for(int iteration=0;iteration<100;iteration++)
            {
                polygon=Clip(p);var j=Moments(polygon);var residual=Residual(j,p,target);error=Norm(residual);
                if(error<1e-11){converged=true;break;}
                var step=Solve(j,residual);bool accepted=false;
                for(double scale=1;scale>=1d/1048576;scale/=2)
                {
                    var trial=new[]{p[0]-scale*step[0],p[1]-scale*step[1],p[2]-scale*step[2]};
                    if(Norm(Residual(Moments(Clip(trial)),trial,target))<error) {p=trial;accepted=true;break;}
                }
                if(!accepted)break;
            }
            if(!converged)throw new ArithmeticException("Distribuzione delle pressioni non convergente: non viene mostrata una soluzione approssimata non equilibrata.");
            polygon=Clip(p);r.EquilibriumError=error;r.CompressedArea=Moments(polygon)[0,0]*b*l;
        }
        double Pressure(double u,double v)=>Math.Max(0,p[0]+p[1]*u+p[2]*v)*mean;
        foreach(var v in polygon)r.ContactPolygon.Add(new(v.X*b,v.Y*l,Pressure(v.X,v.Y)));
        if(!r.Circular)foreach(var v in Square())r.Corners.Add(new(v.X*b,v.Y*l,Pressure(v.X,v.Y)));
        r.MinimumPressure=r.Circular?mean:r.Corners.Min(v=>v.Pressure);
        r.MaximumPressure=r.Circular?mean:r.Corners.Max(v=>v.Pressure);
        r.ContactDescription=r.Circular?"Pressione uniforme: fondazione circolare con carico centrato."
            :"Distribuzione convenzionale affine, senza trazione: equilibrio di N, Mx, My su p=max(0,a+bx+cy). Non è una soluzione del semispazio elastico né una verifica dei cedimenti.";
        for(int iy=0;iy<=32;iy++)for(int ix=0;ix<=32;ix++)
        {
            double u=-.5+ix/32d,v=-.5+iy/32d;
            if(!r.Circular || u*u+v*v<=.25)r.Grid.Add(new(u*b,v*l,Pressure(u,v)));
        }
        for(int i=0;i<=80;i++)
        {
            double t=-.5+i/80d;
            r.SectionX.Add(new(t*b,ey,Pressure(t,ey/l)));
            r.SectionY.Add(new(ex,t*l,Pressure(ex/b,t)));
        }
        r.MechanismX=Mechanism("X",r.EffectiveWidthX,ex,bearing.AdoptedFrictionAngle);
        r.MechanismY=Mechanism("Y",r.EffectiveLengthY,ey,bearing.AdoptedFrictionAngle);
        return r;
    }
    static List<FoundationPlotPoint> Square()=>new(){new(-.5,-.5),new(.5,-.5),new(.5,.5),new(-.5,.5)};
    static List<FoundationPlotPoint> Clip(double[] p)
    {
        var output=new List<FoundationPlotPoint>();var square=Square();
        double Level(FoundationPlotPoint v)=>p[0]+p[1]*v.X+p[2]*v.Y;
        for(int i=0;i<4;i++)
        {
            var a=square[i];var b=square[(i+1)%4];double fa=Level(a),fb=Level(b);
            if(fa>=0)output.Add(a);
            if((fa>0&&fb<0)||(fa<0&&fb>0))
            {double t=fa/(fa-fb);output.Add(new(a.X+t*(b.X-a.X),a.Y+t*(b.Y-a.Y)));}
        }
        return output;
    }
    // Green's theorem: exact area, first and second moments of a counterclockwise polygon.
    static double[,] Moments(List<FoundationPlotPoint> polygon)
    {
        double a=0,x=0,y=0,xx=0,xy=0,yy=0;
        for(int i=0;i<polygon.Count;i++)
        {
            var p=polygon[i];var q=polygon[(i+1)%polygon.Count];double cross=p.X*q.Y-q.X*p.Y;
            a+=cross/2;x+=(p.X+q.X)*cross/6;y+=(p.Y+q.Y)*cross/6;
            xx+=(p.X*p.X+p.X*q.X+q.X*q.X)*cross/12;
            yy+=(p.Y*p.Y+p.Y*q.Y+q.Y*q.Y)*cross/12;
            xy+=(2*p.X*p.Y+p.X*q.Y+q.X*p.Y+2*q.X*q.Y)*cross/24;
        }
        return new[,]{{a,x,y},{x,xx,xy},{y,xy,yy}};
    }
    static double[] Residual(double[,] j,double[] p,double[] target)
        =>Enumerable.Range(0,3).Select(i=>j[i,0]*p[0]+j[i,1]*p[1]+j[i,2]*p[2]-target[i]).ToArray();
    static double Norm(double[] a)=>Math.Sqrt(a.Sum(v=>v*v));
    static double[] Solve(double[,] j,double[] residual)
    {
        var a=new double[3,4];for(int i=0;i<3;i++){for(int k=0;k<3;k++)a[i,k]=j[i,k];a[i,3]=residual[i];}
        for(int k=0;k<3;k++)
        {
            int pivot=k;for(int i=k+1;i<3;i++)if(Math.Abs(a[i,k])>Math.Abs(a[pivot,k]))pivot=i;
            if(Math.Abs(a[pivot,k])<1e-24)throw new ArithmeticException("Area compressa numericamente degenere: impossibile ricostruire le pressioni.");
            for(int c=k;c<4;c++)(a[k,c],a[pivot,c])=(a[pivot,c],a[k,c]);
            double d=a[k,k];for(int c=k;c<4;c++)a[k,c]/=d;
            for(int i=0;i<3;i++)if(i!=k){double f=a[i,k];for(int c=k;c<4;c++)a[i,c]-=f*a[k,c];}
        }
        return new[]{a[0,3],a[1,3],a[2,3]};
    }
    static FoundationMechanism Mechanism(string axis,double width,double centre,double phi)
    {
        // Weightless Prandtl reference: alpha=pi/4+phi/2; fan rotation pi/2; r=r0 exp(theta tan(phi)).
        double alpha=Math.PI/4+phi/2,beta=Math.PI/4-phi/2,r0=width/(2*Math.Cos(alpha));
        var m=new FoundationMechanism { Axis=axis,EffectiveWidth=width,Centre=centre };
        m.Wedge.AddRange(new[]{new FoundationPlotPoint(centre-width/2,0),new FoundationPlotPoint(centre,width/2*Math.Tan(alpha)),new FoundationPlotPoint(centre+width/2,0)});
        for(int i=0;i<=48;i++)
        {
            double theta=i*Math.PI/96,r=r0*Math.Exp(theta*Math.Tan(phi)),angle=Math.PI-alpha-theta;
            double x=width/2+r*Math.Cos(angle),z=r*Math.Sin(angle);
            m.Right.Add(new(centre+x,z));m.Left.Add(new(centre-x,z));
            if(i%6==0)m.Fan.Add(new(centre+x,z));
        }
        var end=m.Right[m.Right.Count-1];double edge=end.X+end.Y/Math.Tan(beta);
        m.Right.Add(new(edge,0));m.Left.Add(new(2*centre-edge,0));
        return m;
    }
}
