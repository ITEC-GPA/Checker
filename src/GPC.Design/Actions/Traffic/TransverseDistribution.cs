using GPC.Model.Actions.Traffic;
using static GPC.Design.Actions.Climate.ClimateMath;

namespace GPC.Design.Actions.Traffic;

/// <summary>Courbon or deformable transverse strip with a converged longitudinal sine expansion.
/// Girders simply supported in bending and restrained in torsional rotation at both ends.
/// Per harmonic λ=nπ/L the transverse nodal springs are EI λ⁴ and GJ λ².
/// Reported percentages are shares of the longitudinal bending moment at the chosen section, not universal load factors.</summary>
public static partial class TransverseDistribution
{
    private sealed record Patch(double Xa,double Xb,double Ya,double Yb,double Pressure);
    private sealed record Loading(string Name,Patch[] Patches);

    public static TransverseDistributionResult Calculate(TransverseDistributionInput x,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();Validate(x);var girders=x.Girders.OrderBy(g=>g.PositionM).ToArray();var loads=Loads(x);
        var result=new TransverseDistributionResult{Method=x.Method==TransverseDistributionMethod.RigidCourbon?"Courbon · traverso rigido, torsione trascurata":"Traverso deformabile · espansione di Fourier e molle flesso-torsionali"};
        result.Notes.Add("Percentuali riferite al momento longitudinale nella sezione x = "+x.SectionM+" m. Non sono coefficienti universali per taglio, reazioni o verifiche locali della soletta. Valori negativi conservati.");
        result.Notes.Add("Travi rettilinee prismatiche in semplice appoggio, stessa luce; rotazione torsionale impedita alle estremità. Soletta come strisce trasversali indipendenti: rigidezza longitudinale inclusa nell’EI delle travi, senza aggiungerla una seconda volta.");
        result.Notes.Add("Carichi assegnati: nessuna amplificazione dinamica o combinazione normativa aggiunta. Corsie con rettangolo caricato esplicito; tandem con due assi e due ruote per asse, impronte rettangolari.");
        result.Notes.Add("Esclusi impalcati obliqui, curvi, continui, geometria variabile e traversi intermedi discreti. Il metodo deformabile non è un modello FEM completo dell’impalcato.");
        if(x.Method==TransverseDistributionMethod.RigidCourbon)
        {
            foreach(var load in loads)result.Cases.Add(Rigid(x,girders,load));result.Converged=true;
            for(int p=0;p<x.InfluenceStations;p++)
            {
                double y=x.DeckLeftM+(x.DeckRightM-x.DeckLeftM)*p/(x.InfluenceStations-1),m=PointMoment(x.SpanM,x.SectionM,x.InfluenceLongitudinalM);
                result.Influence.Add(new(){PositionM=y,MomentOrdinatesM=RigidFactors(girders,y).Select(f=>f*m).ToList()});
            }
        }
        else
        {
            var nodes=new[]{x.DeckLeftM,x.DeckRightM}.Concat(girders.Select(g=>g.PositionM)).Distinct().OrderBy(y=>y).ToArray();
            int[] at=girders.Select(g=>Array.IndexOf(nodes,g.PositionM)).ToArray();
            var values=loads.Select(_=>new double[girders.Length]).ToArray();var torsion=loads.Select(_=>new double[girders.Length]).ToArray();var previous=loads.Select(_=>new double[girders.Length]).ToArray();
            var statics=loads.Select(l=>StaticMoment(x,l)).ToArray();int checkAt=16;
            var influence=Enumerable.Range(0,x.InfluenceStations).Select(p=>new DistributionInfluencePoint{PositionM=x.DeckLeftM+(x.DeckRightM-x.DeckLeftM)*p/(x.InfluenceStations-1),MomentOrdinatesM=Enumerable.Repeat(0d,girders.Length).ToList()}).ToArray();
            var errors=new double[loads.Length];var previousInfluence=influence.Select(_=>new double[girders.Length]).ToArray();
            double influenceStatic=PointMoment(x.SpanM,x.SectionM,x.InfluenceLongitudinalM);
            for(int n=1;n<=x.MaximumModes;n++)
            {
                cancellation.ThrowIfCancellationRequested();double lambda=n*Math.PI/x.SpanM;
                var matrix=Stiffness(nodes,x.TransverseRigidityKnM,girders,at,lambda);var solver=new SymmetricFactor(matrix);
                for(int c=0;c<loads.Length;c++)
                {
                    var force=new double[nodes.Length*2];
                    foreach(var p in loads[c].Patches)
                    {
                        double amplitude=2*p.Pressure*(Math.Cos(lambda*p.Xa)-Math.Cos(lambda*p.Xb))/(x.SpanM*lambda);
                        AddUniform(force,nodes,p.Ya,p.Yb,amplitude);
                    }
                    var u=solver.Solve(force);result.MaximumResidual=Math.Max(result.MaximumResidual,Residual(matrix,u,force));
                    for(int i=0;i<girders.Length;i++)
                    {
                        values[c][i]+=girders[i].FlexuralRigidityKnM2*lambda*lambda*u[2*at[i]]*Math.Sin(lambda*x.SectionM);
                        torsion[c][i]+=girders[i].TorsionalRigidityKnM2*lambda*u[2*at[i]+1]*Math.Cos(lambda*x.SectionM);
                    }
                }
                foreach(var p in influence)
                {
                    var force=new double[nodes.Length*2];AddPoint(force,nodes,p.PositionM,2/x.SpanM*Math.Sin(lambda*x.InfluenceLongitudinalM));var u=solver.Solve(force);
                    for(int i=0;i<girders.Length;i++)p.MomentOrdinatesM[i]+=girders[i].FlexuralRigidityKnM2*lambda*lambda*u[2*at[i]]*Math.Sin(lambda*x.SectionM);
                }
                if(n!=checkAt&&n!=x.MaximumModes)continue;
                bool converged=n>=32;
                for(int c=0;c<loads.Length;c++)
                {
                    double scale=Math.Max(1e-12,Math.Abs(statics[c]));errors[c]=values[c].Select((v,i)=>Math.Abs(v-previous[c][i])).Max()/scale;
                    converged&=errors[c]<=x.RelativeTolerance&&Math.Abs(values[c].Sum()-statics[c])/scale<=x.RelativeTolerance;
                    Array.Copy(values[c],previous[c],girders.Length);
                }
                result.InfluenceConvergenceRelativeError=0;result.InfluenceEquilibriumRelativeError=0;
                for(int p=0;p<influence.Length;p++)
                {
                    var ordinates=influence[p].MomentOrdinatesM;
                    result.InfluenceConvergenceRelativeError=Math.Max(result.InfluenceConvergenceRelativeError,ordinates.Select((v,i)=>Math.Abs(v-previousInfluence[p][i])).Max()/influenceStatic);
                    result.InfluenceEquilibriumRelativeError=Math.Max(result.InfluenceEquilibriumRelativeError,Math.Abs(ordinates.Sum()-influenceStatic)/influenceStatic);
                    ordinates.CopyTo(previousInfluence[p]);
                }
                converged&=result.InfluenceConvergenceRelativeError<=x.RelativeTolerance&&result.InfluenceEquilibriumRelativeError<=x.RelativeTolerance;
                result.Modes=n;result.Converged=converged;
                if(converged||n==x.MaximumModes)break;checkAt*=2;
            }
            for(int c=0;c<loads.Length;c++)result.Cases.Add(Row(girders,loads[c],statics[c],values[c],torsion[c],errors[c]));
            result.Influence=influence.ToList();
            result.Notes.Add("Molle armoniche: kv,i = EI,i (nπ/L)^4; kθ,i = GJ,i (nπ/L)^2. Trave trasversale Euler–Bernoulli, carichi nodali consistenti. Modi usati: "+result.Modes+"; tolleranza relativa sul momento per trave: "+x.RelativeTolerance+".");
            result.Notes.Add(result.Converged?"Convergenza dei momenti di tutti i casi verificata fra due troncamenti successivi e rispetto alla statica globale.":"CONVERGENZA NON RAGGIUNTA: aumentare il numero massimo di modi o riesaminare il modello. Percentuali e diagrammi sono risultati provvisori.");
            result.Notes.Add("Le linee d’influenza usano un carico puntuale unitario: anche il loro incremento fra troncamenti e l’equilibrio globale entrano nel criterio di convergenza. Non usare questi diagrammi per verifiche locali. La convergenza dichiarata riguarda i momenti longitudinali; le torsioni sono grandezze accessorie al troncamento indicato.");
        }
        Require(result.MaximumResidual<1e-7,"Sistema numerico mal condizionato: residuo di equilibrio eccessivo.");return result;
    }

    private static void Validate(TransverseDistributionInput x)
    {
        if(x is null)throw new ArgumentNullException(nameof(x));EnumValue(x.Method);Require(x.ApplicabilityConfirmed,"Confermare le ipotesi del modello di ripartizione scelto.");
        Range(x.SpanM,1,300,"Luce");Range(x.SectionM,.001*x.SpanM,.999*x.SpanM,"Sezione longitudinale");Range(x.InfluenceLongitudinalM,.001*x.SpanM,.999*x.SpanM,"Ascissa del carico unitario");
        Range(x.DeckLeftM,-100,100,"Bordo sinistro");Range(x.DeckRightM,-100,100,"Bordo destro");Positive(x.DeckRightM-x.DeckLeftM,"Larghezza impalcato");Positive(x.TransverseRigidityKnM,"Rigidezza trasversale");
        Range(x.RelativeTolerance,1e-7,.01,"Tolleranza");Require(x.MaximumModes>=32&&x.MaximumModes<=512,"Numero massimo modi: 32…512.");Require(x.InfluenceStations>=11&&x.InfluenceStations<=201,"Stazioni influenza: 11…201.");
        if(x.Girders is null)throw new ArgumentException("Elenco travi mancante.");Require(x.Girders.Count>=2&&x.Girders.Count<=30,"Servono da 2 a 30 travi.");
        foreach(var g in x.Girders){if(g is null)throw new ArgumentException("Trave nulla.");Require(!string.IsNullOrWhiteSpace(g.Name),"Nome trave mancante.");Range(g.PositionM,x.DeckLeftM,x.DeckRightM,"Posizione trave");Positive(g.FlexuralRigidityKnM2,"EI trave");Range(g.TorsionalRigidityKnM2,0,double.MaxValue,"GJ trave");}
        Require(x.Girders.Select(g=>g.Name).Distinct().Count()==x.Girders.Count,"Nomi delle travi duplicati.");var y=x.Girders.Select(g=>g.PositionM).OrderBy(v=>v).ToArray();Require(y.Zip(y.Skip(1),(a,b)=>b-a).All(d=>d>.001),"Travi coincidenti o distanti meno di 1 mm.");
        if(x.Lanes is null||x.Tandems is null)throw new ArgumentException("Elenchi corsie/tandem mancanti.");Require(x.Lanes.Count+x.Tandems.Count>0&&x.Lanes.Count+x.Tandems.Count<=20,"Assegnare da 1 a 20 corsie/tandem.");
        foreach(var l in x.Lanes)
        {if(l is null)throw new ArgumentException("Corsia nulla.");Require(!string.IsNullOrWhiteSpace(l.Name),"Nome corsia mancante.");Positive(l.WidthM,"Larghezza corsia");Range(l.LeftM,x.DeckLeftM,x.DeckRightM-l.WidthM,"Bordo corsia");Range(l.StartM,0,x.SpanM,"Inizio corsia");Range(l.EndM,l.StartM,x.SpanM,"Fine corsia");Positive(l.EndM-l.StartM,"Lunghezza caricata");Positive(l.LoadKnM2,"Carico corsia");}
        foreach(var t in x.Tandems)
        {
            if(t is null)throw new ArgumentException("Tandem nullo.");Require(!string.IsNullOrWhiteSpace(t.Name),"Nome tandem mancante.");Positive(t.AxleLoadKn,"Carico asse");Positive(t.AxleSpacingM,"Interasse assi");Positive(t.WheelSpacingM,"Interasse ruote");Range(t.FootprintLengthM,.05,2,"Impronta longitudinale");Range(t.FootprintWidthM,.05,2,"Impronta trasversale");
            double hx=(t.AxleSpacingM+t.FootprintLengthM)/2,hy=(t.WheelSpacingM+t.FootprintWidthM)/2;Range(t.CentreLongitudinalM,hx,x.SpanM-hx,"Centro tandem longitudinale");Range(t.CentreTransverseM,x.DeckLeftM+hy,x.DeckRightM-hy,"Centro tandem trasversale");
        }
        Require(x.Lanes.Select(l=>l.Name).Concat(x.Tandems.Select(t=>t.Name)).Distinct().Count()==x.Lanes.Count+x.Tandems.Count,"I nomi di corsie e tandem devono essere univoci.");
    }
    private static Loading[] Loads(TransverseDistributionInput x)
    {
        var loads=x.Lanes.Select(l=>new Loading(l.Name,[new(l.StartM,l.EndM,l.LeftM,l.LeftM+l.WidthM,l.LoadKnM2)])).ToList();
        foreach(var t in x.Tandems)
        {var patches=new List<Patch>();foreach(int a in new[]{-1,1})foreach(int b in new[]{-1,1}){double xc=t.CentreLongitudinalM+a*t.AxleSpacingM/2,yc=t.CentreTransverseM+b*t.WheelSpacingM/2;patches.Add(new(xc-t.FootprintLengthM/2,xc+t.FootprintLengthM/2,yc-t.FootprintWidthM/2,yc+t.FootprintWidthM/2,t.AxleLoadKn/(2*t.FootprintLengthM*t.FootprintWidthM)));}loads.Add(new(t.Name,patches.ToArray()));}
        loads.Add(new("Totale simultaneo assegnato",loads.SelectMany(l=>l.Patches).ToArray()));return loads.ToArray();
    }
    private static double Load(Patch p)=>p.Pressure*(p.Xb-p.Xa)*(p.Yb-p.Ya);
    private static double PointMoment(double l,double x,double a)=>a<=x?a*(l-x)/l:x*(l-a)/l;
    private static double StaticMoment(TransverseDistributionInput x,Loading load)=>load.Patches.Sum(p=>PatchMoment(x,p));
    private static double PatchMoment(TransverseDistributionInput x,Patch p)
    {
        double q=p.Pressure*(p.Yb-p.Ya),ra=Load(p)*(x.SpanM-(p.Xa+p.Xb)/2)/x.SpanM;
        double u=Math.Max(0,x.SectionM-p.Xa),v=Math.Max(0,x.SectionM-p.Xb);return ra*x.SectionM-q*(u*u-v*v)/2;
    }
    private static double[] RigidFactors(DistributionGirder[] g,double y)
    {double sum=g.Sum(t=>t.FlexuralRigidityKnM2),centre=g.Sum(t=>t.FlexuralRigidityKnM2*t.PositionM)/sum,inertia=g.Sum(t=>t.FlexuralRigidityKnM2*(t.PositionM-centre)*(t.PositionM-centre));return g.Select(t=>t.FlexuralRigidityKnM2/sum+t.FlexuralRigidityKnM2*(t.PositionM-centre)*(y-centre)/inertia).ToArray();}
    private static DistributionLoadResult Rigid(TransverseDistributionInput x,DistributionGirder[] g,Loading load)
    {var moments=new double[g.Length];foreach(var p in load.Patches){var f=RigidFactors(g,(p.Ya+p.Yb)/2);double m=PatchMoment(x,p);for(int i=0;i<g.Length;i++)moments[i]+=m*f[i];}return Row(g,load,StaticMoment(x,load),moments,new double[g.Length],0);}
    private static DistributionLoadResult Row(DistributionGirder[] g,Loading load,double statics,double[] moments,double[] torsion,double error)
    {
        double total=moments.Sum();var r=new DistributionLoadResult{Name=load.Name,TotalLoadKn=load.Patches.Sum(Load),StaticMomentKnM=statics,CalculatedMomentKnM=total,EquilibriumRelativeError=Math.Abs(total-statics)/Math.Max(1e-12,Math.Abs(statics)),ConvergenceRelativeError=error};
        for(int i=0;i<g.Length;i++)r.Girders.Add(new(){Name=g[i].Name,PositionM=g[i].PositionM,MomentKnM=moments[i],Percentage=100*moments[i]/total,TorsionKnM=torsion[i]});return r;
    }
    private static double[,] Stiffness(double[] y,double d,DistributionGirder[] g,int[] at,double lambda)
    {
        var k=new double[2*y.Length,2*y.Length];
        for(int e=0;e<y.Length-1;e++)
        {
            double l=y[e+1]-y[e],s=d/(l*l*l),l2=l*l;double[,] ke={{12,6*l,-12,6*l},{6*l,4*l2,-6*l,2*l2},{-12,-6*l,12,-6*l},{6*l,2*l2,-6*l,4*l2}};
            for(int i=0;i<4;i++)for(int j=0;j<4;j++)k[2*e+i,2*e+j]+=s*ke[i,j];
        }
        for(int i=0;i<g.Length;i++){k[2*at[i],2*at[i]]+=g[i].FlexuralRigidityKnM2*Math.Pow(lambda,4);k[2*at[i]+1,2*at[i]+1]+=g[i].TorsionalRigidityKnM2*lambda*lambda;}return k;
    }
    private static void AddPoint(double[] force,double[] nodes,double y,double p)
    {
        int e=0;while(e<nodes.Length-2&&y>nodes[e+1])e++;double l=nodes[e+1]-nodes[e],t=(y-nodes[e])/l,t2=t*t,t3=t2*t;
        double[] n={1-3*t2+2*t3,l*(t-2*t2+t3),3*t2-2*t3,l*(-t2+t3)};for(int i=0;i<4;i++)force[2*e+i]+=p*n[i];
    }
    private static void AddUniform(double[] force,double[] nodes,double a,double b,double q)
    {
        double gauss=Math.Sqrt(3d/5);double[] points={-gauss,0,gauss},weights={5d/9,8d/9,5d/9};
        for(int e=0;e<nodes.Length-1;e++){double lo=Math.Max(a,nodes[e]),hi=Math.Min(b,nodes[e+1]);if(hi<=lo)continue;for(int j=0;j<3;j++)AddPoint(force,nodes,(hi+lo)/2+(hi-lo)/2*points[j],q*(hi-lo)/2*weights[j]);}
    }
    private static double Residual(double[,] k,double[] u,double[] f)
    {double worst=0;for(int i=0;i<u.Length;i++){double sum=0,scale=Math.Abs(f[i]);for(int j=0;j<u.Length;j++){sum+=k[i,j]*u[j];scale+=Math.Abs(k[i,j]*u[j]);}worst=Math.Max(worst,Math.Abs(sum-f[i])/Math.Max(1e-30,scale));}return worst;}
    private sealed class SymmetricFactor
    {
        private readonly double[,] l;private readonly double[] scale;private readonly int n;
        internal SymmetricFactor(double[,] matrix)
        {
            n=matrix.GetLength(0);l=new double[n,n];scale=new double[n];for(int i=0;i<n;i++){Require(matrix[i,i]>0,"Rigidezza nulla nel modello trasversale.");scale[i]=Math.Sqrt(matrix[i,i]);}
            for(int i=0;i<n;i++)for(int j=0;j<=i;j++){double v=matrix[i,j]/scale[i]/scale[j];for(int m=0;m<j;m++)v-=l[i,m]*l[j,m];if(i==j){Require(v>1e-14&&!double.IsInfinity(v)&&!double.IsNaN(v),"Modello trasversale singolare o mal condizionato: verificare geometria e rigidezze.");l[i,j]=Math.Sqrt(v);}else l[i,j]=v/l[j,j];}
        }
        internal double[] Solve(double[] f)
        {var u=new double[n];for(int i=0;i<n;i++){double v=f[i]/scale[i];for(int j=0;j<i;j++)v-=l[i,j]*u[j];u[i]=v/l[i,i];}for(int i=n-1;i>=0;i--){double v=u[i];for(int j=i+1;j<n;j++)v-=l[j,i]*u[j];u[i]=v/l[i,i];}for(int i=0;i<n;i++)u[i]/=scale[i];return u;}
    }
}
