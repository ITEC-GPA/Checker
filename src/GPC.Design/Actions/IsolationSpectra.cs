namespace GPC.Design.Actions;

public sealed record BearingGroup(string Name,int Count,double StiffnessKnPerMm);
public sealed record IsolationOptions(double MassKnS2PerM,double StructureDampingPercent,double IsolationDampingPercent,
    double ServiceStiffnessFactor,double UltimateStiffnessFactor,IReadOnlyList<BearingGroup> Groups);
public sealed record BearingForce(string Name,int Count,double ForcePerBearingKn);
public sealed record IsolationState(string State,double StiffnessKnPerMm,double PeriodSeconds,double ThresholdSeconds,
    double AccelerationG,double ForceKn,double DisplacementMm,IReadOnlyList<BearingForce> BearingForces,
    IReadOnlyList<SpectrumPoint> Spectrum,double ExampleSampleAccelerationG);
public sealed record PendulumResult(double FrictionForceKn,double EffectiveStiffnessKnPerMm,double DampingPercent,double PeriodSeconds);

/// <summary>Equivalent linear isolation spectra, NTC 2018 §7.10.5.3.2. Does not verify the applicability of the structural analysis model.</summary>
public static class IsolationSpectra
{
    /// <summary>NTC 2018 §3.2.3.2: longer fundamental periods require a site-specific action definition.</summary>
    public const double MaximumConventionalPeriodSeconds = 4;
    public static IReadOnlyList<IsolationState> Calculate(IReadOnlyList<SpectrumState> states,IsolationOptions o)
    {
        Validate(o);
        double nominal=o.Groups.Sum(g=>g.Count*g.StiffnessKnPerMm), es=NtcSpectra.Damping(o.StructureDampingPercent),ei=NtcSpectra.Damping(o.IsolationDampingPercent);
        return states.Select(s=>{
            double k=nominal*(s.State is "SLO" or "SLD"?o.ServiceStiffnessFactor:o.UltimateStiffnessFactor);
            double t=2*Math.PI*Math.Sqrt(o.MassKnS2PerM/(1000*k)),threshold=.8*t;
            if(!PortableMath.IsFinite(t)||t<=0||t>MaximumConventionalPeriodSeconds)
                throw new ArgumentException($"{s.State}: periodo equivalente Tis = {t:G10} s fuori dal campo (0, 4] s. Per periodi superiori a 4 s le NTC 2018 §3.2.3.2 richiedono apposite analisi o storie temporali del moto del terreno.");
            double Acc(double period)=>NtcSpectra.ElasticHorizontal(s.Parameters,s.Ss,s.Cc,s.St,period>=threshold?ei:es,period);
            // Include both sides of the damping change; no artificial 0.0001 s gap in the engineering result.
            double end=MaximumConventionalPeriodSeconds;
            var periods=Enumerable.Range(0,401).Select(i=>end*i/400).Concat(new[]{s.Tb,s.Tc,s.Td,t,PortableMath.BitDecrement(threshold),threshold}).Where(x=>x>=0&&x<=end).Distinct().OrderBy(orderValue => orderValue).ToArray();
            double sa=Acc(t),force=o.MassKnS2PerM*9.81*sa;
            var groups=o.Groups.Select(g=>new BearingForce(g.Name,g.Count,g.Count==0?0:force*g.StiffnessKnPerMm/nominal)).ToArray();
            var example=ExampleSamples(s,threshold,es,ei);
            double sampled=example.Last(p=>p.PeriodSeconds<=t).AccelerationG;
            return new IsolationState(s.State,k,t,threshold,sa,force,force/k,groups,periods.Select(p=>new SpectrumPoint(p,Acc(p))).ToArray(),sampled);
        }).ToArray();
    }
    /// <summary>The supplied spreadsheet's 45 samples, only for explicit comparison; includes its displaced breakpoint and one-sided threshold.</summary>
    public static IReadOnlyList<SpectrumPoint> ExampleSamples(SpectrumState s,double threshold,double structureEta,double isolationEta)
    {
        double[] f=new double[45],g=new double[46];f[1]=s.Tb;f[2]=s.Tc;
        for(int i=3;i<45;i++)f[i]=f[i-1]+(4-s.Tc)/42;
        for(int i=1;i<45;i++)g[i]=threshold>f[i-1]&&threshold<f[i]?threshold:s.Td>f[i-1]&&s.Td<f[i]?s.Td:i>=3&&g[i-1]==threshold?threshold+.0001:f[i];
        double a=s.Parameters.AgG*s.Ss*s.St,fo=s.Parameters.F0;
        return Enumerable.Range(0,45).Select(i=>{
            double t=g[i],v=t<s.Tb?a*(1+(structureEta*fo-1)*t/s.Tb):g[i+1]>=s.Tb&&t<s.Tc?a*structureEta*fo:
                t>=s.Tc&&t<=threshold?a*structureEta*fo*s.Tc/t:t>=threshold+.0001&&t<s.Td?a*isolationEta*fo*s.Tc/t:
                t>=s.Td?a*(i==0?structureEta:isolationEta)*fo*s.Tc*s.Td/(t*t):0;
            return new SpectrumPoint(t,v);
        }).ToArray();
    }
    /// <summary>Single friction pendulum: N in kN, radius/displacement in mm, friction dimensionless. Example uses g=9806 mm/s².</summary>
    public static PendulumResult Pendulum(double normalForceKn,double friction,double displacementMm,double radiusMm,double gravityMmS2=9806)
    {
        NtcSpectra.FiniteRange(normalForceKn,double.Epsilon,1e12,nameof(normalForceKn));NtcSpectra.FiniteRange(friction,double.Epsilon,1,nameof(friction));
        NtcSpectra.FiniteRange(displacementMm,double.Epsilon,1e7,nameof(displacementMm));NtcSpectra.FiniteRange(radiusMm,double.Epsilon,1e9,nameof(radiusMm));NtcSpectra.FiniteRange(gravityMmS2,double.Epsilon,1e6,nameof(gravityMmS2));
        double ratio=1/radiusMm+friction/displacementMm;
        return new(friction*normalForceKn,normalForceKn*ratio,200/Math.PI/(displacementMm/friction/radiusMm+1),2*Math.PI/Math.Sqrt(gravityMmS2*ratio));
    }
    public static void Validate(IsolationOptions o)
    {
        NtcSpectra.FiniteRange(o.MassKnS2PerM,double.Epsilon,1e12,nameof(o.MassKnS2PerM));
        NtcSpectra.FiniteRange(o.StructureDampingPercent,5,30,nameof(o.StructureDampingPercent));NtcSpectra.FiniteRange(o.IsolationDampingPercent,5,30,nameof(o.IsolationDampingPercent));
        NtcSpectra.FiniteRange(o.ServiceStiffnessFactor,double.Epsilon,100,nameof(o.ServiceStiffnessFactor));NtcSpectra.FiniteRange(o.UltimateStiffnessFactor,double.Epsilon,100,nameof(o.UltimateStiffnessFactor));
        Guard.NotNull(o.Groups);
        if(o.Groups.Count==0||o.Groups.Count>100||o.Groups.All(g=>g.Count==0))throw new ArgumentException("Occorre almeno un isolatore attivo.");
        foreach(var g in o.Groups){if(g.Count<0||g.Count>100000)throw new ArgumentException("Numero di isolatori non valido.");NtcSpectra.FiniteRange(g.StiffnessKnPerMm,double.Epsilon,1e9,nameof(g.StiffnessKnPerMm));}
    }
}
