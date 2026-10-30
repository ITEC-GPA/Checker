using GPC.Model.Actions.Climate;
using GPC.Model.Actions.General;
using GPC.Model.Design.Combinations;
using static GPC.Design.Actions.Climate.ClimateMath;

namespace GPC.Design.Actions.General;

/// <summary>Building action combinations. Enumerates presence/leading alternatives and common-source permanent factors.
/// Optional signed effects are for one response of a LINEAR model; the coefficient matrix remains the primary result.</summary>
public static class ActionCombinations
{
    public static ClimateResult Calculate(ActionCombinationsInput x,CancellationToken cancellation=default)
    {
        if(x is null)throw new ArgumentNullException(nameof(x));National(x.Standard,x.NationalParameters);EnumValue(x.Family);EnumValue(x.Set);EnumValue(x.EnExpression);
        bool ntc=x.Standard==ClimateStandard.Ntc2018;
        if(!ntc){Require(x.RecommendedEnFactorsConfirmed,"Confermare i coefficienti EN raccomandati nella fonte nazionale dichiarata, oppure usare un profilo esterno specifico.");Range(x.EnXi,0,1,"ξ EN");}
        Require(ntc||x.EnExpression==EnUltimateExpression.Equation610||x.Set==CombinationSet.Structural,"Le espressioni 6.10a/b sono implementate per il Set B STR/GEO, non EQU o Set C.");
        Require(!string.IsNullOrWhiteSpace(x.EffectUnit),"Indicare l’unità comune degli effetti.");
        Require(x.Actions is not null&&x.Actions.Count>0&&x.Actions.Count<=20,"Assegnare da 1 a 20 azioni.");var a=x.Actions!.ToArray();
        foreach(var action in a)
        {if(action is null)throw new ArgumentException("Azione nulla.");Require(!string.IsNullOrWhiteSpace(action.Name),"Nome azione mancante.");EnumValue(action.Nature);EnumValue(action.PsiCategory);Range(action.Effect,-double.MaxValue,double.MaxValue,"Effetto "+action.Name);if(action.Nature==ActionNature.Variable)Psi(action,ntc);else Require(string.IsNullOrWhiteSpace(action.ExclusiveGroup),"Le esclusioni reciproche si applicano alle sole azioni variabili.");}
        Require(a.Select(v=>v.Name).Distinct().Count()==a.Length,"Nomi azioni duplicati.");
        string Key(int i)=>string.IsNullOrWhiteSpace(a[i].Correlation)?"row:"+i:"source:"+a[i].Correlation;
        bool Permanent(int i)=>a[i].Nature is ActionNature.PermanentG1 or ActionNature.PermanentG2;
        var permanent=Enumerable.Range(0,a.Length).Where(Permanent).GroupBy(Key).Select(g=>g.ToArray()).ToArray();
        var variable=Enumerable.Range(0,a.Length).Where(i=>a[i].Nature==ActionNature.Variable).GroupBy(Key).Select(g=>g.ToArray()).ToArray();
        var accidents=Enumerable.Range(0,a.Length).Where(i=>a[i].Nature==ActionNature.Accidental).ToArray();
        Require(permanent.Length<=4&&variable.Length<=6,"Massimo 4 fonti permanenti indipendenti e 6 gruppi variabili; raggruppare le parti della medesima azione con Correlazione.");
        foreach(var source in a.Where(v=>!string.IsNullOrWhiteSpace(v.Correlation)).GroupBy(v=>v.Correlation))
        {Require(source.Select(v=>v.Nature==ActionNature.Variable?1:v.Nature is ActionNature.PermanentG1 or ActionNature.PermanentG2?0:2).Distinct().Count()==1,"Una correlazione non può mescolare permanenti, variabili o eccezionali.");}
        if(x.Family==CombinationFamily.Accidental)Require(accidents.Length>0,"Per la combinazione eccezionale serve almeno un’azione Ad.");
        string reference=ntc?"NTC 2018 §2.5.3, Tab. 2.5.I e 2.6.I (edifici)":"EN 1990:2002+A1:2005 §§6.4.3–6.5.3, Allegato A1 (edifici)";
        var r=new ClimateResult{Standard=ntc?Ntc:"EN 1990:2002+A1:2005",Scope="Combinazioni degli edifici · "+x.Family+" · "+x.Set};
        if(!ntc)r.Notes.Add("Parametri nazionali dichiarati: "+x.NationalParameters+". Coefficienti raccomandati confermati; ξ = "+x.EnXi+".");
        var psi=a.Select(v=>v.Nature==ActionNature.Variable?Psi(v,ntc):new[]{0d,0,0}).ToArray();
        foreach(int i in Enumerable.Range(0,a.Length))if(a[i].Nature==ActionNature.Variable)
        {for(int k=0;k<3;k++)r.Parameters.Add(V(a[i].Name+" ψ"+k,"Coefficiente di rappresentatività",psi[i][k],"−",a[i].PsiCategory==ActionPsiCategory.Assigned?"Assegnato":"Tabella per categoria",reference));}
        var seen=new HashSet<string>();double minimum=double.PositiveInfinity,maximum=double.NegativeInfinity;string minName="",maxName="";
        void Add(double[] f,string name,string formula)
        {
            string fingerprint=string.Join(";",f.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));if(!seen.Add(fingerprint))return;
            Require(r.Cases.Count<20000,"Oltre 20000 combinazioni uniche: ridurre le alternative indipendenti.");
            var c=Case(r,name,formula+". Effetti lineari con segno e unità comune: "+x.EffectUnit+".");double value=0;
            for(int i=0;i<a.Length;i++){c.Values.Add(V(a[i].Name,"Moltiplicatore dell’azione",f[i],"−",formula,reference));value+=f[i]*a[i].Effect;}
            c.Values.Add(V("Ed","Somma degli effetti assegnati",value,x.EffectUnit,"Σ ci Ei",reference));
            if(value<minimum){minimum=value;minName=name;}if(value>maximum){maximum=value;maxName=name;}
        }
        int permanentVariants=x.Family==CombinationFamily.Ultimate?1<<permanent.Length:1;
        // In service/accidental situations, omission of favourable G2 is an explicit project choice.
        var optionalG2=permanent.Where(g=>g.All(i=>a[i].Nature==ActionNature.PermanentG2&&a[i].OmitFavourableG2)).ToArray();
        if(x.Family!=CombinationFamily.Ultimate)permanentVariants=1<<optionalG2.Length;
        for(int mask=0;mask<(1<<variable.Length);mask++)
        {
            cancellation.ThrowIfCancellationRequested();var active=Enumerable.Range(0,variable.Length).Where(g=>(mask&(1<<g))!=0).ToArray();
            var exclusions=new HashSet<string>();bool compatible=true;
            foreach(int g in active)foreach(string ex in variable[g].Select(i=>a[i].ExclusiveGroup).Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct())if(!exclusions.Add(ex))compatible=false;
            if(!compatible)continue;
            int[] leading=x.Family==CombinationFamily.QuasiPermanent||x.Family==CombinationFamily.Accidental&&(ntc||!x.EnAccidentalLeadingPsi1)||active.Length==0?[-1]:active;
            foreach(int lead in leading)for(int gm=0;gm<permanentVariants;gm++)
            {
                int[] forms=!ntc&&x.Family==CombinationFamily.Ultimate&&x.EnExpression==EnUltimateExpression.Equations610a610b?[1,2]:[0];
                foreach(int form in forms)foreach(int accidental in x.Family==CombinationFamily.Accidental?accidents:new[]{-1})
                {
                    var factors=new double[a.Length];
                    foreach(var group in permanent.Select((indices,g)=>(indices,g)))foreach(int i in group.indices)
                    {
                        bool unfavourable=(gm&(1<<group.g))!=0;
                        factors[i]=x.Family==CombinationFamily.Ultimate?PermanentFactor(a[i],x.Set,ntc,unfavourable):1;
                        if(x.Family==CombinationFamily.Ultimate&&a[i].Nature==ActionNature.PermanentG2&&a[i].OmitFavourableG2&&!unfavourable)factors[i]=0;
                        if(form==2&&unfavourable)factors[i]*=x.EnXi;
                    }
                    if(x.Family!=CombinationFamily.Ultimate)for(int g=0;g<optionalG2.Length;g++)if((gm&(1<<g))==0)foreach(int i in optionalG2[g])factors[i]=0;
                    for(int i=0;i<a.Length;i++)if(a[i].Nature==ActionNature.Prestress)factors[i]=1;
                    foreach(int g in active)foreach(int i in variable[g])
                    {
                        double representative=x.Family switch
                        {
                            CombinationFamily.Ultimate=>g==lead&&form!=1?1:psi[i][0],
                            CombinationFamily.Characteristic=>g==lead?1:psi[i][0],
                            CombinationFamily.Frequent=>g==lead?psi[i][1]:psi[i][2],
                            CombinationFamily.Accidental=>!ntc&&x.EnAccidentalLeadingPsi1&&g==lead?psi[i][1]:psi[i][2],
                            _=>psi[i][2]
                        };
                        factors[i]=representative*(x.Family==CombinationFamily.Ultimate?(x.Set==CombinationSet.Geotechnical?1.3:1.5):1);
                    }
                    if(accidental>=0)factors[accidental]=1;
                    string formula=x.Family switch{CombinationFamily.Ultimate=>form==1?"EN 6.10a":form==2?"EN 6.10b":ntc?"NTC 2.5.1":"EN 6.10",CombinationFamily.Characteristic=>ntc?"NTC 2.5.2":"EN 6.14b",CombinationFamily.Frequent=>ntc?"NTC 2.5.3":"EN 6.15b",CombinationFamily.QuasiPermanent=>ntc?"NTC 2.5.4":"EN 6.16b",_=>ntc?"NTC 2.5.6":"EN 6.11b"};
                    Add(factors,$"{formula} · Q{mask} G{gm}"+(lead>=0?" · dominante "+a[variable[lead][0]].Name:"")+(accidental>=0?" · "+a[accidental].Name:""),formula);
                }
            }
        }
        r.Parameters.Add(V("Emin",minName,minimum,x.EffectUnit,"min(Σ ci Ei)",reference));r.Parameters.Add(V("Emax",maxName,maximum,x.EffectUnit,"max(Σ ci Ei)",reference));r.Parameters.Add(V("N","Combinazioni distinte",r.Cases.Count,"−","Enumerazione e rimozione duplicati",reference));
        r.Notes.Add("Coefficienti per edifici, non per traffico sui ponti. Nessuna azione sismica. Le righe sono combinazioni di azioni: per analisi non lineari applicare i coefficienti ai carichi e ricalcolare il modello, senza sommare le sollecitazioni.");
        r.Notes.Add("Le parti della stessa sorgente permanente devono condividere Correlazione: γ favorevole/sfavorevole varia insieme. Per variabili correlate, presenza e azione dominante variano insieme. Esclusione identifica alternative non simultanee; il campo vuoto non impone vincoli.");
        r.Notes.Add("Tutte le presenze/assenze delle azioni variabili sono enumerate; i permanenti restano presenti salvo omissione G2 esplicitamente selezionata. Classificare coerentemente condizioni favorevoli/sfavorevoli con l’effetto verificato; gli estremi riguardano soltanto gli effetti scalari assegnati.");
        r.Notes.Add("Precompressione trattata con γP=1: casi specifici di materiale/instabilità richiedono combinazioni dedicate. EQU/STR/GEO qui indica solo il set dei fattori sulle azioni, non una verifica geotecnica completa.");return r;
    }
    private static double PermanentFactor(CombinationAction a,CombinationSet set,bool ntc,bool unfavourable)
    {
        if(!ntc)return set switch{CombinationSet.Equilibrium=>unfavourable?1.1:.9,CombinationSet.Geotechnical=>1,_=>unfavourable?1.35:1};
        bool g2=a.Nature==ActionNature.PermanentG2&&!a.DefinedPermanentG2;
        return set switch{CombinationSet.Equilibrium=>g2?(unfavourable?1.5:.8):(unfavourable?1.1:.9),CombinationSet.Geotechnical=>g2?(unfavourable?1.3:.8):1,_=>g2?(unfavourable?1.5:.8):(unfavourable?1.3:1)};
    }
    private static double[] Psi(CombinationAction a,bool ntc)
    {
        if(a.PsiCategory==ActionPsiCategory.Assigned){foreach(double p in new[]{a.Psi0,a.Psi1,a.Psi2})Range(p,0,1,"ψ "+a.Name);return [a.Psi0,a.Psi1,a.Psi2];}
        var category=a.PsiCategory switch{ActionPsiCategory.Residential=>VariableActionCategory.A,ActionPsiCategory.Office=>VariableActionCategory.B,ActionPsiCategory.Gathering=>VariableActionCategory.C,ActionPsiCategory.Retail=>VariableActionCategory.D,ActionPsiCategory.Storage=>VariableActionCategory.E,ActionPsiCategory.VehiclesF=>VariableActionCategory.F,ActionPsiCategory.VehiclesG=>VariableActionCategory.G,ActionPsiCategory.RoofH=>VariableActionCategory.H,ActionPsiCategory.Wind=>VariableActionCategory.Wind,ActionPsiCategory.SnowLow=>VariableActionCategory.SnowUpTo1000m,ActionPsiCategory.SnowHigh=>VariableActionCategory.SnowAbove1000m,_=>VariableActionCategory.Temperature};
        var profile=ntc?CombinationProfile.Ntc2018:CombinationProfile.En1990;return [profile.Psi0(category),profile.Psi1(category),profile.Psi2(category)];
    }
}
