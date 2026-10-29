using GPC.Model.Actions.Climate;
using static GPC.Design.Actions.Climate.ClimateMath;

namespace GPC.Design.Actions.Climate;

public static class BuildingWindActions
{
    private sealed class Zone
    {
        internal string Name; internal double C10, C1, Height;
        internal Zone(string name,double c10,double c1,double height) { Name=name; C10=c10; C1=c1; Height=height; }
    }
    private static readonly double[] Angles={5,15,30,45,60,75};
    // Each pair is cpe,10 / cpe,1. Entries are transcribed from the amended EN tables,
    // including the same-sign branches required by notes to tables 7.3a and 7.4a.
    private static readonly double[][] UpNegative={new[]{-1.7,-2.5,-1.2,-2.0,-.6,-1.2},new[]{-.9,-2.0,-.8,-1.5,-.3,-.3},new[]{-.5,-1.5,-.5,-1.5,-.2,-.2},new[]{0d,0,0,0,0,0},new[]{.7,.7,.7,.7,.7,.7},new[]{.8,.8,.8,.8,.8,.8}};
    private static readonly double[][] UpPositive={new[]{0d,0,0,0,0,0},new[]{.2,.2,.2,.2,.2,.2},new[]{.7,.7,.7,.7,.4,.4},new[]{.7,.7,.7,.7,.6,.6},new[]{.7,.7,.7,.7,.7,.7},new[]{.8,.8,.8,.8,.8,.8}};
    private static readonly double[][] MonoReverse={new[]{-2.3,-2.5,-1.3,-2.0,-.8,-1.2},new[]{-2.5,-2.8,-1.3,-2.0,-.9,-1.2},new[]{-1.1,-2.3,-.8,-1.5,-.8,-.8},new[]{-.6,-1.3,-.5,-.5,-.7,-.7},new[]{-.5,-1.0,-.5,-.5,-.5,-.5},new[]{-.5,-1.0,-.5,-.5,-.5,-.5}};
    private static readonly double[][] MonoParallel={new[]{-2.1,-2.6,-2.1,-2.4,-1.8,-2.0,-.6,-1.2,-.5,-.5},new[]{-2.4,-2.9,-1.6,-2.4,-1.9,-2.5,-.8,-1.2,-.7,-1.2},new[]{-2.1,-2.9,-1.3,-2.0,-1.5,-2.0,-1.0,-1.3,-.8,-1.2},new[]{-1.5,-2.4,-1.3,-2.0,-1.4,-2.0,-1.0,-1.3,-.9,-1.2},new[]{-1.2,-2.0,-1.2,-2.0,-1.2,-2.0,-1.0,-1.3,-.7,-1.2},new[]{-1.2,-2.0,-1.2,-2.0,-1.2,-2.0,-1.0,-1.3,-.5,-.5}};
    private static readonly double[][] DuoDownNegative={new[]{-.6,-.6,-.6,-.6},new[]{-.4,-.4,-1.0,-1.5},new[]{-.4,-.4,-.5,-.5},new[]{-.2,-.2,-.3,-.3},new[]{-.2,-.2,-.3,-.3},new[]{-.2,-.2,-.3,-.3}};
    private static readonly double[][] DuoDownPositive={new[]{-.6,-.6,.2,.2},new[]{0d,0,0,0},new[]{0d,0,0,0},new[]{0d,0,0,0},new[]{-.2,-.2,-.3,-.3},new[]{-.2,-.2,-.3,-.3}};
    private static readonly double[][] DuoParallel={new[]{-1.6,-2.2,-1.3,-2.0,-.7,-1.2,-.6,-.6},new[]{-1.3,-2.0,-1.3,-2.0,-.6,-1.2,-.5,-.5},new[]{-1.1,-1.5,-1.4,-2.0,-.8,-1.2,-.5,-.5},new[]{-1.1,-1.5,-1.4,-2.0,-.9,-1.2,-.5,-.5},new[]{-1.1,-1.5,-1.2,-2.0,-.8,-1.0,-.5,-.5},new[]{-1.1,-1.5,-1.2,-2.0,-.8,-1.0,-.5,-.5}};

    public static double AreaCoefficient(double c10,double c1,double areaM2)
    { Positive(areaM2,"Area caricata [m²]"); Range(c10,-100,100,"cpe,10"); Range(c1,-100,100,"cpe,1"); return areaM2<=1?c1:areaM2>=10?c10:c1-(c1-c10)*Math.Log10(areaM2); }
    private static double[] At(double angle,double[][] table)
    {
        int i=0; while(i<Angles.Length-2 && angle>Angles[i+1])i++;
        return table[i].Select((v,j)=>Lerp(angle,Angles[i],Angles[i+1],v,table[i+1][j])).ToArray();
    }
    public static ClimateResult Calculate(BuildingWindInput x)
    {
        if(x is null)throw new ArgumentNullException(nameof(x));
        EnumValue(x.Roof); EnumValue(x.InternalPressure);
        Positive(x.WidthM,"b [m]"); Positive(x.DepthM,"d [m]"); Positive(x.HeightM,"h [m]"); Positive(x.LoadedAreaM2,"Area caricata [m²]"); Positive(x.StructuralFactor,"cd / cs·cd");
        Range(x.HeightM/x.DepthM,0,5,"Snellezza h/d del modello tabellato"); Range(x.ParapetM,0,double.MaxValue,"Parapetto [m]");
        Require(x.WindDirectionDeg==0 || x.WindDirectionDeg==90 || (x.WindDirectionDeg==180 && x.Roof==WindRoof.Monopitch),"Direzione vento: 0° o 90°; 180° solo monofalda.");
        Require(x.Roof==WindRoof.Flat || x.ParapetM==0,"Parapetti sulle coperture inclinate: richiedono coefficienti specifici.");
        if(x.Roof!=WindRoof.Flat) Range(x.PitchDeg,5,75,"Inclinazione per le tabelle delle falde [°]");
        bool ntc=x.Site.Standard==ClimateStandard.Ntc2018;
        bool automaticUnit=ntc?x.HeightM<=80:x.HeightM<15;
        Require(x.StructuralFactorAssessed || (automaticUnit && x.StructuralFactor==1),"Confermare la valutazione del coefficiente dinamico/strutturale per questa costruzione.");
        var r=WindClimate.Calculate(x.Site,x.HeightM+x.ParapetM);
        r.Scope="Edificio rettangolare isolato, copertura piana a spigoli vivi/con parapetto o a falde; pressioni locali in zone e casi simultanei.";
        string reference=ntc?"Circolare §C3.3.8.1; EN 1991-1-4 §§7.2.1–7.2.5 (coefficienti locali richiamati)":"EN 1991-1-4 §§7.2.1–7.2.5";
        double hd=x.HeightM/x.DepthM, e=Math.Min(x.WidthM,2*x.HeightM);
        double d10=hd<=.25?.7:hd<=1?Lerp(hd,.25,1,.7,.8):.8;
        double e10=hd<=.25?-.3:hd<=1?Lerp(hd,.25,1,-.3,-.5):Lerp(hd,1,5,-.5,-.7);
        var walls=new List<Zone>{new Zone("Parete A",-1.2,-1.4,x.HeightM)};
        if(x.DepthM>e/5)walls.Add(new Zone("Parete B",-.8,-1.1,x.HeightM));
        if(x.DepthM>e)walls.Add(new Zone("Parete C",-.5,-.5,x.HeightM));
        if(x.HeightM<=x.WidthM)walls.Add(new Zone("Parete D sopravento",d10,1,x.HeightM));
        else { walls.Add(new Zone("Parete D · 0≤z≤b",d10,1,x.WidthM)); walls.Add(new Zone("Parete D · b<z≤h",d10,1,x.HeightM)); }
        walls.Add(new Zone("Parete E sottovento",e10,e10,x.HeightM));
        var roofCases=new List<(string name,List<Zone> zones)>();
        List<Zone> Zones(string[] names,double[] cs)=>names.Select((n,i)=>new Zone("Copertura "+n,cs[2*i],cs[2*i+1],x.HeightM+x.ParapetM)).ToList();
        if(x.Roof==WindRoof.Flat)
        {
            double hp=x.ParapetM/x.HeightM;
            double[] hs={0,.025,.05,.10}; double[][] values={new[]{-1.8,-2.5,-1.2,-2.0},new[]{-1.6,-2.2,-1.1,-1.8},new[]{-1.4,-2.0,-.9,-1.6},new[]{-1.2,-1.8,-.8,-1.4}};
            Require(hp<=.1,"Parapetto hp/h >0,10: coefficienti da studio specifico.");
            int j=0;while(j<2 && hp>hs[j+1])j++;
            var coeffs=values[j].Select((v,k)=>Lerp(hp,hs[j],hs[j+1],v,values[j+1][k])).Concat(new[]{-.7,-1.2}).ToArray();
            foreach(double ci in new[]{-.2,.2}) roofCases.Add(("Piana · I="+ci.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture),Zones(new[]{"F","G","H","I"},coeffs.Concat(new[]{ci,ci}).ToArray())));
        }
        else if(x.WindDirectionDeg==90)
        {
            bool mono=x.Roof==WindRoof.Monopitch;
            roofCases.Add(("Vento parallelo al colmo",Zones(mono?new[]{"Fup","Flow","G","H","I"}:new[]{"F","G","H","I"},At(x.PitchDeg,mono?MonoParallel:DuoParallel))));
        }
        else if(x.WindDirectionDeg==180) roofCases.Add(("Monofalda · vento dal bordo alto",Zones(new[]{"F","G","H"},At(x.PitchDeg,MonoReverse))));
        else
        {
            foreach(bool up in x.PitchDeg>45?new[]{true}:new[]{false,true})
            {
                var front=At(x.PitchDeg,up?UpPositive:UpNegative);
                if(x.Roof==WindRoof.Monopitch)roofCases.Add(("Monofalda · ramo "+(up?"positivo":"negativo"),Zones(new[]{"F","G","H"},front)));
                else foreach(bool down in x.PitchDeg>45?new[]{false}:new[]{false,true})
                    roofCases.Add(($"Due falde · FGH {(up?"+":"−")} / IJ {(down?"+":"−")}",Zones(new[]{"F","G","H","I","J"},front.Concat(At(x.PitchDeg,down?DuoDownPositive:DuoDownNegative)).ToArray())));
            }
        }
        double[] cpis;
        double zi;
        if(x.InternalPressure==InternalPressure.DistributedOpenings){cpis=new[]{-.3,.2};zi=x.HeightM+x.ParapetM;}
        else
        {
            zi=Range(x.InternalReferenceHeightM,double.Epsilon,x.HeightM+x.ParapetM,"Quota di riferimento interna [m]");
            if(x.InternalPressure==InternalPressure.DominantFace)
            {
                Range(x.DominantOpeningRatio,2,double.MaxValue,"Rapporto aperture dominanti"); Range(x.DominantExternalCoefficient,-100,100,"cpe apertura dominante");
                cpis=new[]{(.75+.15*(Math.Min(x.DominantOpeningRatio,3)-2))*x.DominantExternalCoefficient};
            }
            else {Range(x.InternalCoefficientMin,-100,100,"cpi min");Range(x.InternalCoefficientMax,x.InternalCoefficientMin,100,"cpi max"); cpis=new[]{x.InternalCoefficientMin,x.InternalCoefficientMax}.Distinct().ToArray();}
        }
        double qpi=WindClimate.Peak(x.Site,zi);
        foreach(var roof in roofCases) foreach(double cpi in cpis)
        {
            var c=Case(r,roof.name+" · cpi="+cpi.ToString("G4",System.Globalization.CultureInfo.InvariantCulture),"Coefficienti dello stesso ramo su ogni falda; pressione positiva verso la superficie, negativa di risucchio. Casi alternativi.");
            foreach(var z in walls.Concat(roof.zones))
            {
                double cpe=AreaCoefficient(z.C10,z.C1,x.LoadedAreaM2), qpe=WindClimate.Peak(x.Site,z.Height), pe=qpe*cpe, pi=qpi*cpi;
                c.Values.Add(V(z.Name+" · cpe","Coefficiente esterno",cpe,"–","cpe,1 − (cpe,1−cpe,10) log10(A), limiti 1–10 m²",reference));
                c.Values.Add(V(z.Name+" · p","Pressione netta",(pe-pi)*(ntc?x.StructuralFactor:1),"kN/m²",ntc?"cd · [qp(ze)cpe − qp(zi)cpi]":"qp(ze)cpe − qp(zi)cpi",reference));
                if(!ntc && x.StructuralFactor!=1)c.Values.Add(V(z.Name+" · we,eq","Contributo esterno per forza equivalente",pe*x.StructuralFactor,"kN/m²","cs·cd · qp(ze)cpe; contributo interno separato", "EN §5.3(3)"));
            }
        }
        r.Parameters.Add(V("e","Dimensione zone",e,"m","min(b;2h)",reference));
        r.Parameters.Add(V("e/5","Limite zona laterale A",e/5,"m","e/5",reference));
        r.Parameters.Add(V("e/10","Profondità fascia copertura sopravento",e/10,"m","e/10",reference));
        r.Parameters.Add(V("zi","Quota interna",zi,"m","superfici che alimentano le aperture", "EN §7.2.9 / Circolare C3.3.8.5"));
        r.Parameters.Add(V("qpi","Pressione cinetica per aperture",qpi,"kN/m²","qp(zi)",reference));
        r.Parameters.Add(V("A","Area dell’elemento caricato",x.LoadedAreaM2,"m²","area responsabile dell’effetto considerato",reference));
        Range(x.FrictionCoefficient,0,1,"cf attrito");
        r.Parameters.Add(V("pf","Pressione tangenziale",WindClimate.Peak(x.Site,x.HeightM)*x.FrictionCoefficient,"kN/m²","qp(h) · cf","NTC §3.3.5 / EN §7.5"));
        r.Parameters.Add(V("xfr","Inizio superficie di attrito dal fronte",Math.Min(2*x.WidthM,4*x.HeightM),"m","min(2b;4h)","EN §7.5 / CNR G.12"));
        r.Notes.Add("Le dimensioni b (ortogonale al vento) e d (parallela) si riferiscono alla direzione selezionata. Ricalcolare scambiando b/d per il vento ortogonale. Zone da EN figure 7.5–7.8; assenza di riduzione per mancata correlazione D/E.");
        r.Notes.Add("A è l’area caricata dell’elemento verificato, non necessariamente l’area della zona. Le pressioni locali non sono automaticamente una risultante globale; applicare le zone alla geometria reale.");
        r.Notes.Add("Esclusi tettoie aperte (aperture >30% su almeno due facce), falde concave, bordi curvi/mansardati, interferenza fra edifici e risposta aeroelastica. I parapetti stessi richiedono la verifica come pareti libere.");
        return r;
    }
}
