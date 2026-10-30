using GPC.Model.Actions.Climate;
using GPC.Model.Actions.Traffic;
using static GPC.Design.Actions.Climate.ClimateMath;

namespace GPC.Design.Actions.Traffic;

public static partial class TransverseDistribution
{
    public static ClimateResult Summary(TransverseDistributionResult source)
    {
        if(source is null)throw new ArgumentNullException(nameof(source));
        var r=new ClimateResult{Standard=source.Method,Scope=(source.Converged?"Calcolo convergente":"RISULTATI PROVVISORI · convergenza non raggiunta")+" · quote del momento longitudinale nella sezione assegnata",Notes=source.Notes.ToList()};
        const string reference="Compatibilità elastica delle travi e traverso; modello meccanico, carichi assegnati";
        r.Parameters.Add(V("N","Modi longitudinali usati (0 per rigido)",source.Modes,"−","Troncamento adattivo",reference));
        r.Parameters.Add(V("residuo","Massimo residuo relativo del sistema",source.MaximumResidual,"−","|Ku−f| / scala locale",reference));
        r.Parameters.Add(V("εinfl","Variazione relativa delle linee di influenza",source.InfluenceConvergenceRelativeError,"−","Confronto fra troncamenti successivi",reference));
        r.Parameters.Add(V("εeq,infl","Equilibrio relativo delle linee di influenza",source.InfluenceEquilibriumRelativeError,"−","Somma dei momenti / statica globale",reference));
        foreach(var load in source.Cases)
        {
            var c=Case(r,load.Name,"Percentuali del momento, con segno. Torsione accessoria al troncamento indicato; vedere ipotesi.");
            c.Values.Add(V("Ptot","Carico verticale applicato",load.TotalLoadKn,"kN","Integrale delle pressioni",reference));
            c.Values.Add(V("Mstatico","Momento globale indipendente",load.StaticMomentKnM,"kN·m","Statica della trave semplicemente appoggiata",reference));
            c.Values.Add(V("Mtot","Somma dei momenti delle travi",load.CalculatedMomentKnM,"kN·m","Σ Mi",reference));
            c.Values.Add(V("εeq","Errore relativo di equilibrio",load.EquilibriumRelativeError,"−","|Σ Mi − Mstatico| / |Mstatico|",reference));
            c.Values.Add(V("εconv","Incremento relativo fra troncamenti",load.ConvergenceRelativeError,"−","max |Mi,N − Mi,N/2| / |Mstatico|",reference));
            foreach(var girder in load.Girders)
            {
                c.Values.Add(V(girder.Name+" %","Quota del momento longitudinale",girder.Percentage,"%","100 Mi / Σ Mi",reference));
                c.Values.Add(V(girder.Name+" M","Momento longitudinale",girder.MomentKnM,"kN·m","EI Σ λn² wn sin(λn x), oppure Courbon",reference));
                c.Values.Add(V(girder.Name+" T","Torsione accessoria",girder.TorsionKnM,"kN·m","GJ Σ λn θn cos(λn x)",reference));
            }
        }
        return r;
    }
}
