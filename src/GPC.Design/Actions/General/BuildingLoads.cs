using GPC.Model.Actions.Climate;
using GPC.Model.Actions.General;
using static GPC.Design.Actions.Climate.ClimateMath;

namespace GPC.Design.Actions.General;

public static class BuildingLoads
{
    public static ClimateResult Calculate(BuildingLoadsInput x)
    {
        if(x is null)throw new ArgumentNullException(nameof(x));National(x.Standard,x.NationalParameters);EnumValue(x.Occupancy);EnumValue(x.Reduction);
        bool ntc=x.Standard==ClimateStandard.Ntc2018;string reference=ntc?"NTC 2018 §§3.1.3–3.1.4, Tab. 3.1.II":"EN 1991-1-1:2002 §§5.2.2, 6.3.1.2, 6.4";
        var r=Result(x.Standard,"EN 1991-1-1:2002", "Permanenti per strati e sovraccarichi degli edifici; casi globali e locali separati",x.NationalParameters);
        Positive(x.TributaryWidthM,"Larghezza tributaria");Positive(x.LoadedAreaM2,"Area caricata");Range(x.PartitionLineKnM,0,ntc?5:3,"Peso lineare partizioni equivalenti");
        Require(x.Layers is not null&&x.Layers.Count<=100,"Elenco strati mancante o superiore a 100 righe.");
        double g1=0,g2=0;
        foreach(var layer in x.Layers!)
        {
            if(layer is null)throw new ArgumentException("Strato nullo.");Require(!string.IsNullOrWhiteSpace(layer.Name),"Nome strato mancante.");Positive(layer.ThicknessM,"Spessore "+layer.Name);Positive(layer.UnitWeightKnM3,"Peso specifico "+layer.Name);
            double g=layer.ThicknessM*layer.UnitWeightKnM3;if(layer.Structural)g1+=g;else g2+=g;
            r.Parameters.Add(V(layer.Name,layer.Structural?"Permanente strutturale G1":"Permanente non strutturale G2",g,"kN/m²","g = spessore × peso specifico",reference));
        }
        double partition=0;
        if(x.PartitionLineKnM>0)
        {
            Require(x.PartitionDistributionConfirmed,"Confermare adeguata ripartizione trasversale; NTC: divisori di abitazioni/uffici, EN: partizioni mobili.");
            partition=ntc?.4*Math.Ceiling(x.PartitionLineKnM):x.PartitionLineKnM<=1?.5:x.PartitionLineKnM<=2?.8:1.2;
            if(ntc)g2+=partition;
            r.Parameters.Add(V("partizioni",ntc?"Equivalente permanente G2":"Partizioni mobili: aggiunta al sovraccarico Q",partition,"kN/m²","Valore per fascia di peso lineare",reference));
        }
        var nominal=Nominal(x.Occupancy);double q=nominal.q,p=nominal.p,h=nominal.h;
        bool assigned=x.AssignedImposedLoads||x.Occupancy==OccupancyCategory.Assigned;
        if(!ntc)Require(assigned,"EN: assegnare qk, Qk e Hk della categoria e dell’appendice nazionale documentata; i valori NTC non vengono trasferiti.");
        if(x.Occupancy is OccupancyCategory.StorageE1 or OccupancyCategory.VehiclesG)Require(assigned,"Depositi E1 e veicoli G richiedono carichi valutati per il progetto, non inferiori ai minimi NTC.");
        if(assigned)
        {
            q=Range(x.DistributedKnM2,0,double.MaxValue,"qk");p=Range(x.ConcentratedKn,0,double.MaxValue,"Qk per impronta");h=Range(x.HorizontalKnM,0,double.MaxValue,"Hk");
            if(ntc&&x.Occupancy!=OccupancyCategory.Assigned)Require(q>=nominal.q&&p>=nominal.p&&h>=nominal.h,"I carichi assegnati sono inferiori ai minimi della categoria NTC selezionata.");
        }
        double psi=x.Occupancy==OccupancyCategory.MaintenanceH?0:.7,alpha=1;
        bool ordinary=x.Occupancy>=OccupancyCategory.ResidentialA&&x.Occupancy<=OccupancyCategory.RetailD2;
        if(x.Reduction!=ImposedReduction.None)
        {
            Require(ordinary||ntc&&x.Occupancy==OccupancyCategory.MaintenanceH&&x.Reduction==ImposedReduction.LoadedArea,"Riduzione ammessa per categorie A–D; NTC anche H solo per area.");
            if(x.Reduction==ImposedReduction.LoadedArea){alpha=Math.Min(1,5d/7*psi+10/x.LoadedAreaM2);if(x.Occupancy>=OccupancyCategory.GatheringC1&&x.Occupancy<=OccupancyCategory.RetailD2)alpha=Math.Max(.6,alpha);}
            else{Require(x.Storeys>2&&x.Storeys<=200,"Riduzione per più di due piani, fino a 200.");alpha=(2+(x.Storeys-2)*psi)/x.Storeys;}
        }
        double variable=q+(ntc?0:partition),reduced=alpha*variable;
        r.Parameters.Add(V("α", "Riduzione del solo sovraccarico distribuito",alpha,"−",x.Reduction switch{ImposedReduction.LoadedArea=>"min(1, 5 ψ0/7 + 10/A); C,D ≥ 0,6",ImposedReduction.Storeys=>"[2 + (n−2) ψ0]/n",_=>"1"},reference));
        var global=Case(r,"Distribuito globale", "G1 e G2 distinti; Q non simultaneo al carico concentrato locale. Larghezza tributaria uniforme assegnata.");
        foreach(var item in new[]{("G1",g1),("G2",g2),("Qk",variable),("Qrid",reduced)})
        {global.Values.Add(V(item.Item1,"Carico superficiale",item.Item2,"kN/m²",item.Item1=="Qrid"?"α × Qk":"Somma dei contributi pertinenti",reference));global.Values.Add(V(item.Item1+"·b","Carico lineare sulla trave tributaria",item.Item2*x.TributaryWidthM,"kN/m","carico superficiale × larghezza tributaria",reference));}
        var local=Case(r,"Concentrato locale","Alternativo al sovraccarico distribuito. Permanenti da aggiungere nella verifica dell’elemento; nessuna riduzione del carico concentrato.");
        bool wheels=x.Occupancy is OccupancyCategory.VehiclesF or OccupancyCategory.VehiclesG;
        double side=ntc?x.Occupancy==OccupancyCategory.VehiclesF?.1:x.Occupancy==OccupancyCategory.VehiclesG?.2:.05:Positive(x.ContactSideM,"Lato impronta EN");
        local.Values.Add(V("Qk","Carico per impronta",p,"kN","Assegnato / tabellato",reference));local.Values.Add(V("n","Numero impronte",wheels?2:1,"−","F e G: due ruote",reference));
        local.Values.Add(V("a","Lato impronta",side,"m","Impronta quadrata",reference));local.Values.Add(V("p","Pressione di contatto",p/(side*side),"kN/m²","Qk / a²",reference));
        if(wheels)local.Values.Add(V("s","Interasse impronte",1.8,"m","Due ruote",reference));
        Case(r,"Orizzontale locale","Parapetti/partizioni; per F/G solo zone pedonali. Non comprende urti veicolari né spinte dei materiali stoccati.").Values.Add(V("Hk","Carico orizzontale lineare",h,"kN/m","Assegnato / tabellato",reference));
        r.Notes.Add("Riduzioni per area e numero di piani alternative, mai moltiplicate. La riduzione per piani si riferisce a colonne/pareti con piani della medesima categoria; non combinarla automaticamente con ulteriori riduzioni ψ.");
        r.Notes.Add(ntc?"Partizioni oltre 5 kN/m: modellare il posizionamento effettivo. Carichi dinamici atipici, macchinari e categorie industriali/speciali richiedono dati specifici.":"Partizioni mobili oltre 3 kN/m: modellare posizione e direzione effettive. Riduzioni EN con valori raccomandati (A0=10 m², ψ0=0,7 per A–D), da riscontrare nell’appendice nazionale dichiarata.");
        return r;
    }
    private static (double q,double p,double h) Nominal(OccupancyCategory c)=>c switch
    {
        OccupancyCategory.ResidentialA or OccupancyCategory.OfficeB1=>(2,2,1),OccupancyCategory.StairsAB=>(4,4,2),OccupancyCategory.OfficeB2=>(3,2,1),
        OccupancyCategory.GatheringC1=>(3,3,1),OccupancyCategory.GatheringC2=>(4,4,2),OccupancyCategory.GatheringC3 or OccupancyCategory.GatheringC4 or OccupancyCategory.GatheringC5=>(5,5,3),
        OccupancyCategory.ShopD1=>(4,4,2),OccupancyCategory.RetailD2=>(5,5,2),OccupancyCategory.StorageE1=>(6,7,1),OccupancyCategory.VehiclesF=>(2.5,10,1),OccupancyCategory.VehiclesG=>(5,50,1),OccupancyCategory.MaintenanceH=>(.5,1.2,1),_=>(0,0,0)
    };
}
