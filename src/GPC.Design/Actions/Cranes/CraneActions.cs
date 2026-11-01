using GPC.Model.Actions.Climate;
using GPC.Model.Actions.Cranes;
using C=GPC.Design.Actions.Climate.ClimateMath;

namespace GPC.Design.Actions.Cranes;

/// <summary>One overhead bridge crane on two straight runways. EN 1991-3:2006 load groups, not runway verification.</summary>
public static class CraneActions
{
    private const string En="EN 1991-3:2006 §§2.2–2.7, 2.10–2.11; Tabelle 2.2, 2.4–2.6";
    public static CraneActionsResult Calculate(CraneActionsInput i)
    {
        Validate(i);
        double phi2=i.SupplierPhi2?i.AssignedPhi2:Phi2(i.HoistingClass,i.HoistingSpeedMs);
        double phi3=1-i.ReleasedMassFraction*(i.ReleaseDevice==CraneReleaseDevice.Grab?1.5:2);
        var r=new CraneActionsResult{Summary=C.Result(i.Standard,En,"Azioni di un carroponte su due vie rettilinee. Gruppi alternativi caratteristici, di prova e accidentali; nessuna verifica della via di corsa o combinazione di più gru.",i.NationalParameters)};
        r.Summary.Parameters.Add(C.V("φ2","Sollevamento",phi2,"−",i.SupplierPhi2?"dato del costruttore":"φ2,min + β2 vh",En));
        r.Summary.Parameters.Add(C.V("e","Eccentricità locale della ruota, alternativa ±e",i.WheelEccentricityFactor*i.RailHeadWidthMm,"mm","e = ke br","EN 1991-3:2006 §2.5.2.1(2)"));
        r.Summary.Notes.Add("I gruppi della Tab. 2.2 sono azioni alternative: non sommare accelerazione, serpeggiamento e frenatura del carrello. φ1 = 0,9 e 1,1 sono entrambi conservati. Il rilascio può dare φ3 negativo: non viene troncato a zero; eventuali reazioni negative richiedono verifica del contatto e delle ritenute.");
        r.Summary.Notes.Add("I dati verticali sono componenti nominali statiche, prima dei fattori dinamici. Qc comprende ponte e carrello; Qh comprende il carico e gli accessori non inclusi in Qc. Le azioni del costruttore di accelerazione e urto del ponte sono rispettivamente prima di φ5 e φ7; le altre sono già caratteristiche/accidentali. Non importare valori già amplificati nei campi nominali.");
        r.Summary.Notes.Add("Coordinate x lungo la via, y dalla rotaia 1 alla 2. Carichi verticali positivi verso il basso. Modello a quattro ruote: ponte centrato, carrello centrato fra gli assi, due ruote per rotaia, ruote motrici sull’asse x=0. Forze del costruttore conservate con segno e posizione assegnati; non sono automaticamente specchiate. Le posizioni e le direzioni da esaminare sono quelle esplicitamente elencate, senza inviluppo strutturale automatico.");
        r.Summary.Notes.Add("Serpeggiamento, vento di esercizio, frenatura del carrello, urti e ribaltamento richiedono componenti del costruttore e valutazione di applicabilità: una lista vuota non dimostra l’assenza dell’azione. Il modello non determina fatica, vento fuori servizio, urto cinematico o stabilità del carroponte. Per azioni accidentali i valori sono di progetto; non sono aggiunti coefficienti parziali.");
        r.Summary.Notes.Add("Fonte: "+i.SupplierSource+". Valutazione delle azioni applicabili/escluse: "+i.ApplicabilityAssessment);
        if(i.Standard==ClimateStandard.Ntc2018)r.Summary.Notes.Add("Impostazione NTC: modello specialistico EN 1991-3:2006 esplicitamente adottato; coefficienti parziali e combinazioni del progetto restano esterni al foglio.");
        for(int p=0;p<i.CranePositions.Count;p++)
        {
            var pos=i.CranePositions[p];var wheels=Wheels(i,p);var aux=i.AuxiliaryActions.Where(a=>a.Position==0||a.Position==p+1).ToArray();
            void Add(int g,string name,double qc,double qh,CraneActionComponent? component,double horizontal,bool wind)
            {
                var c=new CraneGroupResult{Name=$"{p+1} · {pos.Name} · G{g} {name}",Position=p+1,Group=g,Accidental=g>=9,Test=g==8,CraneFactor=qc,HoistFactor=qh,HorizontalFactor=horizontal};
                foreach(var w in wheels){double q=qc*w.CraneWeightKn+qh*w.HoistLoadKn;c.Points.Add(new CranePointAction{Name=w.Name,Rail=w.Rail,XM=pos.CraneXM+w.XM,YM=w.Rail==1?0:i.LengthM,VerticalDownKn=q,EccentricTorsionKnM=Math.Abs(q)*i.WheelEccentricityFactor*i.RailHeadWidthMm/1000});}
                foreach(var a in aux.Where(a=>a.Component==component||wind&&a.Component==CraneActionComponent.InServiceWind))
                {double factor=a.Component==CraneActionComponent.InServiceWind?1:horizontal;c.Points.Add(new CranePointAction{Name=a.Name,XM=pos.CraneXM+a.XM,YM=a.YM,VerticalDownKn=factor*a.VerticalDownKn,LongitudinalKn=factor*a.LongitudinalKn,TransverseKn=factor*a.TransverseKn});}
                if(component==CraneActionComponent.BridgeAcceleration&&i.AutomaticBridgeAcceleration)Acceleration(i,pos,wheels,qh==0?0:g==8?i.DynamicTestRatio:1,horizontal,c.Points);
                r.Groups.Add(c);var summary=C.Case(r.Summary,c.Name,$"{(c.Accidental?"Accidentale, valori di progetto":c.Test?"Prova":"Caratteristico")}; fattori Qc={qc:G6}, Qh={qh:G6}. Componenti orizzontali: {component?.ToString()??"nessuna"}{(wind?" + vento di esercizio":"")}.");
                foreach(var point in c.Points)
                {
                    summary.Values.Add(C.V(point.Name+" · Q","Verticale (+ giù)",point.VerticalDownKn,"kN","φc Qc + φh Qh / componente assegnata",En));
                    summary.Values.Add(C.V(point.Name+" · Hx","Lungo via",point.LongitudinalKn,"kN","Componente compatibile del gruppo",En));summary.Values.Add(C.V(point.Name+" · Hy","Trasversale",point.TransverseKn,"kN","Componente compatibile del gruppo",En));
                }
                summary.Values.Add(C.V("ΣQ","Risultante verticale",c.Points.Sum(a=>a.VerticalDownKn),"kN","ΣQ","Equilibrio"));
                summary.Values.Add(C.V("ΣHx","Risultante longitudinale",c.Points.Sum(a=>a.LongitudinalKn),"kN","ΣHx","Equilibrio"));summary.Values.Add(C.V("ΣHy","Risultante trasversale",c.Points.Sum(a=>a.TransverseKn),"kN","ΣHy","Equilibrio"));
                summary.Values.Add(C.V("Mz","Momento nel piano delle vie",c.Points.Sum(a=>a.XM*a.TransverseKn-a.YM*a.LongitudinalKn),"kN·m","Σ(x Hy − y Hx)","Equilibrio"));
                if(c.Points.Any(a=>a.Rail!=0&&a.VerticalDownKn<0))r.Summary.Notes.Add(c.Name+": reazione verticale negativa; controllare contatto/ritenute.");
            }
            foreach(double phi1 in new[]{.9,1.1}){Add(1,$"sollevamento φ1={phi1:G2}",phi1,phi2,CraneActionComponent.BridgeAcceleration,i.Phi5,true);if(i.IncludeRelease)Add(2,$"rilascio φ1={phi1:G2}",phi1,phi3,CraneActionComponent.BridgeAcceleration,i.Phi5,true);}
            Add(3,"a vuoto con accelerazione",1,0,CraneActionComponent.BridgeAcceleration,i.Phi5,true);
            Add(4,"traslazione",i.Phi4,i.Phi4,CraneActionComponent.BridgeAcceleration,i.Phi5,true);
            Add(5,"serpeggiamento",i.Phi4,i.Phi4,CraneActionComponent.Skewing,1,true);
            Add(6,"accelerazione carrello",i.Phi4,i.Phi4,CraneActionComponent.CrabAcceleration,1,false);
            Add(7,"accessori residui",1,i.ResidualHoistFraction,null,0,false);
            if(i.IncludeTests)foreach(double phi1 in new[]{.9,1.1})
            {Add(8,$"prova dinamica φ1={phi1:G2}",phi1,i.DynamicTestRatio*.5*(1+phi2),CraneActionComponent.BridgeAcceleration,i.Phi5,true);Add(8,$"prova statica φ1={phi1:G2}",phi1,i.StaticTestRatio,null,0,true);}
            if(i.IncludeAccidental)
            {Add(9,"urto ponte",1,1,CraneActionComponent.BridgeBuffer,i.Phi7,false);Add(9,"urto carrello",1,1,CraneActionComponent.CrabBuffer,1,false);Add(10,"ribaltamento",1,1,CraneActionComponent.Tilting,1,false);}
        }
        foreach(var a in i.AuxiliaryActions)r.Summary.Notes.Add(a.Name+": "+a.Source);
        return r;
    }
    public static double Phi2(HoistingClass hoistingClass,double speedMs)
    {C.EnumValue(hoistingClass);C.Range(speedMs,0,double.MaxValue,"Velocità di sollevamento");int n=(int)hoistingClass+1;return 1+.05*n+.17*n*speedMs;}
    private static List<CraneWheelInput> Wheels(CraneActionsInput i,int p)
    {
        if(i.Distribution==CraneDistribution.SupplierWheelComponents)return i.SupplierWheels.Where(w=>w.Position==p+1).ToList();
        var result=new List<CraneWheelInput>();double right=i.CranePositions[p].CrabYM/i.LengthM;
        for(int rail=1;rail<=2;rail++)for(int wheel=0;wheel<2;wheel++)
        {double share=rail==1?1-right:right;result.Add(new CraneWheelInput{Name=$"R{rail}.{wheel+1}",Rail=rail,XM=wheel*i.WheelbaseM,CraneWeightKn=i.BridgeWeightKn/4+i.CrabWeightKn*share/2,HoistLoadKn=i.HoistLoadKn*share/2});}
        return result;
    }
    private static void Acceleration(CraneActionsInput i,CranePosition pos,List<CraneWheelInput> wheels,double loadRatio,double phi5,List<CranePointAction> points)
    {
        double total=wheels.Sum(w=>w.CraneWeightKn+loadRatio*w.HoistLoadKn);
        double left=wheels.Where(w=>w.Rail==1).Sum(w=>w.CraneWeightKn+loadRatio*w.HoistLoadKn)/total;
        double k=i.DriveForceKn*pos.TravelDirection,m=k*(left-.5)*i.LengthM;
        for(int rail=1;rail<=2;rail++)for(int axle=0;axle<2;axle++)
        {double ht=phi5*(rail==1?1-left:left)*m/i.WheelbaseM;points.Add(new CranePointAction{Name=$"Accelerazione R{rail}.{axle+1}",XM=pos.CraneXM+axle*i.WheelbaseM,YM=rail==1?0:i.LengthM,LongitudinalKn=axle==0?phi5*k/2:0,TransverseKn=axle==0?-ht:ht});}
    }
    private static void Validate(CraneActionsInput i)
    {
        if(i is null)throw new ArgumentNullException(nameof(i));C.National(i.Standard,i.NationalParameters);C.EnumValue(i.Distribution);C.EnumValue(i.HoistingClass);C.EnumValue(i.ReleaseDevice);
        C.Require(i.StaticComponentsConfirmed&&!string.IsNullOrWhiteSpace(i.SupplierSource)&&!string.IsNullOrWhiteSpace(i.ApplicabilityAssessment),"Confermare componenti nominali statiche e documentare fonte del costruttore e valutazione delle azioni applicabili/escluse.");
        C.Positive(i.LengthM,"Interasse vie");C.Positive(i.WheelbaseM,"Passo ruote");C.Positive(i.BridgeWeightKn,"Peso ponte");C.Range(i.CrabWeightKn,0,double.MaxValue,"Peso carrello");C.Positive(i.HoistLoadKn,"Portata nominale");C.Range(i.HoistingSpeedMs,0,double.MaxValue,"Velocità sollevamento");C.Range(i.AssignedPhi2,1,double.MaxValue,"φ2");C.Range(i.Phi4,1,double.MaxValue,"φ4");C.Require(i.Phi4!=1||i.TrackTolerancesConfirmed,"φ4=1 richiede conferma delle tolleranze EN 1993-6.");C.Range(i.Phi5,1,3,"φ5");C.Range(i.Phi7,1.25,1.6,"φ7");C.Range(i.DriveForceKn,0,double.MaxValue,"Forza motrice K");
        C.Positive(i.RailHeadWidthMm,"Testa rotaia");C.Range(i.WheelEccentricityFactor,0,.5,"Fattore eccentricità");C.Range(i.ResidualHoistFraction,0,1,"Frazione residua");C.Range(i.ReleasedMassFraction,0,1,"Frazione rilasciata");if(i.IncludeTests){C.Range(i.DynamicTestRatio,1.1,double.MaxValue,"Rapporto prova dinamica");C.Range(i.StaticTestRatio,1.25,double.MaxValue,"Rapporto prova statica");}
        C.Require(i.CranePositions is {Count:>0}&&i.CranePositions.Count<=100&&i.CranePositions.All(p=>p is not null),"Elenco posizioni non valido (1–100).");C.Require(i.CranePositions.Select(p=>p.Name).Distinct().Count()==i.CranePositions.Count,"Nomi delle posizioni duplicati.");
        foreach(var p in i.CranePositions){C.Require(!string.IsNullOrWhiteSpace(p.Name),"Nome posizione mancante.");C.Range(p.CraneXM,-double.MaxValue,double.MaxValue,"Posizione lungo via");C.Range(p.CrabYM,0,i.LengthM,"Posizione carrello");C.Require(p.TravelDirection==1||p.TravelDirection==-1,"Direzione di marcia: +1 o −1.");}
        C.Require(i.SupplierWheels is not null&&i.SupplierWheels.All(w=>w is not null)&&i.AuxiliaryActions is not null&&i.AuxiliaryActions.All(a=>a is not null),"Elenchi ruote/azioni non validi.");
        if(i.Distribution==CraneDistribution.SupplierWheelComponents)
        {
            C.Require(!i.AutomaticBridgeAcceleration,"La distribuzione assegnata richiede anche le azioni orizzontali del costruttore.");
            for(int p=1;p<=i.CranePositions.Count;p++){var wheels=i.SupplierWheels.Where(w=>w.Position==p).ToArray();C.Require(wheels.Length>=2&&wheels.Any(w=>w.Rail==1)&&wheels.Any(w=>w.Rail==2),"Ogni posizione richiede ruote su entrambe le rotaie.");C.Require(wheels.Select(w=>w.Name).Distinct().Count()==wheels.Length,"Nomi delle ruote duplicati nella posizione.");}
            foreach(var w in i.SupplierWheels){C.Require(w.Position>=1&&w.Position<=i.CranePositions.Count&&w.Rail>=1&&w.Rail<=2&&!string.IsNullOrWhiteSpace(w.Name),"Ruota non valida.");C.Range(w.XM,-double.MaxValue,double.MaxValue,"Ascissa ruota");C.Range(w.CraneWeightKn,0,double.MaxValue,"Quota peso gru");C.Range(w.HoistLoadKn,0,double.MaxValue,"Quota carico sollevato");}
        }
        foreach(var a in i.AuxiliaryActions)
        {
            C.EnumValue(a.Component);C.Require(a.Position>=0&&a.Position<=i.CranePositions.Count&&!string.IsNullOrWhiteSpace(a.Name)&&!string.IsNullOrWhiteSpace(a.Source),"Documentare nome, posizione e fonte di ogni azione del costruttore.");
            foreach(double v in new[]{a.XM,a.YM,a.VerticalDownKn,a.LongitudinalKn,a.TransverseKn})C.Range(v,-double.MaxValue,double.MaxValue,"Componente del costruttore");
            C.Require(!i.AutomaticBridgeAcceleration||a.Component!=CraneActionComponent.BridgeAcceleration,"Accelerazione automatica e del costruttore si escludono.");
        }
    }
}
