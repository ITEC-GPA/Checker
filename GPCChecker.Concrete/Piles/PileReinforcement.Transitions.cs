using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Concrete.Piles
{
    public sealed class PileBarPosition
    {
        public double X { get; set; } // mm, nominal section coordinates
        public double Y { get; set; }
    }
    public sealed class PileBarPair
    {
        public PileBarPosition Upper { get; set; }
        public PileBarPosition Lower { get; set; }
    }
    public sealed class PileBarJoint
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public string UpperRun { get; set; }
        public string LowerRun { get; set; }
        public int Count { get; set; }
        public double Boundary { get; set; }
        public double Start { get; set; }
        public double End { get; set; }
        public double InitialLength { get; set; }
        public double RequiredLength { get; set; }
        public double DevelopmentLength { get; set; }
        public double AdoptedLength { get; set; }
        public double ActualLength => Math.Max(0,End-Start);
        public bool LengthPassed => ActualLength+1e-9>=AdoptedLength;
        public bool ArrangementPassed { get; set; } = true;
        public int MatchedCount => Pairs.Count;
        public double ActualLapPercent { get; set; } = 100;
        public List<PileBarPair> Pairs { get; set; } = new List<PileBarPair>();
        public string Note { get; set; }
    }
    public sealed partial class PileReinforcement
    {
        static bool SameRay(PileBarPosition a,PileBarPosition b)
        {
            double norm=Math.Sqrt((a.X*a.X+a.Y*a.Y)*(b.X*b.X+b.Y*b.Y));
            return norm>1e-6&&a.X*b.X+a.Y*b.Y>0&&Math.Abs(a.X*b.Y-a.Y*b.X)<=norm*1e-9;
        }
        // User-defined segment ends are physical cutting ends. Only the following
        // group moves upwards by its adopted lap. Identical cages are NOT merged.
        static IReadOnlyList<PileBarRun> BuildBarRuns(IReadOnlyList<PileDetailSegment> segments,double length)
        {
            if(!Finite(length)||length<=0)throw new ArgumentException("Lunghezza del palo non valida.");
            var runs=new List<PileBarRun>();int jointNumber=0;
            for(int i=0;i<segments.Count;i++)
            {
                var segment=segments[i];var service=segment.Service;
                if(service==null||segment.Start<0||segment.End<=segment.Start||segment.End>length||
                    (i>0&&segment.Start<segments[i-1].End-1e-9))
                    throw new ArgumentException("Tratti ordinati e non sovrapposti richiesti per la distinta.");
                var r=service.Detail(segment.Id,segment.Start,segment.End,length,segment.Checks);
                if(service.geometry.Bars.Any(b=>Math.Abs(b.Diameter-r.Diameter)>1e-8))
                    throw new ArgumentException("Distinta del palo: diametri misti nella stessa sezione non supportati.");
                r.Id="G"+(i+1).ToString("00");
                r.Positions=service.geometry.Bars.Select(b=>new PileBarPosition{X=b.X,Y=b.Y}).ToList();
                r.Start=segment.Start;r.End=segment.End;
                r.StartKind=segment.Start==0?"Testa":"Inizio tratto senza giunto disponibile";
                r.EndKind=segment.End==length?"Punta":"Fine tratto assegnata";
                if(i>0&&Math.Abs(segments[i-1].End-segment.Start)<1e-9)
                {
                    var upper=runs[i-1];
                    double initial=Math.Max(upper.InitialLap,r.InitialLap),required=Math.Max(upper.RequiredLap,r.RequiredLap);
                    double adopted=Math.Max(initial,required);
                    r.Start=Math.Max(0,segment.Start-adopted);r.StartKind="Arretramento per sovrapposizione";
                    var free=upper.Positions.ToList();var pairs=new List<PileBarPair>();
                    foreach(var lower in r.Positions)
                    {
                        var top=free.FirstOrDefault(p=>SameRay(p,lower));
                        if(top==null)continue;
                        free.Remove(top);pairs.Add(new PileBarPair{Upper=top,Lower=lower});
                    }
                    var joint=new PileBarJoint{Id="J"+(++jointNumber).ToString("00"),Kind="Confine dei tratti",
                        UpperRun=upper.Id,LowerRun=r.Id,Count=Math.Min(upper.Count,r.Count),Boundary=segment.Start,
                        Start=Math.Max(upper.Start,r.Start),End=upper.End,InitialLength=initial,RequiredLength=required,
                        AdoptedLength=adopted,Pairs=pairs,ArrangementPassed=upper.LapClearDistancePassed&&r.LapClearDistancePassed&&pairs.Count==Math.Min(upper.Count,r.Count),
                        Note="Fine gruppo superiore fissata dall'utente; inizio inferiore=confine−l0. Nessuna estensione automatica oltre la fine del tratto; lbd e a_l servono ai controlli, non allungano i pezzi. Disposizione trasversale e confinamento da completare."};
                    if(pairs.Count<joint.Count)joint.Note+=" Coppie radiali nominali disponibili "+pairs.Count+" su "+joint.Count+": abbinamento delle barre da definire.";
                    upper.Joints.Add(joint);r.Joints.Add(joint);
                }
                r.StartDevelopment=r.EndDevelopment=r.Anchorage+r.Shift;
                r.EndDevelopmentAvailable=r.CapacityStart<=r.TheoreticalStart+1e-9&&r.CapacityEnd>=r.TheoreticalEnd-1e-9;
                r.Pieces=PreferredStockPieces(r.Id,r.Start,r.End,service.p.StockLength,r.Lap,r.Count,r.Diameter).ToList();
                for(int j=1;j<r.Pieces.Count;j++)
                {
                    var current=r.Pieces[j];var previous=r.Pieces[j-1];
                    r.Joints.Add(new PileBarJoint{Id="J"+(++jointNumber).ToString("00"),Kind="Taglio commerciale",
                        UpperRun=r.Id,LowerRun=r.Id,Count=r.Count,Boundary=current.Start+r.Lap/2,
                        Start=current.Start,End=previous.End,InitialLength=r.InitialLap,RequiredLength=r.RequiredLap,
                        AdoptedLength=r.Lap,ArrangementPassed=r.LapClearDistancePassed,
                        Pairs=r.Positions.Select(p=>new PileBarPair{Upper=p,Lower=p}).ToList(),
                        Note="Suddivisione interna perché la barra eccede la lunghezza commerciale disponibile. Giunti raggruppati al 100%; lunghezza conteggiata una sola volta."});
                }
                r.Reference+=" Schema quote di taglio: primo gruppo 0–fine; successivi inizio−l0 fino a fine. Nessuna fusione fra tratti e nessuno sviluppo aggiunto alle quote assegnate. Verifiche di sviluppo indipendenti dalla geometria scelta.";
                runs.Add(r);
            }
            foreach(var r in runs)
                for(int i=0;i<r.Joints.Count;i++)for(int j=i+1;j<r.Joints.Count;j++)
                    if(Math.Min(r.Joints[i].End,r.Joints[j].End)-Math.Max(r.Joints[i].Start,r.Joints[j].Start)>1e-9)
                        r.Joints[i].ArrangementPassed=r.Joints[j].ArrangementPassed=false;
            for(int i=0;i<runs.Count;i++)
            {
                var r=runs[i];
                r.Status=r.Joints.Any(j=>!j.LengthPassed||!j.ArrangementPassed)?
                    "Quote rispettate; giunti insufficienti, interferenti o abbinamenti da definire":
                    "Quote di taglio rispettate; sviluppo alle estremità e dettagli da verificare";
                if(r.Joints.Count>0&&segments[i].Service.p.LapPercent<100)
                    r.Status+=". Giunti calcolati al 100%; sfalsamento richiesto dall'utente non ancora disposto";
                if(segments[i].Service.p.HeadAnchorage>0||segments[i].Service.p.ToeAnchorage>0)
                    r.Status+=". Sviluppi esterni del precedente schema conservati nell'archivio ma non aggiunti ai tagli";
            }
            return runs;
        }
    }
}
