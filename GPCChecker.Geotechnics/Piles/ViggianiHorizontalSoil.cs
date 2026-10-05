namespace GPC.Checkers.Geotechnics.Piles;

public enum HorizontalSoilMode { ManualKh, ManualNh, SandTable145, CohesiveTable146, SandCorrelation, ManualDistributed }
public sealed class SandReactionRow
{
    public string Density { get; }
    public double MinimumA { get; }
    public double MaximumA { get; }
    public double RecommendedA { get; }
    public double DryNhNPerCm3 { get; }
    public double SubmergedNhNPerCm3 { get; }
    internal SandReactionRow(string density,double min,double max,double recommended,double dry,double submerged)
    { Density=density;MinimumA=min;MaximumA=max;RecommendedA=recommended;DryNhNPerCm3=dry;SubmergedNhNPerCm3=submerged; }
}
public sealed class CohesiveReactionRow
{
    public string Category => Soil;
    public string Id { get; }
    public string Soil { get; }
    public string Author { get; }
    public double MinimumNPerCm3 { get; }
    public double MaximumNPerCm3 { get; }
    public string Label => Soil+" — "+Author;
    internal CohesiveReactionRow(string id,string soil,string author,double min,double max)
    {Id=id;Soil=soil;Author=author;MinimumNPerCm3=min;MaximumNPerCm3=max;}
}
/// <summary>Inputs in kN and m; table 14.6 choice is explicitly in N/cm³. No soil-name lookup.</summary>
public sealed class HorizontalSoilAssignment
{
    public string Name { get; set; } = "";
    public double Thickness { get; set; }
    public HorizontalSoilMode Mode { get; set; }
    public double? ManualValue { get; set; }
    public string Density { get; set; } = "";
    public string CohesiveRowId { get; set; } = "";
    public double? SelectedNhNPerCm3 { get; set; }
    public double? A { get; set; }
    public double? UnitWeight { get; set; }
    public double? SaturatedUnitWeight { get; set; }
    public double? OverrideNh { get; set; }
    public string OverrideReason { get; set; } = "";
    public string Conditions { get; set; } = "";
    public string SelectionOrigin { get; set; } = "";
}
public sealed class HorizontalSoilDetermination
{
    public int Layer { get; set; }
    public string Name { get; set; } = "";
    public double Top { get; set; }
    public double Bottom { get; set; }
    public string Method { get; set; } = "";
    public string Law { get; set; } = "";
    public double BaseValue { get; set; }
    public double AdoptedValue { get; set; }
    public string Unit { get; set; } = "";
    public double? Nh { get; set; }
    public double? TableNhNPerCm3 { get; set; }
    public double? TableMinimumNPerCm3 { get; set; }
    public double? TableMaximumNPerCm3 { get; set; }
    public double? A { get; set; }
    public double? InitialMean { get; set; }
    public string InitialMeanUnit { get; set; } = "";
    public string SelectionOrigin { get; set; } = "";
    public double? MinimumA { get; set; }
    public double? MaximumA { get; set; }
    public double? RecommendedA { get; set; }
    public double? AdoptedUnitWeight { get; set; }
    public bool Submerged { get; set; }
    public bool Overridden { get; set; }
    public string OverrideReason { get; set; } = "";
    public string Source { get; set; } = "";
    public string Applicability { get; set; } = "";
    public string Conditions { get; set; } = "";
}
public sealed class HorizontalSoilProfile
{
    public List<ElasticPileLayer> Layers { get; } = new List<ElasticPileLayer>();
    public List<HorizontalSoilDetermination> Determinations { get; } = new List<HorizontalSoilDetermination>();
}
/// <summary>Transcription of the supplied scan: PDF 237–238 and 244–245, printed 464–467 and 478–481.
/// Edition not identifiable: the scan starts at the end of a preface dated December 1998, without title/copyright page.
/// Original cited papers were not consulted. SI interface here uses kN,m; the generic ElasticPile engine is unit-consistent.</summary>
public static class ViggianiHorizontalSoil
{
    public const string Source = "C. Viggiani, Fondazioni, §14.4.1; PDF fornito pp. 237–238 e 244–245, libro pp. 464–467 e 478–481. Edizione non identificabile nella scansione; prefazione datata dicembre 1998. Articoli originali citati non consultati.";
    public const string StratificationConvention = "Estensione numerica dichiarata: parametri locali a tratti, z globale dal piano campagna; salto di nh ammesso a strati/falda. Il libro non prescrive questo raccordo per i profili stratificati.";
    public static IReadOnlyList<SandReactionRow> SandTable { get; } = Array.AsReadOnly(new[]{
        new SandReactionRow("Sciolto",100,300,200,2.5,1.5),
        new SandReactionRow("Medio",300,1000,600,7.5,5),
        new SandReactionRow("Denso",1000,3000,1500,20,12)});
    public static IReadOnlyList<CohesiveReactionRow> CohesiveTable { get; } = Array.AsReadOnly(new[]{
        new CohesiveReactionRow("clay-reese-1956","Argilla n.c. o lievemente o.c.","Reese, Matlock, 1956",.2,3.5),
        new CohesiveReactionRow("clay-davisson-1963","Argilla n.c. o lievemente o.c.","Davisson, Prakash, 1963",.3,.5),
        new CohesiveReactionRow("organic-peck-1970","Argilla organica n.c.","Peck, Davisson, 1970",.1,1),
        new CohesiveReactionRow("organic-davisson-1970","Argilla organica n.c.","Davisson, 1970",.1,.8),
        new CohesiveReactionRow("peat-davisson-1970","Torba","Davisson, 1970",.05,.05),
        new CohesiveReactionRow("peat-wilson-1967","Torba","Wilson, Hilts, 1967",.03,.1),
        new CohesiveReactionRow("loess-bowles-1968","Loess","Bowles, 1968",8,10)});
    static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    static double Positive(double? value,string label,bool zero=false)
    {if(!value.HasValue||!Finite(value.Value)||(zero?value.Value<0:value.Value<=0))throw new ArgumentException(label+": valore finito "+(zero?"non negativo":"positivo")+" richiesto.");return value.Value;}
    public static double ToKnPerM3(double nPerCm3)=>Positive(nPerCm3,"nh [N/cm³]",true)*1000;
    public static double FromA(double a,double unitWeight)=>Positive(a,"A")*Positive(unitWeight,"γ o γ′ [kN/m³]")/1.35;
    public static double MeanA(string density){var r=SandTable.FirstOrDefault(x=>x.Density==density)??throw new ArgumentException("Selezionare addensamento.");return (r.MinimumA+r.MaximumA)/2;}
    public static double MeanNh(string id){var r=CohesiveTable.FirstOrDefault(x=>x.Id==id||x.Label==id)??throw new ArgumentException("Selezionare riga 14.6.");return (r.MinimumNPerCm3+r.MaximumNPerCm3)/2;}
    public static HorizontalSoilProfile Resolve(IReadOnlyList<HorizontalSoilAssignment> assignments,double? waterDepth=null,double waterUnitWeight=9.81)
    {
        if(assignments==null||assignments.Count==0)throw new ArgumentException("Assegnare almeno uno strato.");
        if(waterDepth.HasValue)Positive(waterDepth,"Profondità falda [m]",true);
        Positive(waterUnitWeight,"γw [kN/m³]");
        var profile=new HorizontalSoilProfile();double top=0;
        for(int i=0;i<assignments.Count;i++)
        {
            var a=assignments[i]??throw new ArgumentException("Strato nullo.");double bottom=top+Positive(a.Thickness,"Spessore");
            if(!Finite(bottom)||!Enum.IsDefined(typeof(HorizontalSoilMode),a.Mode))throw new ArgumentException("Spessore o modalità non validi.");
            if(string.IsNullOrWhiteSpace(a.Conditions))throw new ArgumentException("Strato "+(i+1)+": dichiarare fonte del parametro e condizioni di impiego.");
            bool assisted=a.Mode==HorizontalSoilMode.SandTable145||a.Mode==HorizontalSoilMode.CohesiveTable146||a.Mode==HorizontalSoilMode.SandCorrelation;
            if(a.OverrideNh.HasValue&&(!assisted||string.IsNullOrWhiteSpace(a.OverrideReason)))throw new ArgumentException("Override nh: ammesso sulle modalità assistite e con motivazione esplicita.");
            var cuts=new List<double>{top};
            if(waterDepth.HasValue&&waterDepth>top&&waterDepth<bottom&&(a.Mode==HorizontalSoilMode.SandTable145||a.Mode==HorizontalSoilMode.SandCorrelation))cuts.Add(waterDepth.Value);
            cuts.Add(bottom);
            for(int j=1;j<cuts.Count;j++)
            {
                double start=cuts[j-1],end=cuts[j];bool wet=waterDepth.HasValue&&(start+end)/2>=waterDepth.Value;
                var d=new HorizontalSoilDetermination {Layer=i+1,Name=a.Name,Top=start,Bottom=end,Method=a.Mode.ToString(),Source=Source,Conditions=a.Conditions,Submerged=wet,OverrideReason=a.OverrideReason};
                ElasticSoilLaw law=ElasticSoilLaw.LinearKhReference;double value;
                if(a.Mode==HorizontalSoilMode.ManualKh||a.Mode==HorizontalSoilMode.ManualNh||a.Mode==HorizontalSoilMode.ManualDistributed)
                {
                    value=Positive(a.ManualValue,"Modulo assegnato",true);
                    law=a.Mode==HorizontalSoilMode.ManualKh?ElasticSoilLaw.ConstantKh:a.Mode==HorizontalSoilMode.ManualDistributed?ElasticSoilLaw.ConstantDistributed:ElasticSoilLaw.LinearKhReference;
                    d.Source="Parametro manuale: "+a.Conditions+". Definizioni e legge: "+Source;
                    d.Applicability=a.Mode==HorizontalSoilMode.ManualKh?"kh costante: schematizzazione per argille sovraconsolidate; valore e applicabilità da giustificare.":a.Mode==HorizontalSoilMode.ManualNh?"Legge Reese–Matlock: incoerenti e argille n.c./debolmente o.c.; nh assegnato.":"k distribuito assegnato; nessuna seconda moltiplicazione per D.";
                }
                else if(a.Mode==HorizontalSoilMode.CohesiveTable146)
                {
                    var row=CohesiveTable.FirstOrDefault(r=>r.Id==a.CohesiveRowId)??throw new ArgumentException("Selezionare una riga della tabella 14.6, con la sua fonte.");
                    d.InitialMean=MeanNh(row.Id);d.InitialMeanUnit="N/cm³";
                    double selected=Positive(a.SelectedNhNPerCm3??d.InitialMean,"nh [N/cm³]");
                    d.SelectionOrigin=a.SelectedNhNPerCm3.HasValue?(string.IsNullOrWhiteSpace(a.SelectionOrigin)?"Scelta utente":a.SelectionOrigin):"Media iniziale del software";
                    if(selected<row.MinimumNPerCm3||selected>row.MaximumNPerCm3)throw new ArgumentException("nh fuori dall'intervallo della riga 14.6 selezionata. Usare un override motivato per valori esterni.");
                    value=ToKnPerM3(selected);d.TableNhNPerCm3=selected;d.TableMinimumNPerCm3=row.MinimumNPerCm3;d.TableMaximumNPerCm3=row.MaximumNPerCm3;
                    d.Source="Tabella 14.6, libro p. 479 (PDF 244); "+row.Author+". "+Source;d.Applicability=row.Soil+"; intervallo orientativo; media iniziale convenzionale del software, non raccomandazione bibliografica.";
                }
                else
                {
                    var row=SandTable.FirstOrDefault(r=>r.Density==a.Density)??throw new ArgumentException("Selezionare lo stato di addensamento della tabella 14.5.");
                    d.MinimumA=row.MinimumA;d.MaximumA=row.MaximumA;d.RecommendedA=row.RecommendedA;
                    d.Source="Tabella 14.5"+(a.Mode==HorizontalSoilMode.SandCorrelation?" ed eq. 14.25":"")+", libro p. 478 (PDF 244). "+Source;
                    d.Applicability="Terreni incoerenti, "+row.Density.ToLowerInvariant()+"; "+(wet?"sotto falda":"sopra falda / non immersi")+". Valori orientativi.";
                    if(a.Mode==HorizontalSoilMode.SandTable145){d.TableNhNPerCm3=wet?row.SubmergedNhNPerCm3:row.DryNhNPerCm3;value=ToKnPerM3(d.TableNhNPerCm3.Value);}
                    else
                    {
                        d.InitialMean=MeanA(row.Density);d.InitialMeanUnit="adimensionale";
                        double selectedA=Positive(a.A??d.InitialMean,"A");
                        d.SelectionOrigin=a.A.HasValue?(string.IsNullOrWhiteSpace(a.SelectionOrigin)?"Scelta utente":a.SelectionOrigin):"Media iniziale del software";
                        if(selectedA<row.MinimumA||selectedA>row.MaximumA)throw new ArgumentException("A fuori campo della tabella 14.5 per l'addensamento selezionato.");
                        double gamma=wet?Positive(a.SaturatedUnitWeight,"γsat")-waterUnitWeight:Positive(a.UnitWeight,"γ");
                        value=FromA(selectedA,gamma);d.A=selectedA;d.AdoptedUnitWeight=gamma;
                    }
                }
                d.BaseValue=value;d.AdoptedValue=a.OverrideNh.HasValue?Positive(a.OverrideNh,"Override nh",true):value;d.Overridden=a.OverrideNh.HasValue;
                d.Law=law.ToString();d.Unit=law==ElasticSoilLaw.ConstantDistributed?"kN/m²":"kN/m³";d.Nh=law==ElasticSoilLaw.LinearKhReference?d.AdoptedValue:(double?)null;
                profile.Determinations.Add(d);profile.Layers.Add(new ElasticPileLayer{Name=a.Name,Thickness=end-start,Law=law,Value=d.AdoptedValue,Source=d.Source,OriginalLayer=i+1});
            }
            top=bottom;
        }
        return profile;
    }
}
