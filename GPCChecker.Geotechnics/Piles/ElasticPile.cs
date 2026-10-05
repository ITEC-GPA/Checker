namespace GPC.Checkers.Geotechnics.Piles;

/// <summary>Resolved constant/linear Winkler laws; ViggianiHorizontalSoil handles documented parameter selection. Consistent force/length units throughout.</summary>
public enum ElasticSoilLaw { ConstantKh, LinearKhReference, ConstantDistributed }
public enum ElasticPileTip { Free, Pinned, Fixed }
public sealed class ElasticPileLayer
{
    public string Name { get; set; } = "";
    public int OriginalLayer { get; set; }
    public double Thickness { get; set; }
    public ElasticSoilLaw Law { get; set; }
    /// <summary>F/L³ for kh and kh,ref; F/L² for distributed stiffness.</summary>
    public double Value { get; set; }
    public string Source { get; set; } = "Manuale";
}
public sealed class ElasticPileInput
{
    public double AxialHeadForce { get; set; }
    public double WeightPerLength { get; set; }
    public IReadOnlyList<double> AdditionalNodes { get; set; } = Array.Empty<double>();
    public ElasticPileSection? Section { get; set; }
    public double Length { get; set; }
    public double FreeLength { get; set; }
    public double Diameter { get; set; }
    public double EI { get; set; }
    public string EISource { get; set; } = "";
    public double Force { get; set; }
    public double HeadMoment { get; set; }
    /// <summary>Alternative to HeadMoment; generalized moment = H e. Never added twice.</summary>
    public double Eccentricity { get; set; }
    public bool FixedHeadRotation { get; set; }
    public ElasticPileTip Tip { get; set; }
    public double Step { get; set; }
    public IReadOnlyList<ElasticPileLayer> Layers { get; set; } = Array.Empty<ElasticPileLayer>();
}
public sealed class ElasticPilePoint
{
    public double AxialForce { get; set; }
    public double? Nh { get; set; }
    public double Depth { get; set; }
    public double GroundDepth { get; set; }
    public int Layer { get; set; }
    public string Side { get; set; } = "";
    public double Kh { get; set; }
    public double DistributedStiffness { get; set; }
    public double Displacement { get; set; }
    public double Rotation { get; set; }
    public double Shear { get; set; }
    public double Moment { get; set; }
    public double SoilReaction { get; set; }
}
public sealed class ElasticPileExtreme
{
    public double Minimum { get; set; }
    public double MinimumDepth { get; set; }
    public double Maximum { get; set; }
    public double MaximumDepth { get; set; }
    public double AbsoluteMaximum { get; set; }
    public double AbsoluteMaximumDepth { get; set; }
}
public sealed class ElasticPileResult
{
    public double AxialHeadForce { get; set; }
    public double WeightPerLength { get; set; }
    public string AxialModel => "N positivo a compressione; N(x)=Ntesta+w x. Nessun trasferimento assiale al terreno, galleggiamento o secondo ordine.";
    public ElasticPileSection? Section { get; set; }
    public List<ElasticPileNode> Nodes { get; set; } = new List<ElasticPileNode>();
    public List<ElasticPileSectionDemand> SectionDemands { get; set; } = new List<ElasticPileSectionDemand>();
    public Dictionary<string,double> RelativeMeshChanges { get; set; } = new Dictionary<string,double>();
    public bool MeshConverged { get; set; }
    public int ComparisonElements { get; set; }
    public List<ElasticPilePoint> Points { get; set; } = new List<ElasticPilePoint>();
    public Dictionary<string, ElasticPileExtreme> Extrema { get; set; } = new Dictionary<string, ElasticPileExtreme>();
    public int Elements { get; set; }
    public double HeadDisplacement { get; set; }
    public double HeadRotation { get; set; }
    public double AppliedHeadMoment { get; set; }
    public double HeadReactionMoment { get; set; }
    public double TipReactionForce { get; set; }
    public double TipReactionMoment { get; set; }
    public double SoilForce { get; set; }
    public double SoilMomentAboutHead { get; set; }
    public double ForceResidual { get; set; }
    public double MomentResidual { get; set; }
    public double MaximumFreeDofResidual { get; set; }
    public string FoundationDiscretization => "Consistent integral of k NᵀN; no independent nodal springs";
}
/// <summary>Linear Euler–Bernoulli/Winkler FEM. x from head down, z=x−free length from ground.
/// theta=y'; M=EI y''; V=M'; q=−k y; V'=q. Head generalized moment C gives M(0)=−C−Rtheta.
/// Exact Gauss integration for constant/linear k. Equilibrated force recovery; no displacement finite differences.</summary>
public static class ElasticPile
{
    public static ElasticPileResult CalculateWithConvergence(ElasticPileInput input)
    {
        var coarse=Calculate(input);var fine=Calculate(input,2);
        foreach(string key in new[]{"y","M","V"})
        {
            double a=key=="y"?coarse.HeadDisplacement:coarse.Extrema[key].AbsoluteMaximum;
            double b=key=="y"?fine.HeadDisplacement:fine.Extrema[key].AbsoluteMaximum;
            double scale=key=="y"?fine.Extrema["y"].AbsoluteMaximum:fine.Extrema[key].AbsoluteMaximum;
            fine.RelativeMeshChanges[key]=scale==0?0:Math.Abs(a-b)/Math.Max(Math.Abs(b),scale*1e-12);
        }
        fine.ComparisonElements=coarse.Elements;fine.MeshConverged=fine.RelativeMeshChanges.Values.All(v=>v<=.001);return fine;
    }
    static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    static void Require(bool ok, string text) { if (!ok) throw new ArgumentException(text); }
    public static double Distributed(ElasticPileLayer layer, double groundDepth, double diameter)
    {
        switch (layer.Law)
        {
            case ElasticSoilLaw.ConstantKh: return layer.Value * diameter;
            case ElasticSoilLaw.LinearKhReference: return layer.Value * groundDepth;
            case ElasticSoilLaw.ConstantDistributed: return layer.Value;
            default: throw new ArgumentException("Unknown soil law.");
        }
    }
    public static ElasticPileResult Calculate(ElasticPileInput input, int refinement = 1)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        Require(Finite(input.Length) && input.Length > 0 && Finite(input.FreeLength) && input.FreeLength >= 0 && input.FreeLength < input.Length, "Total length > free length ≥ 0 required.");
        Require(Finite(input.Diameter) && input.Diameter > 0 && Finite(input.EI) && input.EI > 0 && !string.IsNullOrWhiteSpace(input.EISource), "Positive diameter/EI and explicit EI source required.");
        Require(Finite(input.Force) && Finite(input.HeadMoment) && Finite(input.Eccentricity), "Finite loads required.");
        Require(Finite(input.AxialHeadForce) && Finite(input.WeightPerLength) && input.WeightPerLength >= 0, "Finite axial load and nonnegative weight required.");
        Require(input.HeadMoment == 0 || input.Eccentricity == 0, "Specify head moment OR eccentricity, not both.");
        Require(Enum.IsDefined(typeof(ElasticPileTip), input.Tip), "Unknown tip boundary.");
        Require(Finite(input.Step) && input.Step > 0 && refinement >= 1 && refinement <= 8, "Positive mesh step and refinement 1…8 required.");
        Require(input.Layers != null && input.Layers.Count > 0, "Soil layers required.");
        var boundaries = new List<double> { 0 }; if(input.FreeLength > 0) boundaries.Add(input.FreeLength);
        double bottom = input.FreeLength;
        foreach(var layer in input.Layers!)
        {
            if(layer == null) throw new ArgumentException("Null soil layer.");
            Require(Finite(layer.Thickness) && layer.Thickness > 0 && Finite(layer.Value) && layer.Value >= 0 && Enum.IsDefined(typeof(ElasticSoilLaw),layer.Law) && !string.IsNullOrWhiteSpace(layer.Source), "Each layer needs positive thickness, nonnegative finite stiffness, known law and source.");
            bottom += layer.Thickness;
            if(bottom < input.Length) boundaries.Add(bottom);
        }
        Require(bottom >= input.Length - 1e-10 * input.Length, "Layers must cover the embedded length."); boundaries.Add(input.Length);
        foreach(double station in input.AdditionalNodes){Require(Finite(station)&&station>=0&&station<=input.Length,"Invalid verification station.");boundaries.Add(station);}
        boundaries=boundaries.Distinct().OrderBy(v=>v).ToList();
        bool springs = input.Layers.TakeWhile((s,i)=>input.FreeLength+input.Layers.Take(i).Sum(t=>t.Thickness)<input.Length).Any(s=>s.Value>0);
        Require(springs || input.Tip == ElasticPileTip.Fixed || input.Tip == ElasticPileTip.Pinned && input.FixedHeadRotation, "Unstable model: unconstrained rigid-body motion; no artificial tip fixity is applied.");
        var x = new List<double> { 0 };
        for(int j=1;j<boundaries.Count;j++)
        {
            double a=boundaries[j-1], b=boundaries[j], count=Math.Ceiling((b-a)/input.Step)*refinement;
            Require(count <= 4000 && x.Count + count <= 4001, "Maximum 4000 elements, including refinement.");
            for(int i=1;i<=count;i++)x.Add(i==count?b:a+(b-a)*i/count);
        }
        int n=2*x.Count; var matrix=new Band(n); var elements=new List<(double[,] K,int Layer,double K0,double K1)>();
        for(int e=0;e<x.Count-1;e++)
        {
            double a=x[e], h=x[e+1]-a, mid=(a+x[e+1])/2; int layerIndex=-1; double top=input.FreeLength;
            if(mid>=top) for(int j=0;j<input.Layers.Count;j++){ if(mid<top+input.Layers[j].Thickness){layerIndex=j;break;}top+=input.Layers[j].Thickness; }
            double k0=layerIndex<0?0:Distributed(input.Layers[layerIndex],a-input.FreeLength,input.Diameter), k1=layerIndex<0?0:Distributed(input.Layers[layerIndex],a+h-input.FreeLength,input.Diameter);
            var k=new double[4,4];
            // Four Gauss points exactly integrate Hermite N_i N_j times linear stiffness (degree seven).
            double[] gp={-.8611363115940526,-.3399810435848563,.3399810435848563,.8611363115940526}, gw={.3478548451374539,.6521451548625461,.6521451548625461,.3478548451374539};
            for(int g=0;g<4;g++)
            {
                double t=(gp[g]+1)/2, w=gw[g]*h/2;
                double[] shape={1-3*t*t+2*t*t*t,h*(t-2*t*t+t*t*t),3*t*t-2*t*t*t,h*(-t*t+t*t*t)};
                double[] curvature={(-6+12*t)/(h*h),(-4+6*t)/h,(6-12*t)/(h*h),(-2+6*t)/h};
                for(int i=0;i<4;i++)for(int j=0;j<4;j++)k[i,j]+=w*(input.EI*curvature[i]*curvature[j]+(k0+(k1-k0)*t)*shape[i]*shape[j]);
            }
            for(int i=0;i<4;i++)for(int j=0;j<=i;j++)matrix.Add(2*e+i,2*e+j,k[i,j]);
            elements.Add((k,layerIndex,k0,k1));
        }
        var f=new double[n]; f[0]=input.Force; f[1]=input.HeadMoment+input.Force*input.Eccentricity;
        var fixedDofs=new bool[n]; fixedDofs[1]=input.FixedHeadRotation;fixedDofs[n-2]=input.Tip!=ElasticPileTip.Free;fixedDofs[n-1]=input.Tip==ElasticPileTip.Fixed;
        double[] u=matrix.Solve(f,fixedDofs), residual=matrix.Multiply(u);for(int i=0;i<n;i++)residual[i]-=f[i];
        var r=new ElasticPileResult { Elements=elements.Count,HeadDisplacement=u[0],HeadRotation=u[1],AppliedHeadMoment=f[1],HeadReactionMoment=fixedDofs[1]?residual[1]:0,TipReactionForce=fixedDofs[n-2]?residual[n-2]:0,TipReactionMoment=fixedDofs[n-1]?residual[n-1]:0,MaximumFreeDofResidual=Enumerable.Range(0,n).Where(i=>!fixedDofs[i]).Max(i=>Math.Abs(residual[i])) };
        for(int e=0;e<elements.Count;e++)
        {
            double a=x[e],h=x[e+1]-a;var el=elements[e];double[] ue={u[2*e],u[2*e+1],u[2*e+2],u[2*e+3]};
            var end=new double[4];for(int i=0;i<4;i++)for(int j=0;j<4;j++)end[i]+=el.K[i,j]*ue[j];
            // Polynomials in normalized element coordinate t. Equilibrated end forces include foundation terms.
            double[] y={ue[0],h*ue[1],-3*ue[0]-2*h*ue[1]+3*ue[2]-h*ue[3],2*ue[0]+h*ue[1]-2*ue[2]+h*ue[3]};
            var q=new double[5];for(int i=0;i<4;i++){q[i]-=el.K0*y[i];q[i+1]-=(el.K1-el.K0)*y[i];}
            double[] v=Integral(q,h,end[0]), m=Integral(v,h,-end[1]), theta=Derivative(y,1/h);
            var samples=new SortedSet<double>{0,1};for(int i=1;i<8;i++)samples.Add(i/8d);
            foreach(var polynomial in new[]{y,theta,v,m,q})foreach(double t in Roots(Derivative(polynomial,1)))samples.Add(t);
            foreach(double t in samples) {double k=el.K0+(el.K1-el.K0)*t; r.Points.Add(new ElasticPilePoint {Depth=a+h*t,GroundDepth=a+h*t-input.FreeLength,Layer=el.Layer<0?0:input.Layers[el.Layer].OriginalLayer>0?input.Layers[el.Layer].OriginalLayer:el.Layer+1,Nh=el.Layer>=0&&input.Layers[el.Layer].Law==ElasticSoilLaw.LinearKhReference?input.Layers[el.Layer].Value:(double?)null,Side=t==0?"Below":t==1?"Above":"Interior",Kh=k/input.Diameter,DistributedStiffness=k,Displacement=Value(y,t),Rotation=Value(theta,t),Shear=Value(v,t),Moment=Value(m,t),SoilReaction=Value(q,t)});}
            double force=Value(Integral(q,h,0),1), couple=0;for(int i=0;i<q.Length;i++)couple+=q[i]*h*(a/(i+1)+h/(i+2));
            r.SoilForce+=force;r.SoilMomentAboutHead+=couple;
        }
        r.ForceResidual=input.Force+r.SoilForce+r.TipReactionForce;
        r.MomentResidual=f[1]+r.HeadReactionMoment+r.TipReactionMoment+r.TipReactionForce*input.Length+r.SoilMomentAboutHead;
        r.Section=input.Section;
        r.AxialHeadForce=input.AxialHeadForce;r.WeightPerLength=input.WeightPerLength;
        foreach(var point in r.Points)point.AxialForce=input.AxialHeadForce+input.WeightPerLength*point.Depth;
        for(int i=0;i<x.Count;i++)
        {
            double tributaryTop=i==0?x[i]:(x[i-1]+x[i])/2,tributaryBottom=i==x.Count-1?x[i]:(x[i]+x[i+1])/2, spring=0;
            if(i>0){var el=elements[i-1];double h=x[i]-x[i-1];spring+=h*(el.K0+3*el.K1)/8;}
            if(i<elements.Count){var el=elements[i];double h=x[i+1]-x[i];spring+=h*(3*el.K0+el.K1)/8;}
            r.Nodes.Add(new ElasticPileNode{Depth=x[i],TributaryTop=tributaryTop,TributaryBottom=tributaryBottom,EquivalentSpring=spring});
        }
        foreach(var item in new Dictionary<string,Func<ElasticPilePoint,double>>{{"N",p=>p.AxialForce},{"y",p=>p.Displacement},{"theta",p=>p.Rotation},{"V",p=>p.Shear},{"M",p=>p.Moment},{"q",p=>p.SoilReaction}})
        {var lo=r.Points.OrderBy(item.Value).First();var hi=r.Points.OrderByDescending(item.Value).First();var abs=Math.Abs(item.Value(lo))>=Math.Abs(item.Value(hi))?lo:hi;r.Extrema[item.Key]=new ElasticPileExtreme{Minimum=item.Value(lo),MinimumDepth=lo.Depth,Maximum=item.Value(hi),MaximumDepth=hi.Depth,AbsoluteMaximum=Math.Abs(item.Value(abs)),AbsoluteMaximumDepth=abs.Depth};}
        foreach(var p in r.Points){var demand=new ElasticPileSectionDemand{Depth=p.Depth,Side=p.Side,Shear=p.Shear,Moment=p.Moment,SectionReference=input.Section?.Reference??input.EISource};foreach(string key in new[]{"V","M"}){var ex=r.Extrema[key];double v=key=="V"?p.Shear:p.Moment;if(p.Depth==ex.MinimumDepth&&v==ex.Minimum)demand.ExtremeReferences.Add(key+":min");if(p.Depth==ex.MaximumDepth&&v==ex.Maximum)demand.ExtremeReferences.Add(key+":max");if(p.Depth==ex.AbsoluteMaximumDepth&&Math.Abs(v)==ex.AbsoluteMaximum)demand.ExtremeReferences.Add(key+":abs");}r.SectionDemands.Add(demand);}
        for(int i=0;i<r.SectionDemands.Count;i++)r.SectionDemands[i].AxialForce=r.Points[i].AxialForce;
        Require(r.Points.All(p=>Finite(p.Displacement)&&Finite(p.Moment)&&Finite(p.Shear)&&Finite(p.AxialForce)), "Nonfinite result: review stiffness and units.");
        return r;
    }
    static double Value(double[] p,double t){double v=0;for(int i=p.Length-1;i>=0;i--)v=v*t+p[i];return v;}
    static double[] Integral(double[] p,double scale,double constant){var r=new double[p.Length+1];r[0]=constant;for(int i=0;i<p.Length;i++)r[i+1]=p[i]*scale/(i+1);return r;}
    static double[] Derivative(double[] p,double scale){var r=new double[Math.Max(1,p.Length-1)];for(int i=1;i<p.Length;i++)r[i-1]=p[i]*i*scale;return r;}
    // Isolate roots using derivative roots: every interval is monotone, including repeated roots.
    static List<double> Roots(double[] p)
    {
        int degree=p.Length-1;while(degree>0 && p[degree]==0)degree--;if(degree==0)return new List<double>();
        if(degree==1){double root=-p[0]/p[1];return root>0&&root<1?new List<double>{root}:new List<double>();}
        var cuts=new List<double>{0};cuts.AddRange(Roots(Derivative(p.Take(degree+1).ToArray(),1)));cuts.Add(1);var roots=new List<double>();
        double tolerance=p.Sum(Math.Abs)*1e-12;
        for(int i=1;i<cuts.Count;i++){double a=cuts[i-1],b=cuts[i],fa=Value(p,a),fb=Value(p,b);if(a>0&&Math.Abs(fa)<=tolerance)roots.Add(a);if(fa*fb>=0)continue;for(int j=0;j<48;j++){double c=(a+b)/2,fc=Value(p,c);if(Math.Sign(fc)==Math.Sign(fa)){a=c;fa=fc;}else b=c;}roots.Add((a+b)/2);}
        return roots;
    }
    sealed class Band
    {
        readonly double[,] a;readonly int n;
        internal Band(int size){n=size;a=new double[n,4];}
        double Get(int i,int j)=>i<j?Get(j,i):i-j>3?0:a[i,i-j];
        internal void Add(int i,int j,double value){a[i,i-j]+=value;}
        internal double[] Multiply(double[] u){var r=new double[n];for(int i=0;i<n;i++)for(int j=Math.Max(0,i-3);j<=Math.Min(n-1,i+3);j++)r[i]+=Get(i,j)*u[j];return r;}
        internal double[] Solve(double[] force,bool[] restrained)
        {
            var l=new double[n,4];var scale=new double[n];var b=new double[n];for(int i=0;i<n;i++){Require(a[i,0]>0&&Finite(a[i,0]),"Singular stiffness matrix.");scale[i]=Math.Sqrt(a[i,0]);b[i]=restrained[i]?0:force[i]/scale[i];}
            for(int i=0;i<n;i++)for(int j=Math.Max(0,i-3);j<=i;j++)
            {
                double value=restrained[i]||restrained[j]?(i==j?1:0):Get(i,j)/scale[i]/scale[j];
                for(int k=Math.Max(0,i-3);k<j;k++)value-=l[i,i-k]*l[j,j-k];
                if(i==j){Require(value>1e-14&&Finite(value),"Unstable or ill-conditioned model: review restraints, stiffness contrast and mesh.");l[i,0]=Math.Sqrt(value);}else l[i,i-j]=value/l[j,0];
            }
            var u=new double[n];for(int i=0;i<n;i++){double v=b[i];for(int j=Math.Max(0,i-3);j<i;j++)v-=l[i,i-j]*u[j];u[i]=v/l[i,0];}
            for(int i=n-1;i>=0;i--){double v=u[i];for(int j=i+1;j<=Math.Min(n-1,i+3);j++)v-=l[j,j-i]*u[j];u[i]=v/l[i,0];}
            for(int i=0;i<n;i++)u[i]/=scale[i];return u;
        }
    }
}
