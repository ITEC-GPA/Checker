using GPC.Model.Actions.Climate;
using GPC.Model.Actions.Construction;
using GPC.Model.Actions.General;
using GPC.Model.Stages;
using C = GPC.Design.Actions.Climate.ClimateMath;

namespace GPC.Design.Actions.Construction;

/// <summary>Construction load geometry and schedule, not a staged structural analysis or a combination generator.</summary>
public static class ConstructionActions
{
    private const string En="EN 1991-1-6:2005 §§4.11.1–4.11.2, Tabelle 4.1–4.2";
    private const string Equilibrium="Equilibrio delle sole azioni assegnate; assi x,y orizzontali, z verso l’alto";
    public static ConstructionActionsResult Calculate(ConstructionActionsInput input)
    {
        if(input is null)throw new ArgumentNullException(nameof(input));
        C.National(input.Standard,input.NationalParameters);
        C.Require(input.ConstructionStages is {Count:>0}&&input.ConstructionStages.All(s=>s is not null),"Inserire almeno una fase.");
        C.Require(input.ConstructionLoads is not null&&input.ConstructionLoads.All(l=>l is not null),"Elenco dei carichi non valido.");
        Names(input.ConstructionStages.Select(s=>s.Name),"fasi");Names(input.ConstructionLoads.Select(l=>l.Name),"carichi");
        foreach(var s in input.ConstructionStages)C.Range(s.DurationDays,0,double.MaxValue,"Durata fase");
        foreach(var l in input.ConstructionLoads)ValidateLoad(l,input);
        var result=new ConstructionActionsResult{Summary=C.Result(input.Standard,En,"Azioni caratteristiche per fase e loro risultanti esterne. Nessuna analisi di vincoli, puntelli, resistenza o redistribuzione.",input.NationalParameters)};
        result.Summary.Parameters.Add(C.V("n","Numero di fasi",input.ConstructionStages.Count,"−","assegnato","Piano di costruzione"));
        result.Summary.Notes.Add("Le fasi sono ordinate: i carichi si attivano all’inizio di StartStage e si disattivano all’inizio di StopStage; 0 mantiene il carico fino al termine. Il modello GPC StageDefinition registra gli eventi senza applicarli a una struttura.");
        result.Summary.Notes.Add("Le risultanti sommano i carichi simultanei definiti dall’utente, separati per natura; non sono combinazioni SLU/SLE. Posizioni alternative richiedono documenti o scenari distinti. Le aree vanno collocate anche nelle posizioni più sfavorevoli: il foglio non esegue un inviluppo automatico.");
        result.Summary.Notes.Add("Nei getti EN il carico di lavoro include già personale e piccoli mezzi: 0,75 kPa esterni all’area di lavoro, max(0,75; min(1,5; 0,10 γt)) nell’area 3×3 m, limitata alle dimensioni disponibili. Peso del getto e casseri sono aggiunti sull’area effettiva. I pannelli e i puntelli richiedono una verifica separata, comprese le pressioni orizzontali del calcestruzzo fresco.");
        result.Summary.Notes.Add("Piano di esecuzione: "+(string.IsNullOrWhiteSpace(input.ExecutionPlan)?"non specificato":input.ExecutionPlan));
        double day=0;
        for(int index=0;index<input.ConstructionStages.Count;index++)
        {
            int stage=index+1;var source=input.ConstructionStages[index];
            var active=input.ConstructionLoads.Where(l=>l.StartStage<=stage&&(l.StopStage==0||stage<l.StopStage)).ToArray();
            C.Require(!active.Any(a=>a.Model==ConstructionLoadModel.EnPersonnel&&active.Any(b=>b.Model==ConstructionLoadModel.EnConcreteCasting&&Overlap(a,b))),"Personale EN sovrapposto a un’area di getto: il personale è già compreso nel relativo carico di lavoro.");
            var changes=input.ConstructionLoads.Where(l=>l.StartStage==stage||l.StopStage==stage).Select(l=>new StageGroupChange(l.Name,StageGroupKind.Load,l.StartStage==stage?StageGroupAction.Activate:StageGroupAction.Deactivate,StageEventTiming.First)).ToArray();
            var calculated=new ConstructionStageResult{Name=source.Name,StartDay=day,EndDay=day+source.DurationDays,Definition=new StageDefinition(stage,source.DurationDays,groupChanges:changes,sourceRecord:input.ExecutionPlan),Loads=active.Select(Load).ToList()};
            C.Range(calculated.EndDay,0,double.MaxValue,"Fine fase");result.Stages.Add(calculated);day=calculated.EndDay;
            var c=C.Case(result.Summary,$"{stage} · {source.Name}",$"Assetto attivo da giorno {calculated.StartDay:G8} a {day:G8}, dopo gli eventi iniziali. Carichi verticali positivi verso il basso; momenti esterni secondo la mano destra.");
            c.Values.Add(C.V("t0","Inizio fase",calculated.StartDay,"giorni","somma durate precedenti","Piano di costruzione"));c.Values.Add(C.V("t1","Fine fase",day,"giorni","t0 + durata","Piano di costruzione"));
            foreach(var nature in new[]{ActionNature.PermanentG1,ActionNature.PermanentG2,ActionNature.Variable})
                c.Values.Add(C.V(nature==ActionNature.PermanentG1?"G1":nature==ActionNature.PermanentG2?"G2":"Qc","Risultante verticale per natura",calculated.Loads.Where(l=>l.Nature==nature).Sum(l=>l.VerticalDownKn),"kN","Σ (∫ q dA + F)",Equilibrium));
            c.Values.Add(C.V("Hx","Forza orizzontale x",calculated.Loads.Sum(l=>l.HorizontalXKn),"kN","Σ Hx",Equilibrium));c.Values.Add(C.V("Hy","Forza orizzontale y",calculated.Loads.Sum(l=>l.HorizontalYKn),"kN","Σ Hy",Equilibrium));
            c.Values.Add(C.V("Mx","Momento esterno rispetto all’origine",calculated.Loads.Sum(l=>l.MomentXKnM),"kN·m","Σ (−Q y − Hy z)",Equilibrium));
            c.Values.Add(C.V("My","Momento esterno rispetto all’origine",calculated.Loads.Sum(l=>l.MomentYKnM),"kN·m","Σ (Q x + Hx z)",Equilibrium));
            c.Values.Add(C.V("Mz","Momento esterno rispetto all’origine",calculated.Loads.Sum(l=>l.MomentZKnM),"kN·m","Σ (Hy x − Hx y)",Equilibrium));
            foreach(var l in calculated.Loads)
            {
                var original=active.Single(a=>a.Name==l.Name);string reference=original.Model is ConstructionLoadModel.Assigned or ConstructionLoadModel.SelfWeight?"Dato del progetto":En;
                foreach(var patch in l.Patches)c.Values.Add(C.V(l.Name+" · "+patch.Name,"Pressione verticale sulla zona indicata",patch.PressureKpa,"kPa",original.Model==ConstructionLoadModel.EnConcreteCasting?"Base + incremento locale (senza doppio conteggio)":"Valore caratteristico × fattore dinamico assegnato",reference));
                c.Values.Add(C.V(l.Name+" · F","Forza concentrata verticale",l.PointLoadKn,"kN","F caratteristica × fattore dinamico assegnato",reference));
                if(!string.IsNullOrWhiteSpace(original.Source))result.Summary.Notes.Add(l.Name+": "+original.Source);
            }
        }
        return result;
    }
    private static void ValidateLoad(ConstructionLoadInput l,ConstructionActionsInput input)
    {
        C.EnumValue(l.Model);C.Require(l.Nature is ActionNature.PermanentG1 or ActionNature.PermanentG2 or ActionNature.Variable,"Natura del carico di costruzione non ammessa.");
        C.Require(l.StartStage>=1&&l.StartStage<=input.ConstructionStages.Count,"Fase iniziale fuori dalla sequenza.");C.Require(l.StopStage==0||l.StopStage>l.StartStage&&l.StopStage<=input.ConstructionStages.Count,"La fase di disattivazione deve seguire l’attivazione ed esistere nella sequenza (0 = nessuna).");
        foreach(double coordinate in new[]{l.LeftXM,l.BottomYM,l.PointXM,l.PointYM,l.HorizontalHeightM,l.HorizontalXKn,l.HorizontalYKn})C.Range(coordinate,-double.MaxValue,double.MaxValue,"Coordinata / componente orizzontale");
        C.Positive(l.LengthM,"Lunghezza area");C.Positive(l.WidthM,"Larghezza area");C.Range(l.ThicknessM,0,double.MaxValue,"Spessore");C.Positive(l.UnitWeightKnM3,"Peso specifico");
        double minimum=l.Model==ConstructionLoadModel.Assigned?-double.MaxValue:0;
        foreach(double value in new[]{l.AssignedPressureKpa,l.PointLoadKn})C.Range(value,minimum,double.MaxValue,"Carico verticale");
        C.Range(l.FormworkKpa,0,double.MaxValue,"Peso dei casseri");C.Range(l.DynamicFactor,1,double.MaxValue,"Fattore dinamico");
        bool en=l.Model is ConstructionLoadModel.EnPersonnel or ConstructionLoadModel.EnBridgeStorage or ConstructionLoadModel.EnConcreteCasting;
        if(en){C.Require(input.Standard==ClimateStandard.EurocodeFirstGeneration&&input.RecommendedEnLoadsConfirmed,"I preset EN richiedono il ramo EN 1991-1-6:2005 e la conferma dei valori raccomandati applicabili.");C.Require(l.Nature==ActionNature.Variable,"I carichi di esecuzione EN Qc sono variabili; per il peso strutturale permanente usare Peso proprio con natura G1.");}
        if(l.Model==ConstructionLoadModel.Assigned||l.DynamicFactor>1)C.Require(!string.IsNullOrWhiteSpace(l.Source),"Documentare carichi assegnati e amplificazione dinamica.");
        if(l.Model==ConstructionLoadModel.EnConcreteCasting)
        {
            C.Range(l.WorkXOffsetM,0,l.LengthM-Math.Min(3,l.LengthM),"Posizione x area di getto");C.Range(l.WorkYOffsetM,0,l.WidthM-Math.Min(3,l.WidthM),"Posizione y area di getto");
            C.Require(l.PointLoadKn==0&&l.AssignedPressureKpa==0,"Il preset di getto include già il carico di lavoro; inserire eventuali altre azioni come righe separate documentate.");
        }
    }
    private static ConstructionLoadResult Load(ConstructionLoadInput l)
    {
        double weight=l.UnitWeightKnM3*l.ThicknessM;
        double q=l.Model switch{ConstructionLoadModel.SelfWeight=>weight,ConstructionLoadModel.EnPersonnel=>Math.Max(1,l.AssignedPressureKpa),ConstructionLoadModel.EnBridgeStorage=>Math.Max(.2,l.AssignedPressureKpa),ConstructionLoadModel.EnConcreteCasting=>weight+l.FormworkKpa+.75,_=>l.AssignedPressureKpa};
        var r=new ConstructionLoadResult{Name=l.Name,Nature=l.Nature,PointLoadKn=(l.Model==ConstructionLoadModel.EnBridgeStorage?Math.Max(100,l.PointLoadKn):l.PointLoadKn)*l.DynamicFactor,PointXM=l.PointXM,PointYM=l.PointYM,HorizontalXKn=l.HorizontalXKn*l.DynamicFactor,HorizontalYKn=l.HorizontalYKn*l.DynamicFactor};
        r.Patches.Add(new ConstructionPressurePatch{Name="Area completa",LeftXM=l.LeftXM,BottomYM=l.BottomYM,LengthM=l.LengthM,WidthM=l.WidthM,PressureKpa=q*l.DynamicFactor});
        if(l.Model==ConstructionLoadModel.EnConcreteCasting)r.Patches.Add(new ConstructionPressurePatch{Name="Incremento area di lavoro",LeftXM=l.LeftXM+l.WorkXOffsetM,BottomYM=l.BottomYM+l.WorkYOffsetM,LengthM=Math.Min(3,l.LengthM),WidthM=Math.Min(3,l.WidthM),PressureKpa=(C.Clamp(.1*weight,.75,1.5)-.75)*l.DynamicFactor});
        r.VerticalDownKn=r.PointLoadKn;r.MomentXKnM=-r.PointLoadKn*l.PointYM-r.HorizontalYKn*l.HorizontalHeightM;r.MomentYKnM=r.PointLoadKn*l.PointXM+r.HorizontalXKn*l.HorizontalHeightM;r.MomentZKnM=r.HorizontalYKn*l.PointXM-r.HorizontalXKn*l.PointYM;
        foreach(var p in r.Patches){double force=p.PressureKpa*p.LengthM*p.WidthM;r.VerticalDownKn+=force;r.MomentXKnM-=force*(p.BottomYM+p.WidthM/2);r.MomentYKnM+=force*(p.LeftXM+p.LengthM/2);}
        return r;
    }
    private static void Names(IEnumerable<string> names,string kind)
    {var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(string name in names)C.Require(!string.IsNullOrWhiteSpace(name)&&seen.Add(name.Trim()),"Nomi mancanti o duplicati nell’elenco "+kind+".");}
    private static bool Overlap(ConstructionLoadInput a,ConstructionLoadInput b)=>a.LeftXM<b.LeftXM+b.LengthM&&b.LeftXM<a.LeftXM+a.LengthM&&a.BottomYM<b.BottomYM+b.WidthM&&b.BottomYM<a.BottomYM+a.WidthM;
}
