using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.Detailing;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Checkers.Concrete.Shear;
using GPC.Geometry;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Piles
{
    public sealed class PileResistanceTimings { public double WorkerPreparationMs { get; set; } public double ParallelResistanceMs { get; set; } public int Workers { get; set; } public int NewAxialValues { get; set; } }
    public enum PileDetailingMode { Column, Beam, CommonOnly }
    /// <summary>Exact-N resistances for one immutable section/material/solver configuration. No interpolation or rounding.</summary>
    public sealed class PileResistanceTable
    {
        internal readonly ConcurrentDictionary<double,Tuple<double?,double?,string>> Values=new ConcurrentDictionary<double,Tuple<double?,double?,string>>();
        public int Count => Values.Count;
    }
    /// <summary>Concomitant design actions, kN/kNm, depth m. Compression positive.</summary>
    public sealed class PileAction
    {
        public double Depth { get; set; }
        public string Side { get; set; }
        public double N { get; set; }
        public double V { get; set; }
        public double M { get; set; }
    }
    public sealed class PileRcParameters
    {
        public SectionSolver Solver { get; set; }
        public CoordinateSystem Axes { get; set; }
        public ReinforcedConcreteSection Section { get; set; }
        public StandardModelCode2010 Standard { get; set; }
        public double Diameter { get; set; } // mm
        public double Fck { get; set; }
        public double Fctk05 { get; set; }
        public double Fcd { get; set; }
        public double Fyd { get; set; }
        public double Es { get; set; }
        public double Cover { get; set; }
        public double LinkDiameter { get; set; }
        public double LinkSpacing { get; set; }
        public double LeverFactor { get; set; } = .9;
        public bool ShearModelConfirmed { get; set; }
        public bool DesignActionsConfirmed { get; set; }
        public bool PileMinimumRequirements { get; set; }
        public bool GoodBond { get; set; }
        public double LapPercent { get; set; } = 100;
        public double LapClearDistance { get; set; }
        public double HeadAnchorage { get; set; } // m available outside the model
        public double ToeAnchorage { get; set; }
        public double StockLength { get; set; } = 12;
        public double LapDiameterFactor { get; set; } = 60;
        public double Aggregate { get; set; } = 20;
        public double? MinimumDurabilityCover { get; set; }
        public double CoverDeviation { get; set; } = 10;
        public double Fctm { get; set; } = 3;
        public double Fyk { get; set; } = 450;
        public PileDetailingMode DetailingMode { get; set; } = PileDetailingMode.Column;
        public bool CompressionBarsRestrained { get; set; }
        public bool EndZonesConfirmed { get; set; }
    }
    public sealed class PileSectionCheck
    {
        public PileAction Action { get; set; }
        public double? MRdPositive { get; set; }
        public double? MRdNegative { get; set; }
        public double? VRd { get; set; }
        public double? BendingRatio { get; set; }
        public double? ShearRatio { get; set; }
        public double CotTheta { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public bool? MinimumReinforcementPassed { get; set; }
        public bool? DevelopmentAvailable { get; set; }
        public double? UsableMRdPositive => DevelopmentAvailable == true ? MRdPositive : null;
        public double? UsableMRdNegative => DevelopmentAvailable == true ? MRdNegative : null;
        public double? GoverningRatio => BendingRatio.HasValue && ShearRatio.HasValue ? Math.Max(BendingRatio.Value,ShearRatio.Value) : (double?)null;
    }
    public sealed class PileBarRun
    {
        public string Id { get; set; }
        public List<PileBarPosition> Positions { get; set; } = new List<PileBarPosition>();
        public List<PileBarJoint> Joints { get; set; } = new List<PileBarJoint>();
        public string StartKind { get; set; }
        public string EndKind { get; set; }
        public double? StartDevelopment { get; set; }
        public double? EndDevelopment { get; set; }
        public string Segment { get; set; }
        public int Count { get; set; }
        public double Diameter { get; set; }
        public double TheoreticalStart { get; set; }
        public double TheoreticalEnd { get; set; }
        public double Start { get; set; }
        public double End { get; set; }
        public double Anchorage { get; set; }
        public double Lap { get; set; }
        public double RequiredLap { get; set; }
        public double InitialLap { get; set; }
        public bool LapClearDistancePassed { get; set; } = true;
        public double Shift { get; set; }
        public double TotalLength => Count*(End-Start);
        public bool EndDevelopmentAvailable { get; set; }
        public double CapacityStart => Start + (StartDevelopment ?? Anchorage + Shift);
        public double CapacityEnd => End - (EndDevelopment ?? Anchorage + Shift);
        public string Status { get; set; }
        public string Reference { get; set; }
        public List<PileBarPiece> Pieces { get; set; } = new List<PileBarPiece>();
        public double CuttingLength => Pieces.Sum(b=>b.Count*(b.End-b.Start));
    }
    public sealed class PileBarPiece
    {
        public string Id { get; set; }
        public int Count { get; set; }
        public double Diameter { get; set; }
        public double Start { get; set; }
        public double End { get; set; }
        public double LapWithPrevious { get; set; }
        public double StockLength { get; set; }
        public double CuttingLength => End-Start;
    }
    public sealed class PileDetailSegment
    {
        public string Id { get; set; }
        public double Start { get; set; }
        public double End { get; set; }
        public PileReinforcement Service { get; set; }
        public IReadOnlyList<PileSectionCheck> Checks { get; set; }
    }
    /// <summary>Orchestration of the existing section-domain, shear and bond engines. No alternate resistance solver.</summary>
    public sealed partial class PileReinforcement
    {
        readonly PileRcParameters p;readonly CrackSectionGeometry geometry;readonly double d,area,asl;
        public PileResistanceTimings ResistanceTimings { get; private set; } = new PileResistanceTimings();
        public double LinkCentreRadius => CircularLinkRadius(p.Diameter,p.Cover,p.LinkDiameter);
        readonly Dictionary<double,Tuple<double?,double?,string>> resistance=new Dictionary<double,Tuple<double?,double?,string>>();
        /// <summary>Compute distinct N values on independent solver instances. Factories run sequentially because mesh construction may use native shared state. Publish only a completed batch.</summary>
        public int PrepareResistance(IEnumerable<double> axialForces,Func<PileReinforcement> workerFactory,PileResistanceTable table=null,CancellationToken cancellation=default(CancellationToken),int maximumParallelism=0,Action<int,int> progress=null)
        {
            cancellation.ThrowIfCancellationRequested();
            var values=axialForces.Distinct().ToArray();if(values.Any(n=>!Finite(n)))throw new ArgumentException("Sforzi normali finiti richiesti.");
            table=table??new PileResistanceTable();
            var missing=values.Where(n=>!table.Values.ContainsKey(n)).ToArray();
            ResistanceTimings=new PileResistanceTimings{NewAxialValues=missing.Length};
            int completed=values.Length-missing.Length;var progressLock=new object();progress?.Invoke(completed,values.Length);
            if(missing.Length>0)
            {
                int degree=Math.Min(missing.Length,maximumParallelism>0?maximumParallelism:Math.Max(1,Environment.ProcessorCount-1));
                ResistanceTimings.Workers=degree;var watch=Stopwatch.StartNew();
                var workers=new PileReinforcement[degree];
                for(int i=0;i<degree;i++){cancellation.ThrowIfCancellationRequested();workers[i]=workerFactory();}
                for(int i=0;i<degree;i++)for(int j=0;j<i;j++)if(ReferenceEquals(workers[i].p.Solver,workers[j].p.Solver))throw new ArgumentException("Ogni worker deve avere un solutore indipendente.");
                ResistanceTimings.WorkerPreparationMs=watch.Elapsed.TotalMilliseconds;watch.Restart();
                var solved=new Tuple<double?,double?,string>[missing.Length];
                Parallel.For(0,degree,new ParallelOptions{MaxDegreeOfParallelism=degree,CancellationToken=cancellation},worker=>
                {
                    for(int i=worker;i<missing.Length;i+=degree){cancellation.ThrowIfCancellationRequested();solved[i]=workers[worker].SolveResistance(missing[i],cancellation);lock(progressLock){completed++;progress?.Invoke(completed,values.Length);}}
                });
                ResistanceTimings.ParallelResistanceMs=watch.Elapsed.TotalMilliseconds;
                cancellation.ThrowIfCancellationRequested();
                for(int i=0;i<missing.Length;i++)table.Values.TryAdd(missing[i],solved[i]);
            }
            foreach(double n in values)resistance[n]=table.Values[n];return missing.Length;
        }
        Tuple<double?,double?,string> SolveResistance(double n,CancellationToken cancellation=default(CancellationToken))
        {
            double? plus=null,minus=null;string error="";
            try{cancellation.ThrowIfCancellationRequested();plus=Moment(n,1);cancellation.ThrowIfCancellationRequested();minus=Moment(n,-1);}
            catch(OperationCanceledException){throw;}
            catch(Exception ex){error="N fuori dominio o soluzione resistente non convergente: "+ex.Message;}
            return Tuple.Create(plus,minus,error);
        }
        public PileReinforcement(PileRcParameters parameters)
        {
            p=parameters??throw new ArgumentNullException(nameof(parameters));geometry=CrackSectionGeometry.From(p.Section);
            if(!Finite(p.LapPercent)||p.LapPercent<=0||p.LapPercent>100)throw new ArgumentException("Percentuale di sovrapposizione richiesta tra 0 e 100%.");
            if(!geometry.Circular||geometry.Holes.Count!=0||geometry.Bars.Count<4)throw new ArgumentException("Palo c.a.: richiesta sezione circolare piena con almeno quattro barre.");
            if(new[]{p.Diameter,p.Fcd,p.Fyd,p.Fctk05,p.Es,p.LinkDiameter,p.LinkSpacing,p.StockLength}.Any(v=>!Finite(v)||v<=0)||new[]{p.Cover,p.HeadAnchorage,p.ToeAnchorage}.Any(v=>!Finite(v)||v<0)||!Finite(p.LeverFactor)||p.LeverFactor<=0||p.LeverFactor>.9)throw new ArgumentException("Materiali, staffe, lunghezze o copriferro non validi.");
            var lower=geometry.Bars.Where(b=>b.Y<0).ToArray();var upper=geometry.Bars.Where(b=>b.Y>0).ToArray();
            if(lower.Length==0||upper.Length==0)throw new ArgumentException("Armatura richiesta sui due semicerchi.");
            d=p.Diameter/2+Math.Min(-lower.Sum(b=>b.Y*b.Area)/lower.Sum(b=>b.Area),upper.Sum(b=>b.Y*b.Area)/upper.Sum(b=>b.Area));
            area=Math.PI*p.Diameter*p.Diameter/4;asl=Math.Min(lower.Sum(b=>b.Area),upper.Sum(b=>b.Area));
        }
        public PileSectionCheck Check(PileAction action)
        {
            if(action==null||new[]{action.Depth,action.N,action.V,action.M}.Any(v=>!Finite(v)))throw new ArgumentException("Azioni concomitanti finite richieste.");
            var r=new PileSectionCheck{Action=action,Status="Non verificabile",Message=""};
            if(!resistance.TryGetValue(action.N,out var moments))
            {
                moments=SolveResistance(action.N);resistance.Add(action.N,moments);
            }
            r.MRdPositive=moments.Item1;r.MRdNegative=moments.Item2;r.Message=moments.Item3;
            if(!r.MRdPositive.HasValue||!r.MRdNegative.HasValue){r.MRdPositive=r.MRdNegative=null;return r;}
            double mr=action.M>=0?r.MRdPositive.Value:-r.MRdNegative.Value;
            r.BendingRatio=mr>0?Math.Abs(action.M)/mr:action.M==0?0:(double?)null;
            if(p.ShearModelConfirmed)
            {
                var shear=SectionShearCalculator.Calculate(new SectionShearInput(p.Standard,-action.N*1000,action.V*1000,action.M*1e6,area,p.Diameter,d,asl,p.Fck,p.Fcd,p.Fyd,p.Standard.GammaC,p.Es,2*Math.PI*p.LinkDiameter*p.LinkDiameter/4,p.LinkSpacing,90,null,p.LeverFactor));
                r.VRd=shear.VRd/1000;r.ShearRatio=shear.Ratio;r.CotTheta=shear.CotTheta;
                if(shear.Verdict==ShearVerdict.NotEvaluated)r.ShearRatio=null;
            }
            else r.Message+=" Confermare il modello di taglio circolare e z/d.";
            if(!p.DesignActionsConfirmed)r.Message+=" Natura delle azioni di progetto da confermare.";
            if(p.PileMinimumRequirements){r.MinimumReinforcementPassed=geometry.Bars.Sum(b=>b.Area)>=.003*area&&p.LinkDiameter>=8&&p.LinkSpacing<=8*geometry.Bars.Min(b=>b.Diameter);r.Message+=" Minimi pali NTC 2018 §7.2.5: As≥0,3% Ac, φst≥8 mm, s≤8φL; escluse prescrizioni delle zone dissipative.";}
            r.Status=(r.BendingRatio>1||r.ShearRatio>1)?"Non soddisfatto":r.GoverningRatio.HasValue&&p.DesignActionsConfirmed?"Soddisfatto N–M–V; dettagli da completare":"Parziale";
            if(r.MinimumReinforcementPassed==false)r.Status="Non soddisfatto";
            return r;
        }
        double Moment(double n,int sign)
        {
            // Native solver uses N in newtons, negative in compression. Constant-N must be selected by caller.
            var point=p.Solver.CalculateDomainPoint(new[]{new ResultBeamForces(-n*1000,0,0,0,sign*1e6,0,p.Axes)})[0];
            if(point==null||!Finite(point.NRd+point.MxRd+point.MyRd)||Math.Abs(point.NRd+n*1000)>Math.Max(1000,Math.Abs(n)*.001)||point.MxRd*sign<0||Math.Abs(point.MyRd)>Math.Max(1e6,Math.Abs(point.MxRd)*.001))throw new ArgumentException("Equilibrio N o direzione non rispettati.");
            return point.MxRd/1e6;
        }
        public PileBarRun Detail(string id,double start,double end,double totalLength,IReadOnlyList<PileSectionCheck> checks)
        {
            if(start<0||end<=start||end>totalLength)throw new ArgumentException("Quote del tratto non valide.");
            double phi=geometry.Bars.Max(b=>b.Diameter);var profile=DetailingProfiles.Resolve(p.Standard);
            var anchorage=AnchorageCalculator.Calculate(profile,new AnchorageInput(phi,p.Fyd,p.Fctk05,p.Standard.GammaC,p.GoodBond,0));
            // The generated pieces splice together in one plane. Never use a lower requested
            // percentage to shorten a grouped (100%) joint that has not been staggered.
            var lap=AnchorageCalculator.Calculate(profile,new AnchorageInput(phi,p.Fyd,p.Fctk05,p.Standard.GammaC,p.GoodBond,0,true,100,p.LapClearDistance));
            // EC2 9.2.1.3(2), shear truss with links at 90 degrees. Upper permitted cot(theta) if shear is not confirmed.
            double shift=p.LeverFactor*d*checks.Select(c=>c.CotTheta>0?c.CotTheta:2.5).DefaultIfEmpty(2.5).Max()/2000;
            double lb=anchorage.RequiredLength/1000,requiredLap=lap.RequiredLength/1000,initialLap=InitialLap(phi,p.LapDiameterFactor),l0=Math.Max(requiredLap,initialLap);
            double actualStart=start==0?0:Math.Max(0,start-l0),actualEnd=end;
            bool available=start-actualStart+1e-9>=lb+shift&&actualEnd-end+1e-9>=lb+shift;
            var run=new PileBarRun{Segment=id,Count=geometry.Bars.Count,Diameter=phi,TheoreticalStart=start,TheoreticalEnd=end,Start=actualStart,End=actualEnd,Anchorage=lb,Lap=l0,RequiredLap=requiredLap,InitialLap=initialLap,Shift=shift,EndDevelopmentAvailable=available,
                LapClearDistancePassed=lap.LapClearDistancePassed,
                Status=available&&lap.LapClearDistancePassed?"Proposta: sviluppo disponibile; giunti, confinamento e disposizione da verificare":"Proposta incompleta: sviluppo o distanza di sovrapposizione insufficiente",
                Reference=anchorage.Reference+"; "+lap.Reference+"; traslazione a_l=z cotθ/2 (EN1992-1-1 §9.2.1.3, JRC Arrieta 2011). α1…α5=1, σsd=fyd. Capacità nominale senza contributo aggiuntivo delle barre sovrapposte."};
            run.Reference+=" Default software richiesto: 60φ (o fattore assegnato), arrotondato per eccesso a 0,10 m; l0 adottata=max(default, richiesta).";
            run.Pieces=PreferredStockPieces(id,actualStart,actualEnd,p.StockLength,l0,run.Count,phi).ToList();
            if(run.Pieces.Count>1&&p.LapPercent<100)run.Status="Proposta incompleta: la distinta raggruppa i giunti al 100%; distribuire i giunti per la percentuale assegnata prima di usarla.";
            return run;
        }
        /// <summary>Equal cuts respecting the assigned stock length and lap. A preliminary grouped splice, not a staggered fabrication schedule.</summary>
        public static IReadOnlyList<PileBarPiece> StockPieces(string id,double start,double end,double stock,double lap,int count,double diameter)
        {
            if(new[]{start,end,stock,lap,diameter}.Any(v=>!Finite(v))||end<=start||stock<=lap||lap<0||count<1||diameter<=0)throw new ArgumentException("Lunghezza commerciale insufficiente rispetto alla sovrapposizione.");
            double length=end-start;int pieces=Math.Max(1,(int)Math.Ceiling((length-lap)/(stock-lap)));double piece=(length+(pieces-1)*lap)/pieces;var result=new List<PileBarPiece>();
            for(int i=0;i<pieces;i++){double a=start+i*(piece-lap);result.Add(new PileBarPiece{Id=id+"."+(i+1),Count=count,Diameter=diameter,Start=a,End=i==pieces-1?end:a+piece,LapWithPrevious=i==0?0:lap});}return result;
        }
        public static IReadOnlyList<PileBarRun> SegmentRuns(IReadOnlyList<PileDetailSegment> segments,double totalLength)
            => BuildBarRuns(segments,totalLength);
        // Compatibility entry point. The current user-defined cutting scheme preserves every segment.
        public static IReadOnlyList<PileBarRun> ContinuousRuns(IReadOnlyList<PileDetailSegment> segments,double totalLength)
            => SegmentRuns(segments,totalLength);
        /// <summary>Conservative availability gate for straight bars: full tensile development plus tension shift from both physical ends. Does not certify the remaining lap/confinement details.</summary>
        public static void ApplyDevelopment(PileBarRun run,IEnumerable<PileSectionCheck> checks)
            => ApplyDevelopment(new[]{run},checks);
        public static void ApplyDevelopment(IEnumerable<PileBarRun> runs,IEnumerable<PileSectionCheck> checks)
        {
            var bars=runs.ToArray();
            foreach(var c in checks)
            {
                c.DevelopmentAvailable=bars.Length>0&&bars.All(run=>c.Action.Depth>=run.CapacityStart-1e-9&&c.Action.Depth<=run.CapacityEnd+1e-9&&run.Joints.All(j=>j.LengthPassed&&j.ArrangementPassed));
                if(c.DevelopmentAvailable==false)
                {
                    if(c.Status.StartsWith("Soddisfatto"))c.Status="Parziale";
                    c.Message+=" Sviluppo delle barre o giunti non disponibili: MRd nominale non utilizzabile. Dettaglio locale da definire.";
                }
            }
        }
        /// <summary>Candidate boundaries from changes of demand class and a user-assigned maximum cage length. Preliminary heuristic, never a normative rule.</summary>
        public static double[] SuggestBoundaries(IReadOnlyList<PileSectionCheck> checks,double length,double maximumLength,double minimumLength)
        {
            if(!Finite(length+maximumLength+minimumLength)||length<=0||maximumLength<=0||minimumLength<=0||minimumLength>maximumLength)throw new ArgumentException("Lunghezze esecutive positive e coerenti richieste.");
            var cuts=new List<double>{0};double last=0;int previous=-1;
            foreach(var c in checks.OrderBy(c=>c.Action.Depth))
            {double x=c.Action.Depth;int demand=c.GoverningRatio.HasValue?(int)Math.Ceiling(c.GoverningRatio.Value/.25):-1;
                if(x-last>=minimumLength&&length-x>=minimumLength&&((previous>=0&&demand!=previous)||x-last>=maximumLength)){cuts.Add(x);last=x;}previous=demand;}
            for(int i=0;i<cuts.Count;i++){double end=i+1<cuts.Count?cuts[i+1]:length;while(end-cuts[i]>maximumLength){cuts.Insert(i+1,cuts[i]+maximumLength);i++;}}
            cuts.Add(length);return cuts.Distinct().OrderBy(x=>x).ToArray();
        }
        public static double[] SuggestWithDevelopment(IReadOnlyList<PileSectionCheck> checks,IReadOnlyList<PileBarRun> runs,double length,double maximumLength,double minimumLength)
        {
            double allowance=runs.Select(r=>Math.Max(r.Anchorage,r.Lap)+r.Shift).DefaultIfEmpty(0).Max();
            double available=maximumLength-allowance,minimum=Math.Max(minimumLength,allowance);
            if(available<minimum)throw new ArgumentException("La lunghezza massima non lascia spazio sufficiente per sviluppo e sovrapposizioni; aumentarla o modificare il dettaglio.");
            return SuggestBoundaries(checks,length,available,minimum);
        }
        static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);

        /// <summary>Discrete design over explicitly supplied layouts, retaining the existing domain and shear engines. Returns the first fully passing candidate; no independent maxima.</summary>
        public static int SelectCandidate(IReadOnlyList<PileRcParameters> candidates,IReadOnlyList<PileAction> actions)
        {
            if(actions.Count==0)throw new ArgumentException("Azioni di progetto richieste.");
            for(int i=0;i<candidates.Count;i++)
            {
                if(!candidates[i].DesignActionsConfirmed||!candidates[i].ShearModelConfirmed)throw new ArgumentException("Confermare natura delle azioni e modello di taglio prima del dimensionamento.");
                var service=new PileReinforcement(candidates[i]);bool pass=true;
                foreach(var action in actions){var c=service.Check(action);if(!c.Status.StartsWith("Soddisfatto")){pass=false;break;}}
                if(pass)return i;
            }
            return -1;
        }
    }
}
