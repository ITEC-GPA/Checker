using GPC.Model.Actions.Climate;
using static GPC.Design.Actions.Climate.ClimateMath;

namespace GPC.Design.Actions.Climate;

public static class BridgeWindActions
{
    public static ClimateResult Calculate(BridgeWindInput x)
    {
        if (x is null) throw new ArgumentNullException(nameof(x));
        Positive(x.WidthM,"Larghezza impalcato d [m]"); Positive(x.TotalHeightM,"Ingombro htot [m]"); Range(x.BridgeLengthM,double.Epsilon,200,"Lunghezza totale ponte [m]");
        Range(x.DeckCentreHeightM,0,200,"Quota centro impalcato [m]"); Positive(x.StructuralFactor,"Coefficiente dinamico");
        Require(x.ApplicabilityConfirmed,"Confermare applicabilità CNR G.11: impalcato a sezione costante ordinario, lunghezza ≤200 m; esclusi archi, sospesi, strallati, ponti coperti/mobili e rilevanti curvature in pianta. Valutare separatamente dinamica e aeroelasticità.");
        double z=x.DeckCentreHeightM+x.TotalHeightM/2;
        var r=WindClimate.Calculate(x.Site,z); r.Standard+=" · coefficienti " + Cnr + " §G.11";
        r.Scope="Forze statiche per unità di lunghezza dell’impalcato e momenti torcenti; quattro combinazioni dei segni di portanza e momento.";
        double qp=r.Parameters.Single(p=>p.Symbol=="qp").Value;
        (double fx,double fy,double mz) Forces(double d)
        {
            double ratio=d/x.TotalHeightM;
            Require(ratio>=2 || x.AssignedDragCoefficient,"d/htot <2: assegnare cfX dalla CNR §G.10.3 (sezione rettangolare) o da prove documentate.");
            double cx=x.AssignedDragCoefficient?Positive(x.DragCoefficient,"cfX assegnato"):ratio<=5?1.85/ratio-.1:1.35/ratio;
            double cy=ratio<=5?.7+.1*ratio:1.2;
            return (qp*d*cx,qp*d*cy,qp*d*d*.2);
        }
        var single=Forces(x.WidthM);
        r.Parameters.Add(V("d/htot","Rapporto geometrico",x.WidthM/x.TotalHeightM,"–","d/htot","CNR G.11.1"));
        r.Parameters.Add(V("cfX","Coefficiente forza lungo il vento",single.fx/(qp*x.WidthM),"–",x.AssignedDragCoefficient?"assegnato":"1,85/r−0,10 per 2≤r≤5; 1,35/r per r>5","CNR (G.25a)"));
        r.Parameters.Add(V("|cfY|","Coefficiente portanza",single.fy/(qp*x.WidthM),"–","0,7+0,1r per r≤5; 1,2 oltre","CNR (G.25b)"));
        r.Parameters.Add(V("|cmZ|","Coefficiente momento",.2,"–","±0,2","CNR (G.25c)"));
        void Add(string label,(double fx,double fy,double mz) f)
        {
            foreach(int sy in new[]{-1,1}) foreach(int sm in new[]{-1,1})
            {
                var c=Case(r,$"{label} · Y{sy:+0;-0} M{sm:+0;-0}","X concorde al vento; Y verticale positivo verso l’alto; MZ torcente. Invertire il vento per la direzione opposta.");
                c.Values.Add(V("fX","Forza orizzontale statica",f.fx,"kN/m","qp · d · cfX","CNR §3.3 e G.11"));
                c.Values.Add(V("fY","Forza verticale statica",sy*f.fy,"kN/m","qp · d · cfY","CNR §3.3 e G.11"));
                c.Values.Add(V("mZ","Momento torcente statico",sm*f.mz,"kN·m/m","qp · d² · cmZ","CNR §3.3 e G.11"));
                c.Values.Add(V("fX,eq","Forza orizzontale equivalente",f.fx*x.StructuralFactor,"kN/m","cd assegnato · fX","CNR §3.4"));
            }
        }
        if(!x.AdjacentDeck) Add("Impalcato isolato",single);
        else
        {
            Positive(x.SecondWidthM,"Seconda larghezza [m]"); Range(x.GapM,0,double.MaxValue,"Interspazio [m]");
            var second=Forces(x.SecondWidthM); double total=x.WidthM+x.SecondWidthM+x.GapM, dg=(x.WidthM+x.SecondWidthM)/2+x.GapM;
            bool far=x.GapM/Math.Max(x.WidthM,x.SecondWidthM)>=.25;
            if(far) { Add("Impalcato 1 · isolato",single); Add("Impalcato 2 · isolato",second); }
            else
            {
                var both=Forces(total);
                if(x.CoupledDecks) Add("Coppia collegata · vento da impalcato 1",(Math.Max(both.fx,single.fx),both.fy,both.mz));
                else
                {
                    (double fx,double fy,double mz) Independent((double fx,double fy,double mz) f)=>(Math.Max(f.fx,.75*both.fx),Math.Max(f.fy,.5*both.fy+(both.mz-f.mz)/dg),f.mz);
                    Add("Impalcato 1 · indipendente",Independent(single)); Add("Impalcato 2 · indipendente",Independent(second));
                    Add("Pile comuni · coppia",(Math.Max(both.fx,single.fx),both.fy,both.mz));
                }
                Add("Coppia/pile · vento da impalcato 2",(Math.Max(both.fx,second.fx),both.fy,both.mz));
            }
            r.Notes.Add("Impalcati affiancati di forma simile e uguale htot: CNR G.11.2. Le azioni sugli impalcati indipendenti non si sommano per le pile comuni; usare i casi coppia/pile.");
        }
        r.Notes.Add("htot comprende l’ingombro effettivo di impalcato, arredi e traffico: sagoma convenzionale stradale 3 m, ferroviaria 4 m sopra il piano. Ripetere senza traffico per l’inviluppo.");
        r.Notes.Add("Il coefficiente dinamico assegnato è applicato alla sola componente lungo vento. Risposta trasversale/torsionale, distacco vortici, flutter, fatica e buffeting non sono verificati da questo calcolo statico.");
        return r;
    }
}
