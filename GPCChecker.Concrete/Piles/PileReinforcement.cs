using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.Detailing;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Checkers.Concrete.Shear;
using GPC.Geometry;
using GPC.Model.Results;
using GPC.Model.Checking.Contracts;
using GPC.Model.Core.Diagnostics;
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
        /// <summary>Anchorage of the bars beyond the pile head (into the cap), m, outside the model: counted as embedded length in the development at the head, not added to the cuts.</summary>
        public double HeadAnchorage { get; set; }
        /// <summary>Anchorage of the bars beyond the toe, m, outside the model: counted as embedded length in the development at the toe, not added to the cuts.</summary>
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
        /// <summary>Nominal N-M-V and requested minimum-reinforcement outcome. A pass does not certify all construction details; ApplyDevelopment can make it unavailable.</summary>
        public EngineeringOutcome Outcome { get; internal set; } = EngineeringOutcome.NotEvaluated;
        /// <summary>Execution of this check. An unsolved resistance is Error, not a satisfied zero demand.</summary>
        public ExecutionStatus Execution { get; internal set; } = ExecutionStatus.NotExecuted;
        /// <summary>Availability of the information needed by this check, including explicit action/shear confirmations and requested development.</summary>
        public DataStatus Data { get; internal set; } = DataStatus.Insufficient;
        public PileAction Action { get; set; }
        public double? MRdPositive { get; set; }
        public double? MRdNegative { get; set; }
        public double? VRd { get; set; }
        public double? BendingRatio { get; set; }
        public double? ShearRatio { get; set; }
        public double CotTheta { get; set; }
        /// <summary>Compatibility display label. Engineering decisions use Outcome, Execution and Data, never this mutable text.</summary>
        public string Status { get; set; }
        public string Message { get; set; }
        public bool? MinimumReinforcementPassed { get; set; }
        /// <summary>
        /// Set by <see cref="PileReinforcement.ApplyDevelopment(IEnumerable{PileBarRun}, IEnumerable{PileSectionCheck})"/>: true where the bars are developed
        /// (and their joints usable), false only where the demand needs tensile bars and they are not developed, null where it does not need them
        /// (no bending and no axial tension, see <see cref="PileReinforcement.DevelopmentDemandRatio"/>) or before ApplyDevelopment.
        /// </summary>
        public bool? DevelopmentAvailable { get; set; }
        /// <summary>Developed fraction r of the bars used for MRd by <see cref="PileReinforcement.ApplyPartialDevelopment"/> (fyd·r); null with fully developed bars.</summary>
        public double? DevelopedFraction { get; set; }
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
        // Public actions/lengths are kN, kNm, m; section calculations use N, Nmm, mm.
        // Derive factors once from the shared unit API; preserve multiply/divide order of the legacy contract.
        static readonly double NewtonsPerKilonewton = GPC.Model.Units.ConvertForceToDefault(1, GPC.Model.Units.Knm);
        static readonly double NewtonMillimetresPerKilonewtonMetre = GPC.Model.Units.ConverMomentToDefault(1, GPC.Model.Units.Knm);
        static readonly double MillimetresPerMetre = GPC.Model.Units.ConvertLengthToDefault(1, GPC.Model.Units.Knm);
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
            // Continuous ring of radius rs: the tension half has its centroid 2rs/π from the centre and half of the bars.
            // Independent of bar count and of rounding of the bars lying on the neutral axis (12 bars: a bar at 180° fell in one half).
            double bars=geometry.Bars.Sum(b=>b.Area),ring=geometry.Bars.Sum(b=>b.Area*Math.Sqrt(b.X*b.X+b.Y*b.Y))/bars;
            d=p.Diameter/2+2*ring/Math.PI;
            area=Math.PI*p.Diameter*p.Diameter/4;asl=bars/2;
        }
        public PileSectionCheck Check(PileAction action)
        {
            if(action==null||new[]{action.Depth,action.N,action.V,action.M}.Any(v=>!Finite(v)))throw new ArgumentException("Azioni concomitanti finite richieste.");
            var r=new PileSectionCheck{Action=action,Status="Non verificabile",Message="",Data=DataStatus.Ready,Execution=ExecutionStatus.Error};
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
                var shear=SectionShearCalculator.Calculate(new SectionShearInput(p.Standard,-action.N*NewtonsPerKilonewton,action.V*NewtonsPerKilonewton,action.M*NewtonMillimetresPerKilonewtonMetre,area,p.Diameter,d,asl,p.Fck,p.Fcd,p.Fyd,p.Standard.GammaC,p.Es,2*Math.PI*p.LinkDiameter*p.LinkDiameter/4,p.LinkSpacing,90,null,p.LeverFactor));
                r.VRd=shear.VRd/NewtonsPerKilonewton;r.ShearRatio=shear.Ratio;r.CotTheta=shear.CotTheta;
                if(shear.Verdict==ShearVerdict.NotEvaluated)r.ShearRatio=null;
            }
            else r.Message+=" Confermare il modello di taglio circolare e z/d.";
            if(!p.DesignActionsConfirmed)r.Message+=" Natura delle azioni di progetto da confermare.";
            if(p.PileMinimumRequirements){r.MinimumReinforcementPassed=geometry.Bars.Sum(b=>b.Area)>=.003*area&&p.LinkDiameter>=8&&p.LinkSpacing<=8*geometry.Bars.Min(b=>b.Diameter);r.Message+=" Minimi pali NTC 2018 §7.2.5: As≥0,3% Ac, φst≥8 mm, s≤8φL; escluse prescrizioni delle zone dissipative.";}
            bool complete=r.GoverningRatio.HasValue&&p.DesignActionsConfirmed;
            r.Execution=ExecutionStatus.Completed;
            r.Data=complete?DataStatus.Ready:DataStatus.Insufficient;
            r.Outcome=r.BendingRatio>1||r.ShearRatio>1||r.MinimumReinforcementPassed==false
                ?EngineeringOutcome.NotSatisfied:complete?EngineeringOutcome.Satisfied:EngineeringOutcome.NotEvaluated;
            r.Status=r.Outcome==EngineeringOutcome.NotSatisfied?"Non soddisfatto":r.Outcome==EngineeringOutcome.Satisfied?"Soddisfatto N–M–V; dettagli da completare":"Parziale";
            return r;
        }
        double Moment(double n,int sign)
        {
            // Native solver uses N in newtons, negative in compression. Constant-N must be selected by caller.
            var point=p.Solver.CalculateDomainPoints(new[]{new ResultBeamForces(-n*NewtonsPerKilonewton,0,0,0,sign*NewtonMillimetresPerKilonewtonMetre,0,p.Axes)})[0];
            // Same acceptance as the section resistance at assigned N (F2-15, ANTHEA bug analysis 15/10/2026 PO-3): max(1 kN; 1e-6 |N|; 0.5e-4 b h fck),
            // also 1e-3 NRd,c with the stress block. The former max(1 kN; 1e-6 |N|) rejected the normal stops of the search on large piles
            // (D >= 1.6 m in C35/45: |NRd - N| 1.1-2.0 kN) and left every depth without MRd. Only the acceptance changes, never the point.
            double axialToleranceN=DomainPointAxialTolerance.Calculate(p.Solver,-n*NewtonsPerKilonewton);
            if(point==null||!Finite(point.NRd+point.MxRd+point.MyRd)||Math.Abs(point.NRd+n*NewtonsPerKilonewton)>axialToleranceN||point.MxRd*sign<0||Math.Abs(point.MyRd)>Math.Max(NewtonMillimetresPerKilonewtonMetre,Math.Abs(point.MxRd)*.001))throw new ArgumentException("Equilibrio N o direzione non rispettati.");
            return point.MxRd/NewtonMillimetresPerKilonewtonMetre;
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
            double shift=p.LeverFactor*d*checks.Select(c=>c.CotTheta>0?c.CotTheta:2.5).DefaultIfEmpty(2.5).Max()/(2*MillimetresPerMetre);
            double lb=anchorage.RequiredLength/MillimetresPerMetre,requiredLap=lap.RequiredLength/MillimetresPerMetre,initialLap=InitialLap(phi,p.LapDiameterFactor),l0=Math.Max(requiredLap,initialLap);
            double actualStart=start==0?0:Math.Max(0,start-l0),actualEnd=end;
            // Anchorage outside the model (into the cap at the head, beyond the toe) counts as embedded length at the pile ends (PO-5).
            double headExtra=start==0?p.HeadAnchorage:0,toeExtra=end>=totalLength-1e-9?p.ToeAnchorage:0;
            bool available=start-actualStart+headExtra+1e-9>=lb+shift&&actualEnd-end+toeExtra+1e-9>=lb+shift;
            var run=new PileBarRun{Segment=id,Count=geometry.Bars.Count,Diameter=phi,TheoreticalStart=start,TheoreticalEnd=end,Start=actualStart,End=actualEnd,Anchorage=lb,Lap=l0,RequiredLap=requiredLap,InitialLap=initialLap,Shift=shift,EndDevelopmentAvailable=available,
                LapClearDistancePassed=lap.LapClearDistancePassed,
                Status=available&&lap.LapClearDistancePassed?"Proposta: sviluppo disponibile; giunti, confinamento e disposizione da verificare":"Proposta incompleta: sviluppo o distanza di sovrapposizione insufficiente",
                Reference=anchorage.Reference+"; "+lap.Reference+"; traslazione a_l=z cotθ/2 (EN1992-1-1 §9.2.1.3, JRC Arrieta 2011). α1…α5=1, σsd=fyd. Capacità nominale senza contributo aggiuntivo delle barre sovrapposte."};
            if(start==0)run.StartDevelopment=Math.Max(0,lb+shift-p.HeadAnchorage);
            if(end>=totalLength-1e-9)run.EndDevelopment=Math.Max(0,lb+shift-p.ToeAnchorage);
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
        /// <summary>
        /// Bending ratio |M|/MRd at or below which a depth has no bending demand on the bars (1e-6: a numerically zero moment, such as the free head or the
        /// tip of the elastic pile model, M about 1e-10 kNm).
        /// </summary>
        public const double DevelopmentDemandRatio = 1e-6;
        /// <summary>Conservative availability gate for straight bars: full tensile development plus tension shift from both physical ends. Does not certify the remaining lap/confinement details.</summary>
        public static void ApplyDevelopment(PileBarRun run,IEnumerable<PileSectionCheck> checks)
            => ApplyDevelopment(new[]{run},checks);
        /// <summary>
        /// Development of the bars at each depth (ANTHEA bug analysis 15/10/2026, PO-5). The bars are developed at depth x when x lies in
        /// [CapacityStart; CapacityEnd] of every run, i.e. at least lbd + a_l from the bar ends (the anchorage outside the model at the pile head and toe,
        /// <see cref="PileRcParameters.HeadAnchorage"/> and <see cref="PileRcParameters.ToeAnchorage"/>, counts as embedded length there), and every joint
        /// of the runs has its length and arrangement. The demand needs tensile bars at x unless the bending ratio |M|/MRd is known and not above
        /// <see cref="DevelopmentDemandRatio"/> and the axial force is not tensile (N ≥ 0, compression positive); an unknown ratio counts as a demand.
        /// DevelopmentAvailable is true where the bars are developed, false only where the demand needs them and they are not (the nominal MRd is not
        /// usable there: outcome Satisfied becomes NotEvaluated "Parziale", data insufficient), null where the demand does not need them.
        /// Before 15/10/2026 every depth within lbd + a_l of the bar ends was "not available", also with M = 0, and the head anchorage was not used.
        /// </summary>
        public static void ApplyDevelopment(IEnumerable<PileBarRun> runs,IEnumerable<PileSectionCheck> checks)
        {
            var bars=runs.ToArray();
            foreach(var c in checks)
            {
                bool developed=bars.Length>0&&bars.All(run=>c.Action.Depth>=run.CapacityStart-1e-9&&c.Action.Depth<=run.CapacityEnd+1e-9&&run.Joints.All(j=>j.LengthPassed&&j.ArrangementPassed));
                bool required=!(c.BendingRatio<=DevelopmentDemandRatio)||c.Action.N<0;
                c.DevelopmentAvailable=developed?true:required?false:(bool?)null;
                if(c.DevelopmentAvailable==false)
                {
                    c.Data=DataStatus.Insufficient;
                    if(c.Outcome==EngineeringOutcome.Satisfied){c.Outcome=EngineeringOutcome.NotEvaluated;c.Status="Parziale";}
                    c.Message+=" Sviluppo delle barre o giunti non disponibili: MRd nominale non utilizzabile. Dettaglio locale da definire.";
                }
            }
        }
        /// <summary>Step of the developed fraction in <see cref="ApplyPartialDevelopment"/>: the fraction is rounded down to it (safe side), so few reduced services are built.</summary>
        public const double DevelopmentFractionStep = .05;
        /// <summary>
        /// Developed fraction of the bars at a depth (BUG-PO-5b of ANTHEA, user's decision of 26/10/2026): with the anchorage measured beyond the shift a_l,
        /// r = (l − a_l)/lbd within [0; 1], l the length from the nearest bar end (with the anchorage outside the model at the pile ends), the smallest of the
        /// runs. Null when a joint of the runs is not usable (the fraction does not apply).
        /// </summary>
        public static double? DevelopedFraction(IEnumerable<PileBarRun> runs,double depth)
        {
            double fraction=1;
            foreach(var run in runs)
            {
                if(!run.Joints.All(j=>j.LengthPassed&&j.ArrangementPassed))return null;
                double required=run.Anchorage+run.Shift;if(!(run.Anchorage>0))continue;
                // CapacityStart/End already hold the anchorage outside the model: the deficit is the distance still missing to them.
                double missing=Math.Max(Math.Max(0,run.CapacityStart-depth),Math.Max(0,depth-run.CapacityEnd));
                fraction=Math.Min(fraction,Math.Max(0,Math.Min(1,(required-missing-run.Shift)/run.Anchorage)));
            }
            return fraction;
        }
        /// <summary>
        /// Partial development (BUG-PO-5b of ANTHEA, user's decision of 26/10/2026: «ok»): after <see cref="ApplyDevelopment(IEnumerable{PileBarRun}, IEnumerable{PileSectionCheck})"/>,
        /// a depth where the demand needs the bars and they are not fully developed is checked with the bar strength reduced in proportion to the
        /// developed length, fyd·r with r of <see cref="DevelopedFraction"/> rounded down to <see cref="DevelopmentFractionStep"/>, instead of staying
        /// "Parziale". <paramref name="reduced"/> gives the service of the same section with the bar design strength multiplied by r (built by the caller,
        /// which owns the section solver). Below one step, or with an unusable joint, the depth stays as ApplyDevelopment left it. The nominal MRd and the
        /// shear are not changed: MRd± becomes the one of the reduced bars, with its bending ratio and outcome; DevelopedFraction records r.
        /// </summary>
        public static void ApplyPartialDevelopment(IEnumerable<PileBarRun> runs,IEnumerable<PileSectionCheck> checks,Func<double,PileReinforcement> reduced)
        {
            if(reduced==null)throw new ArgumentNullException(nameof(reduced));
            var bars=runs.ToArray();var services=new Dictionary<double,PileReinforcement>();
            foreach(var c in checks)
            {
                if(c.DevelopmentAvailable!=false)continue;
                double? exact=DevelopedFraction(bars,c.Action.Depth);if(!exact.HasValue)continue;
                double r=Math.Floor(exact.Value/DevelopmentFractionStep+1e-9)*DevelopmentFractionStep;if(r<DevelopmentFractionStep)continue;
                if(!services.TryGetValue(r,out var service)){service=reduced(r);services.Add(r,service);}
                var partial=service.Check(c.Action);
                c.DevelopedFraction=r;c.MRdPositive=partial.MRdPositive;c.MRdNegative=partial.MRdNegative;c.BendingRatio=partial.BendingRatio;
                if(!c.MRdPositive.HasValue||!c.MRdNegative.HasValue){c.Message+=" Sviluppo parziale r = "+r.ToString("0.00",CultureInfo.InvariantCulture)+": "+partial.Message;continue;}
                c.DevelopmentAvailable=true;
                bool complete=c.GoverningRatio.HasValue;
                c.Data=complete&&partial.Data==DataStatus.Ready?DataStatus.Ready:DataStatus.Insufficient;
                c.Outcome=c.BendingRatio>1||c.ShearRatio>1||c.MinimumReinforcementPassed==false?EngineeringOutcome.NotSatisfied:complete&&c.Data==DataStatus.Ready?EngineeringOutcome.Satisfied:EngineeringOutcome.NotEvaluated;
                c.Status=c.Outcome==EngineeringOutcome.NotSatisfied?"Non soddisfatto":c.Outcome==EngineeringOutcome.Satisfied?"Soddisfatto N–M–V; dettagli da completare":"Parziale";
                c.Message=c.Message.Replace(" Sviluppo delle barre o giunti non disponibili: MRd nominale non utilizzabile. Dettaglio locale da definire.","")
                    +" Sviluppo parziale delle barre: MRd con fyd·r, r = "+r.ToString("0.00",CultureInfo.InvariantCulture)+" (lunghezza ancorata oltre a_l / lbd, arrotondata per difetto).";
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
                foreach(var action in actions){var c=service.Check(action);if(c.Outcome!=EngineeringOutcome.Satisfied){pass=false;break;}}
                if(pass)return i;
            }
            return -1;
        }
    }
}
