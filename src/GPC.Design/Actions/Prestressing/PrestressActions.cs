using GPC.Design.Actions.TimeEffects;
using GPC.Model.Actions.Climate;
using GPC.Model.Actions.Prestressing;
using GPC.Model.Materials;
using C=GPC.Design.Actions.Climate.ClimateMath;

namespace GPC.Design.Actions.Prestressing;

/// <summary>Planar tendon, constant section and assigned concrete stress changes. Friction, compatible anchorage draw-in,
/// elastic shortening, relaxation and simplified coupled deferred losses. No structural or anchorage-zone verification.</summary>
public static class PrestressActions
{
    private const string En="EN 1992-1-1:2004 §§3.3.2, 5.10.2–5.10.6";
    private const string Ntc="NTC 2018 §§4.1.8.1.2, 4.1.8.1.5; modelli di perdita EN 1992-1-1:2004";
    public static PrestressActionsResult Calculate(PrestressActionsInput i)
    {
        if(i is null)throw new ArgumentNullException(nameof(i));Validate(i);
        double h0=ConcreteTimeEffects.NotionalSize(i.ConcreteAreaMm2,i.DryingPerimeterMm);
        var law=new ConcreteDeformationLaw(i.Standard,i.FckMpa,i.Cement,i.RelativeHumidityPercent,h0,i.DryingStartDays);
        var coarse=Solve(i,law,i.ProfileIntervals);var result=Solve(i,law,i.ProfileIntervals*2);
        double error=0;
        for(int s=0;s<coarse.Stages.Count;s++)for(int n=0;n<coarse.Stages[s].Points.Count;n++)
            error=Math.Max(error,Math.Abs(coarse.Stages[s].Points[n].EffectiveStressMpa-result.Stages[s].Points[n*2].EffectiveStressMpa));
        result.MaxDiscretizationMpa=error;result.Converged=error<=.05+.0001*i.JackingStressMpa;
        Summary(i,law,result);return result;
    }
    /// <summary>Constant-stress relaxation loss in MPa. 20 °C, hours since tensioning, rho1000 in percent.
    /// The final conventional value is capped at 500000 h per §3.3.2(8).</summary>
    public static double RelaxationLoss(PrestressRelaxationClass kind,double rho1000Percent,double stressMpa,double fpkMpa,double hours)
    {
        C.EnumValue(kind);C.Range(rho1000Percent,0,100,"ρ1000");C.Positive(fpkMpa,"fpk");C.Range(stressMpa,0,fpkMpa,"Tensione di rilassamento");C.Range(hours,0,double.MaxValue,"Ore di rilassamento");
        if(hours==0||stressMpa==0||rho1000Percent==0)return 0;
        double mu=stressMpa/fpkMpa,a=kind==PrestressRelaxationClass.OrdinaryWireStrand?5.39:kind==PrestressRelaxationClass.LowRelaxationWireStrand?.66:1.98;
        double b=kind==PrestressRelaxationClass.OrdinaryWireStrand?6.7:kind==PrestressRelaxationClass.LowRelaxationWireStrand?9.1:8;
        return stressMpa*a*rho1000Percent*Math.Exp(b*mu)*Math.Pow(Math.Min(hours,500000)/1000,.75*(1-mu))*1e-5;
    }
    /// <summary>EN Annex D equivalent-time continuation after an instantaneous stress change.
    /// Current stress excludes the already consumed relaxation loss; previous loss is never counted twice.</summary>
    public static double SubsequentRelaxationLoss(PrestressRelaxationClass kind,double rho1000Percent,double currentStressMpa,double previousLossMpa,double fpkMpa,double intervalHours)
    {
        C.Range(previousLossMpa,0,double.MaxValue,"Rilassamento già avvenuto");C.Range(intervalHours,0,double.MaxValue,"Intervallo di rilassamento");C.Positive(currentStressMpa,"Tensione dopo variazione istantanea");
        double reference=currentStressMpa+previousLossMpa;
        double at1000=RelaxationLoss(kind,rho1000Percent,reference,fpkMpa,1000);
        if(intervalHours==0||at1000==0)return 0;
        C.Require(reference<fpkMpa,"Tensione equivalente di rilassamento fuori campo.");
        double exponent=.75*(1-reference/fpkMpa),equivalent=previousLossMpa==0?0:1000*Math.Pow(previousLossMpa/at1000,1/exponent);
        if(double.IsPositiveInfinity(equivalent))return 0; // already beyond the conventional final relaxation of the new state
        return Math.Max(0,RelaxationLoss(kind,rho1000Percent,reference,fpkMpa,equivalent+intervalHours)-previousLossMpa);
    }
    private static void Validate(PrestressActionsInput i)
    {
        C.National(i.Standard,i.NationalParameters);C.EnumValue(i.Method);C.EnumValue(i.Profile);C.EnumValue(i.Jacking);C.EnumValue(i.Relaxation);
        foreach(var p in new[]{(i.LengthM,"L"),(i.TendonAreaMm2,"Ap"),(i.TendonModulusMpa,"Ep"),(i.FpkMpa,"fpk"),(i.Fp01kMpa,"fp0,1k"),(i.ConcreteAreaMm2,"Ac"),(i.ConcreteInertiaMm4,"Ic"),(i.DryingPerimeterMm,"u")})C.Positive(p.Item1,p.Item2);
        C.Require(i.Fp01kMpa<=i.FpkMpa,"fp0,1k non può superare fpk.");C.Range(i.JackingStressMpa,0,i.Fp01kMpa,"Tensione di tiro (campo elastico dell’acciaio)");C.Positive(i.JackingStressMpa,"Tensione di tiro");
        foreach(double e in new[]{i.StartEccentricityM,i.MiddleEccentricityM,i.EndEccentricityM})C.Range(e,-i.LengthM,i.LengthM,"Eccentricità");
        C.Range(i.FrictionCoefficient,0,1,"μ attrito");C.Range(i.WobbleRadM,0,1,"k deviazione involontaria [rad/m]");C.Range(i.AnchorageSlipMm,0,double.MaxValue,"Rientro ancoraggio");
        C.Require(!string.IsNullOrWhiteSpace(i.SystemSource),"Documentare sistema, materiali, attrito e rientro (ETA / fornitore / dati del progetto).");
        C.Require(!string.IsNullOrWhiteSpace(i.ConcreteStressSource),"Documentare le tensioni nel calcestruzzo ricavate dal modello strutturale.");
        C.Range(i.TransferAgeDays,3.001,100000,"Età di trasferimento (>3 giorni)");C.Range(i.JackingAgeDays,-365,i.TransferAgeDays,"Età al tiro");
        if(i.Method!=PrestressingMethod.PreTensioned)C.Require(i.JackingAgeDays==i.TransferAgeDays,"Post-tensione: tiro e trasferimento devono avere la stessa età nel modello semplificato.");
        else C.Require(i.Profile==TendonProfile.Straight,"Pretensione: supportato il banco rettilineo; deviatori e maturazione termica richiedono un modello specifico.");
        if(i.Jacking==JackingEnd.BothSymmetric)C.Require(Math.Abs(i.StartEccentricityM-i.EndEccentricityM)<=1e-10,"Tiro bilaterale: profilo simmetrico, tiri uguali e rientri uguali obbligatori.");
        C.Range(i.SuccessiveTendons,1,100000,"Numero di cavi identici tesati in sequenza");C.Range(i.ConcreteStressChangeMpa,0,double.MaxValue,"Incremento compressivo nel cls");C.Range(i.ConcreteQpStressMpa,0,double.MaxValue,"Compressione quasi permanente nel cls");
        C.Range(i.TendonQpStressIncrementMpa,-i.FpkMpa,i.FpkMpa,"Incremento nel cavo dovuto alle azioni quasi permanenti");
        C.Range(i.CertifiedRho1000Percent,0,100,"Rilassamento certificato");foreach(double factor in new[]{i.EnJackingFpkFactor,i.EnJackingFp01Factor,i.EnInitialFpkFactor,i.EnInitialFp01Factor})C.Range(factor,0,1,"Fattori limite EN");
        C.Require(i.ProfileIntervals>=20&&i.ProfileIntervals<=2000&&i.ProfileIntervals%2==0,"Intervalli del cavo: numero pari fra 20 e 2000.");
        C.Require(i.Observations is {Count:>0}&&i.Observations.All(o=>o is not null&&!string.IsNullOrWhiteSpace(o.Name)),"Inserire età di osservazione denominate.");foreach(var o in i.Observations!)C.Range(o.AgeDays,i.TransferAgeDays,100000,"Età di osservazione");
    }
    private sealed record Geometry(double X,double Eccentricity,double S,double Angle,double Theta);
    private static Geometry[] Profile(PrestressActionsInput i,int intervals)
    {
        double a=i.Profile==TendonProfile.Straight?0:2*(i.StartEccentricityM+i.EndEccentricityM-2*i.MiddleEccentricityM)/(i.LengthM*i.LengthM);
        double b=(i.EndEccentricityM-i.StartEccentricityM-a*i.LengthM*i.LengthM)/i.LengthM,angle0=Math.Atan(b),s=0;
        double ArcDensity(double x)=>Math.Sqrt(1+Math.Pow(2*a*x+b,2));
        var points=new Geometry[intervals+1];double gauss=Math.Sqrt(3.0/5);
        for(int n=0;n<=intervals;n++)
        {
            double x=i.LengthM*n/intervals;
            if(n>0){double previous=i.LengthM*(n-1)/intervals,half=(x-previous)/2,mid=(x+previous)/2;s+=half*(5.0/9*ArcDensity(mid-half*gauss)+8.0/9*ArcDensity(mid)+5.0/9*ArcDensity(mid+half*gauss));}
            double angle=Math.Atan(2*a*x+b);C.Require(Math.Abs(angle)<=Math.PI/18,"Inclinazione del cavo oltre 10°: fuori dal limite implementativo del modello longitudinale delle perdite.");points[n]=new(x,i.StartEccentricityM+b*x+a*x*x,s,angle,Math.Abs(angle-angle0));
        }
        return points;
    }
    private static double Integral(Geometry[] g,double[] values)
    {double sum=0;for(int n=1;n<g.Length;n++)sum+=(g[n].S-g[n-1].S)*(values[n]+values[n-1])/2;return sum;}
    private static double[] Anchored(Geometry[] g,double[] q,double jack,double slip,double modulus)
    {
        var initial=q.Select(v=>jack*Math.Exp(-v)).ToArray();if(slip==0)return initial;
        double max=Integral(g,initial)*1000/modulus;C.Require(slip<max,"Il rientro esaurisce l’allungamento disponibile: il cavo perderebbe tutta la precompressione.");
        double lo=0,hi=jack;
        for(int iteration=0;iteration<90;iteration++)
        {
            double anchor=(lo+hi)/2;
            var losses=q.Select((v,n)=>Math.Max(0,initial[n]-anchor*Math.Exp(v))).ToArray();
            double recovery=Integral(g,losses)*1000/modulus;
            if(recovery>slip)lo=anchor;else hi=anchor;
        }
        double final=(lo+hi)/2;return q.Select((v,n)=>Math.Min(initial[n],final*Math.Exp(v))).ToArray();
    }
    private static PrestressActionsResult Solve(PrestressActionsInput i,ConcreteDeformationLaw law,int intervals)
    {
        var g=Profile(i,intervals);bool pre=i.Method==PrestressingMethod.PreTensioned;
        double mu=pre?0:i.FrictionCoefficient,k=pre?0:i.WobbleRadM,totalS=g.Last().S,totalTheta=g.Last().Theta;
        var qleft=g.Select(p=>mu*(p.Theta+k*p.S)).ToArray();var qright=g.Select(p=>mu*(totalTheta-p.Theta+k*(totalS-p.S))).ToArray();
        double[] friction,anchored;
        if(i.Jacking==JackingEnd.Left)
        {friction=qleft.Select(q=>i.JackingStressMpa*Math.Exp(-q)).ToArray();anchored=Anchored(g,qleft,i.JackingStressMpa,i.AnchorageSlipMm,i.TendonModulusMpa);}
        else if(i.Jacking==JackingEnd.Right)
        {
            friction=qright.Select(q=>i.JackingStressMpa*Math.Exp(-q)).ToArray();
            var reverse=g.Reverse().Select(p=>p with{S=totalS-p.S}).ToArray();anchored=Anchored(reverse,qright.Reverse().ToArray(),i.JackingStressMpa,i.AnchorageSlipMm,i.TendonModulusMpa).Reverse().ToArray();
        }
        else
        {
            friction=qleft.Select((q,n)=>i.JackingStressMpa*Math.Exp(-Math.Min(q,qright[n]))).ToArray();
            int half=intervals/2;var partial=Anchored(g.Take(half+1).ToArray(),qleft.Take(half+1).ToArray(),i.JackingStressMpa,i.AnchorageSlipMm,i.TendonModulusMpa);
            anchored=Enumerable.Range(0,intervals+1).Select(n=>partial[n<=half?n:intervals-n]).ToArray();
        }
        double rho=i.CertifiedRelaxation?i.CertifiedRho1000Percent:i.Relaxation==PrestressRelaxationClass.OrdinaryWireStrand?8:i.Relaxation==PrestressRelaxationClass.LowRelaxationWireStrand?2.5:4;
        double j=pre?1:(i.SuccessiveTendons-1.0)/(2*i.SuccessiveTendons),elastic=i.TendonModulusMpa/law.EcmAt(i.TransferAgeDays)*j*i.ConcreteStressChangeMpa;
        double shortHours=pre?(i.TransferAgeDays-i.JackingAgeDays)*24:0;
        var shortRelax=anchored.Select(s=>RelaxationLoss(i.Relaxation,rho,s,i.FpkMpa,shortHours)).ToArray();var initial=anchored.Select((s,n)=>s-shortRelax[n]-elastic).ToArray();
        C.Require(initial.All(s=>s>0),"Le perdite immediate esauriscono la tensione del cavo; rivedere il modello o i dati.");
        double ntcFpk=pre?.8:.75,ntcFpy=pre?.9:.85;
        double jackLimit=i.Standard==ClimateStandard.Ntc2018?Math.Min(ntcFpk*i.FpkMpa,ntcFpy*i.Fp01kMpa):Math.Min(i.EnJackingFpkFactor*i.FpkMpa,i.EnJackingFp01Factor*i.Fp01kMpa);
        double initialLimit=i.Standard==ClimateStandard.Ntc2018?jackLimit:Math.Min(i.EnInitialFpkFactor*i.FpkMpa,i.EnInitialFp01Factor*i.Fp01kMpa);
        var result=new PrestressActionsResult{Rho1000Percent=rho,JackingLimitMpa=jackLimit,InitialLimitMpa=initialLimit,JackingElongationMm=Integral(g,friction)*1000/i.TendonModulusMpa,AnchorageRecoveryMm=Integral(g,friction.Zip(anchored,(f,a)=>f-a).ToArray())*1000/i.TendonModulusMpa,WithinStressLimits=i.JackingStressMpa<=jackLimit&&initial.All(s=>s<=initialLimit)&&i.ConcreteQpStressMpa<=.45*law.FckAt(i.TransferAgeDays)};
        var creep=law.CreepAtLoading(i.TransferAgeDays);double shrink0=law.ShrinkageAt(i.TransferAgeDays),epEc=i.TendonModulusMpa/law.Ecm28Mpa;
        bool unbonded=i.Method==PrestressingMethod.PostTensionedUnbonded;
        foreach(var observation in i.Observations)
        {
            double phi=creep.AtAge(observation.AgeDays).CreepCoefficient,shrink=Math.Max(0,shrink0-law.ShrinkageAt(observation.AgeDays));
            double hours=(observation.AgeDays-i.TransferAgeDays)*24;
            var relax=initial.Select((s,n)=>SubsequentRelaxationLoss(i.Relaxation,rho,s+i.TendonQpStressIncrementMpa,shortRelax[n],i.FpkMpa,hours)).ToArray();
            double meanRelax=Integral(g,relax)/totalS,meanEccSquare=Integral(g,g.Select(p=>p.Eccentricity*p.Eccentricity*1e6).ToArray())/totalS;
            var stage=new PrestressStageResult{Name=observation.Name,AgeDays=observation.AgeDays,CreepCoefficient=phi,ShrinkageSinceTransfer=-shrink};
            for(int n=0;n<g.Length;n++)
            {
                var p=g[n];double eccSquare=unbonded?meanEccSquare:p.Eccentricity*p.Eccentricity*1e6;
                double denominator=1+epEc*i.TendonAreaMm2/i.ConcreteAreaMm2*(1+i.ConcreteAreaMm2*eccSquare/i.ConcreteInertiaMm4)*(1+.8*phi);
                double drying=i.IncludeDeferredLosses?i.TendonModulusMpa*shrink/denominator:0,viscous=i.IncludeDeferredLosses?epEc*phi*i.ConcreteQpStressMpa/denominator:0,relaxation=i.IncludeDeferredLosses?.8*(unbonded?meanRelax:relax[n])/denominator:0;
                double deferred=drying+viscous+relaxation,effective=initial[n]-deferred;
                C.Require(effective>0,"Le perdite differite esauriscono la precompressione; il modello semplificato non è applicabile.");
                double force=effective*i.TendonAreaMm2/1000,horizontal=force*Math.Cos(p.Angle);
                stage.Points.Add(new PrestressProfilePoint{XM=p.X,EccentricityM=p.Eccentricity,ArcLengthM=p.S,TangentRadians=p.Angle,AngularDeviationRad=p.Theta,JackingStressMpa=i.JackingStressMpa,AfterFrictionMpa=friction[n],AfterAnchoringMpa=anchored[n],ShortRelaxationLossMpa=shortRelax[n],ElasticLossMpa=elastic,InitialStressMpa=initial[n],ShrinkageContributionMpa=drying,CreepContributionMpa=viscous,RelaxationContributionMpa=relaxation,DeferredLossMpa=deferred,EffectiveStressMpa=effective,EffectiveForceKn=force,EffectivePrestrain=effective/i.TendonModulusMpa,ConcreteAxialForceKn=-horizontal,ConcretePrimaryMomentKnM=horizontal*p.Eccentricity,WithinInitialStressLimit=initial[n]<=initialLimit});
            }
            result.Stages.Add(stage);
        }
        return result;
    }
    private static void Summary(PrestressActionsInput i,ConcreteDeformationLaw law,PrestressActionsResult r)
    {
        r.Summary.Standard=i.Standard==ClimateStandard.Ntc2018?Ntc:En;
        r.Summary.Scope="Cavo piano o famiglia equivalente, sezione costante, calcestruzzo integro a 20 °C. Tensioni assegnate nel cls uniformi lungo il cavo. Tiro, ancoraggio, trasferimento e stato efficace distinti.";
        var parameters=r.Summary.Parameters;
        parameters.Add(C.V("σjack,lim","Limite adottato al tiro",r.JackingLimitMpa,"MPa","min(k1 fpk; k2 fp0,1k)",r.Summary.Standard));parameters.Add(C.V("σpm0,lim","Limite iniziale dopo trasferimento",r.InitialLimitMpa,"MPa","min(k7 fpk; k8 fp0,1k)",r.Summary.Standard));
        parameters.Add(C.V("ρ1000","Rilassamento a 1000 ore e 0,7 fpk",r.Rho1000Percent,"%","Certificato o classe 1/2/3: 8 / 2,5 / 4%",En));
        parameters.Add(C.V("ΔLjack","Allungamento elastico integrato durante il tiro",r.JackingElongationMm,"mm","∫ σattrito(s)/Ep ds",En));parameters.Add(C.V("ΔLanc","Recupero totale agli ancoraggi attivi",r.AnchorageRecoveryMm,"mm","∫ [σattrito−σanc]/Ep ds; somma dei rientri attivi","Compatibilità elastica e attrito invertito"));
        parameters.Add(C.V("Δσmesh","Differenza massima fra griglie n e 2n",r.MaxDiscretizationMpa,"MPa","max |σeff(2n)−σeff(n)|","Tolleranza 0,05 MPa + 0,01% σjack"));
        foreach(var stage in r.Stages)
        {
            var c=C.Case(r.Summary,stage.Name,$"Età {stage.AgeDays:G8} giorni. Il profilo completo distingue ogni perdita e conserva lo stato efficace per ciascuna ascissa.");
            void Add(string symbol,string description,double value,string unit,string formula)=>c.Values.Add(C.V(symbol,description,value,unit,formula,En));
            Add("t","Età del calcestruzzo",stage.AgeDays,"giorni","assegnata");Add("φ","Coefficiente viscosità dal trasferimento",stage.CreepCoefficient,"−","EN Appendice B");Add("εcs","Ritiro dopo trasferimento",stage.ShrinkageSinceTransfer,"−","εcs(t)−εcs(t0)");
            Add("σpm0,min","Tensione iniziale minima",stage.Points.Min(p=>p.InitialStressMpa),"MPa","min lungo il cavo");Add("σpm0,max","Tensione iniziale massima",stage.Points.Max(p=>p.InitialStressMpa),"MPa","max lungo il cavo");
            Add("σeff,min","Tensione efficace minima",stage.Points.Min(p=>p.EffectiveStressMpa),"MPa","σpm0−Δσc+s+r");Add("σeff,max","Tensione efficace massima",stage.Points.Max(p=>p.EffectiveStressMpa),"MPa","σpm0−Δσc+s+r");
            Add("Peff,min","Forza efficace minima",stage.Points.Min(p=>p.EffectiveForceKn),"kN","Ap σeff");Add("Peff,max","Forza efficace massima",stage.Points.Max(p=>p.EffectiveForceKn),"kN","Ap σeff");
            Add("Δσd,max","Perdita differita massima",stage.Points.Max(p=>p.DeferredLossMpa),"MPa","[Ep |εcs| + 0,8 Δσpr + (Ep/Ecm) φ σc,QP] / [1+(Ep/Ecm)(Ap/Ac)(1+Ac z²/Ic)(1+0,8φ)]");
        }
        r.Summary.Notes.Add("Fonte del sistema: "+i.SystemSource+". Fonte delle tensioni del calcestruzzo: "+i.ConcreteStressSource);
        r.Summary.Notes.Add("Geometria: x da sinistra a destra, z positivo verso l’alto dal baricentro. θ è la deviazione angolare cumulata, senza segno; s è l’ascissa curvilinea. Attrito: σ=σjack exp[−μ(θ+k s)], con k in rad/m all’interno del fattore μ. Banco preteso rettilineo: nessuna perdita per attrito distribuito.");
        r.Summary.Notes.Add("Rientro: profilo di attrito invertito σanc=min(σattrito,σa exp[q]); σa risolve la compatibilità integrale con il rientro assegnato. Gestiti anche propagazione all’intero cavo e attrito nullo. Tiro bilaterale solo simultaneo, simmetrico, con tiri e rientri uguali: ogni metà è risolta per simmetria. Tiri sequenziali dai due capi, rientri disuguali e riprese di tiro esclusi.");
        r.Summary.Notes.Add("Accorciamento immediato: Δσp=(Ep/Ecm(t0)) j Δσc. Preteso j=1; posteso, media di n cavi identici tesati in successione: j=(n−1)/(2n). Δσc deve provenire dallo stato strutturale corretto: il foglio non ricava le tensioni del cls dalla sola forza nel cavo.");
        r.Summary.Notes.Add("Rilassamento a 20 °C: EN (3.28)–(3.30), tempo convenzionale finale 500000 h. Il preteso comprende il rilassamento fra tiro e trasferimento; dopo l’accorciamento elastico si prosegue con il tempo equivalente dell’Appendice D, conservando la perdita già consumata. La tensione di riferimento comprende l’incremento assegnato da azioni quasi permanenti; se nullo, si assume σp,QP=σpm0. Questo incremento da carichi non entra nella predeformazione esportata. Le perdite differite usano l’interazione semplificata (5.46), con 0,8 applicato una volta.");
        r.Summary.Notes.Add(i.Method==PrestressingMethod.PostTensionedUnbonded?"Posteso interno non aderente: rilassamento e z² sono mediati sull’intera lunghezza curvilinea, per compatibilità della perdita uniforme; Ac, Ic, Ep e σc,QP sono costanti. Non è implementato il modello a tratti dei cavi esterni con deviatori.":"Cavi aderenti: perdite differite locali; il profilo conserva la variazione lungo il cavo. Per gruppi con profili o età diversi occorre un modello per fasi dedicato.");
        if(i.Standard==ClimateStandard.Ntc2018)r.Summary.Notes.Add("Scelta conservativa NTC: il limite iniziale §4.1.8.1.5 è applicato anche alla tensione al martinetto; sovratensioni non utilizzate. Ritiro NTC, maturazione/autogeno/viscosità e modelli di perdita EN esplicitamente richiamati. Non sono applicati automaticamente coefficienti di appendici nazionali EN.");
        else r.Summary.Notes.Add("Parametri nazionali/progettuali EN: "+i.NationalParameters+". Fattori di tiro e iniziali assegnati, preimpostati ai valori raccomandati di prima generazione; sovratensione esclusa.");
        r.Summary.Notes.Add("N sul cls è negativo in compressione. Il momento primario è P cos(α) z, positivo con compressione superiore. Sono sole azioni primarie: effetti iperstatici, perdite per fasi non contemplate, fatica, trasferimento per aderenza, lunghezze di trasmissione, bursting/spalling e ancoraggi non sono verificati da questo foglio. Il limite 0,45 fck(t0) riguarda la linearità della viscosità, non sostituisce le verifiche tensionali complete.");
        if(!r.WithinStressLimits)r.Summary.Notes.Insert(0,"ATTENZIONE: superato un limite di tensione iniziale/tiro o il campo lineare della viscosità; stato efficace non utilizzabile senza revisione del progetto.");
        if(!r.Converged)r.Summary.Notes.Insert(0,"ATTENZIONE: discretizzazione del cavo insufficiente; aumentare gli intervalli prima di usare lo stato efficace.");
    }
}
