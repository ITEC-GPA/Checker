using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Checkers.Concrete.Detailing;

namespace GPC.Checkers.Concrete.Piles
{
    public sealed class PileSubdivisionProposal
    {
        public double[] Boundaries { get; set; }
        public double? HalfMomentDepth { get; set; }
        public double? ChangeDepth { get; set; }
        public string Explanation { get; set; }
    }
    public sealed class PileLayout
    {
        public int Count { get; set; }
        public double Diameter { get; set; }
        public double LinkDiameter { get; set; }
        public double LinkSpacing { get; set; }
        public double SteelArea => Count*Math.PI*Diameter*Diameter/4;
    }
    public sealed partial class PileReinforcement
    {
        public static double CircularLinkRadius(double diameterMm,double coverMm,double linkDiameterMm)
        {
            double radius=diameterMm/2-coverMm-linkDiameterMm/2;
            if(!Finite(diameterMm+coverMm+linkDiameterMm)||diameterMm<=0||coverMm<0||linkDiameterMm<=0||radius<=0)throw new ArgumentException("Geometria della staffa non valida.");return radius;
        }
        /// <summary>Link station schedule. The following segment owns an internal boundary; only the last owns the toe. Cutting lengths need an explicit closure detail.</summary>
        public static double[] LinkStations(double start,double end,double maximumSpacingMm,bool last)
        {
            if(!Finite(start+end+maximumSpacingMm)||end<=start||maximumSpacingMm<=0)throw new ArgumentException("Quote e passo staffe validi richiesti.");
            int intervals=(int)Math.Ceiling((end-start)*1000/maximumSpacingMm-1e-10);intervals=Math.Max(1,intervals);
            return Enumerable.Range(0,intervals+(last?1:0)).Select(i=>start+(end-start)*i/intervals).ToArray();
        }
        /// <summary>User requested initial convention, metres. Independent from code-required lap.</summary>
        public static double InitialLap(double diameterMm,double factor=60)
        {
            if(!Finite(diameterMm)||!Finite(factor)||diameterMm<=0||factor<=0)throw new ArgumentException("Diametro e fattore di sovrapposizione positivi richiesti.");
            return Math.Ceiling(diameterMm*factor/100-1e-12)/10;
        }
        public IReadOnlyList<DetailingCheck> ConstructionChecks(double maximumCompressionKn=0)
        {
            if(!Finite(maximumCompressionKn))throw new ArgumentException("Compressione finita richiesta.");
            // Column is a user-selected detailing convention, not a reclassification of the geotechnical model.
            var kind=p.DetailingMode==PileDetailingMode.Beam?MemberDetailingKind.Beam:MemberDetailingKind.Column;
            var input=new MemberDetailingInput(kind,geometry,area,p.Fck,p.Fctm,p.Fyk,p.Fyd,p.Diameter,p.Diameter,p.Diameter,Math.Max(0,maximumCompressionKn)*1000,true,p.LinkDiameter,p.LinkSpacing,2,p.Aggregate,p.Cover,p.MinimumDurabilityCover,p.CoverDeviation,false,p.CompressionBarsRestrained,p.EndZonesConfirmed);
            var checks=MemberDetailingCalculator.Calculate(DetailingProfiles.Resolve(p.Standard),input).Checks.Where(c=>p.DetailingMode!=PileDetailingMode.CommonOnly||c.Key=="ClearSpacing"||c.Key=="NominalCover"||c.Key=="BarCoverMargin").ToList();
            if(p.DetailingMode==PileDetailingMode.CommonOnly)checks.Add(new DetailingCheck("MemberRulesExcluded",null,null,"",null,"Scelta utente","Prescrizioni di trave/pilastro escluse: verificare separatamente i dettagli del tipo di elemento."));
            void Rule(string key,double actual,double limit,bool passed,string expression)=>checks.Add(new DetailingCheck(key,actual,limit,key=="PileSteelArea"?"mm²":"mm",p.PileMinimumRequirements?(bool?)passed:null,"NTC 2018 §7.2.5",p.PileMinimumRequirements?expression:"Campo di applicabilità dei minimi pali da confermare; "+expression));
            double steel=geometry.Bars.Sum(b=>b.Area),spacing=8*geometry.Bars.Min(b=>b.Diameter);
            Rule("PileSteelArea",steel,.003*area,steel>=.003*area,"As ≥ 0,003 Ac");
            Rule("PileLinkDiameter",p.LinkDiameter,8,p.LinkDiameter>=8,"φ staffe ≥ 8 mm");
            Rule("PileLinkSpacing",p.LinkSpacing,spacing,p.LinkSpacing<=spacing,"s ≤ 8φ longitudinale minimo; escluse zone dissipative");
            return checks;
        }
        public static IReadOnlyList<PileLayout> Layouts(IEnumerable<int> counts,IEnumerable<double> diameters,IEnumerable<double> linkDiameters,IEnumerable<double> spacings)
        {
            var result=(from n in counts.Distinct() from d in diameters.Distinct() from t in linkDiameters.Distinct() from s in spacings.Distinct() select new PileLayout{Count=n,Diameter=d,LinkDiameter=t,LinkSpacing=s}).ToArray();
            if(result.Length==0||result.Any(l=>l.Count<4||l.Count%2!=0||!Finite(l.Diameter+l.LinkDiameter+l.LinkSpacing)||l.Diameter<=0||l.LinkDiameter<=0||l.LinkSpacing<=0))throw new ArgumentException("Catalogo: quantità pari ≥4; diametri e passi positivi richiesti.");
            return result.OrderBy(l=>l.SteelArea).ThenBy(l=>l.LinkDiameter*l.LinkDiameter/l.LinkSpacing).ThenBy(l=>l.Count).ToArray();
        }
        /// <summary>Preferred commercial lengths include lap. Last cut may be shorter; no extra capacity is credited to waste.</summary>
        public static IReadOnlyList<PileBarPiece> PreferredStockPieces(string id,double start,double end,double maximum,double lap,int count,double diameter)
        {
            if(!Finite(start+end+maximum+lap+diameter)||end<=start||maximum<=lap||lap<0||count<=0||diameter<=0)throw new ArgumentException("Lunghezze barre o sovrapposizione non valide.");
            var stock=new[]{6d,8,10,12}.Where(s=>s<=maximum+1e-9&&s>lap).ToArray();
            if(stock.Length==0)throw new ArgumentException("Nessuna lunghezza preferita 6/8/10/12 m disponibile entro il limite assegnato e maggiore della sovrapposizione.");
            var pieces=new List<PileBarPiece>();double x=start;
            while(x<end-1e-9)
            {
                double remaining=end-x,chosen=stock.FirstOrDefault(s=>s>=remaining-1e-9);
                if(chosen==0)
                {
                    chosen=stock.Last();
                    // Avoid an unnecessarily short terminal cut when another preferred length fits.
                    var alternatives=stock.Where(s=>remaining-s+lap>=3-1e-9).ToArray();if(alternatives.Length>0)chosen=alternatives.Last();
                }
                double b=Math.Min(end,x+chosen);
                pieces.Add(new PileBarPiece{Id=id+"."+(pieces.Count+1),Count=count,Diameter=diameter,Start=x,End=b,LapWithPrevious=pieces.Count==0?0:lap,StockLength=chosen});
                if(b>=end-1e-9)break;x=b-lap;
            }
            return pieces;
        }
        /// <summary>Software heuristic on a 0.5 m grid, minimum 3 m. Reserves one incoming lap after the first segment; assigned ends remain cutting ends.</summary>
        public static double[] SuggestBuildable(IReadOnlyList<PileSectionCheck> checks,IReadOnlyList<PileBarRun> runs,double length,double maximum,double requestedMinimum)
            =>ProposeBuildable(checks,runs,length,maximum,requestedMinimum).Boundaries;
        public static PileSubdivisionProposal ProposeBuildable(IReadOnlyList<PileSectionCheck> checks,IReadOnlyList<PileBarRun> runs,double length,double maximum,double requestedMinimum)
        {
            double minimum=Math.Max(3,requestedMinimum);
            if(!Finite(length+maximum+minimum)||length<minimum||maximum<=0)throw new ArgumentException("La proposta richiede lunghezza totale e tratti di almeno 3 m; per un palo più corto conservare il tratto unico.");
            double lap=runs.Select(r=>r.Lap).DefaultIfEmpty(0).Max();
            var stocks=new[]{6d,8,10,12}.Where(s=>s<=maximum+1e-9).ToArray();if(stocks.Length==0)throw new ArgumentException("Lunghezza massima: consentire almeno una barra preferita da 6/8/10/12 m.");
            var points=checks.Select(c=>c.Action).OrderBy(c=>c.Depth).ToArray();
            if(points.Any(c=>!Finite(c.Depth+c.M)))throw new ArgumentException("Diagramma del momento non valido.");
            double peak=points.Select(c=>Math.Abs(c.M)).DefaultIfEmpty(0).Max();double? target=null;
            if(peak>1e-12)
            {
                int maxIndex=Array.FindLastIndex(points,c=>Math.Abs(c.M)>=peak*(1-1e-10));
                for(int i=maxIndex+1;i<points.Length;i++)if(Math.Abs(points[i].M)<=peak/2&&Math.Abs(points[i-1].M)>peak/2)
                {
                    // Linear interpolation of absolute demand only within the same continuous branch.
                    double a=Math.Abs(points[i-1].M),b=Math.Abs(points[i].M);
                    target=points[i-1].Depth+(points[i].Depth-points[i-1].Depth)*(a-peak/2)/(a-b);break;
                }
            }
            var x=new List<double>{0};for(double z=.5;z<length-1e-9;z+=.5)x.Add(z);x.Add(length);int n=x.Count;
            double Edge(int i,int j)
            {
                double size=x[j]-x[i];if(size<minimum-1e-9)return double.PositiveInfinity;
                double needed=size+(i==0?0:lap),fit=stocks.FirstOrDefault(v=>v>=needed-1e-9);if(fit==0)return double.PositiveInfinity;
                var demand=checks.Where(c=>c.Action.Depth>=x[i]&&c.Action.Depth<=x[j]&&c.GoverningRatio.HasValue).Select(c=>c.GoverningRatio.Value).ToArray();
                return 1+(fit-needed)/2+(demand.Length==0?0:Math.Min(4,(demand.Max()-demand.Min())/.25));
            }
            var cost=Enumerable.Repeat(double.PositiveInfinity,n).ToArray();var tail=(double[])cost.Clone();var previous=new int[n];var next=new int[n];cost[0]=0;tail[n-1]=0;
            for(int j=1;j<n;j++)for(int i=j-1;i>=0&&x[j]-x[i]<=stocks.Last()+1e-9;i--){double c=cost[i]+Edge(i,j);if(c<cost[j]){cost[j]=c;previous[j]=i;}}
            if(double.IsInfinity(cost[n-1]))throw new ArgumentException("Nessuna suddivisione ≥3 m compatibile con barre 6/8/10/12 m e sovrapposizione iniziale dei tratti successivi; rivedere vincoli. Nessun tratto corto introdotto automaticamente.");
            for(int i=n-2;i>=0;i--)for(int j=i+1;j<n&&x[j]-x[i]<=stocks.Last()+1e-9;j++){double c=Edge(i,j)+tail[j];if(c<tail[i]){tail[i]=c;next[i]=j;}}
            int change=target.HasValue?Enumerable.Range(1,n-2).Where(i=>!double.IsInfinity(cost[i]+tail[i])).OrderBy(i=>Math.Abs(x[i]-target.Value)).ThenBy(i=>cost[i]+tail[i]).DefaultIfEmpty(-1).First():-1;
            var cuts=new List<double>();int end=change>0?change:n-1;for(int j=end;j>0;j=previous[j])cuts.Add(x[j]);cuts.Add(0);cuts.Reverse();
            if(change>0)for(int i=next[change];i<n;i=next[i]){cuts.Add(x[i]);if(i==n-1)break;}
            return new PileSubdivisionProposal{Boundaries=cuts.ToArray(),HalfMomentDepth=target,ChangeDepth=change>0?(double?)x[change]:null,Explanation=change>0?
                "Primo attraversamento discendente di |M|max/2 dopo l'ultimo massimo assoluto; quota realizzabile più vicina su griglia 0,5 m. Tratti ≥3 m, barre 6/8/10/12 m: primo tratto senza aggiunte, successivi con una sovrapposizione arretrata. La sezione successiva va dimensionata sulle azioni concomitanti: nessuna riduzione automatica di capacità.":
                "Nessun cambio Mmax/2 realizzabile (momento nullo, attraversamento assente o vincoli di lunghezza). Suddivisione costruttiva senza riduzione automatica dell'armatura."};
        }
    }
}
