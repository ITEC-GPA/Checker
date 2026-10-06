using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Concrete.Piles
{
    public enum PileSeismicLinks { Unspecified, SingleHoops, Spiral }
    public sealed class PileSeismicZone
    {
        public double NominalLength { get; set; }
        public double MinimumEnd { get; set; }
        public double AdoptedEnd { get; set; }
        public bool MeetsMinimum => AdoptedEnd>=MinimumEnd;
    }
    public sealed class PileSeismicSettings
    {
        public bool Enabled { get; set; }
        public double? HeadLength { get; set; } // m from pile head; null = 10D
        public bool SeismicActionsConfirmed { get; set; }
        public bool ElasticMomentConfirmed { get; set; }
        public PileSeismicLinks Links { get; set; }
        public bool StandardNtc2018 { get; set; } = true;
    }
    public sealed class PileSeismicRule
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public double? Actual { get; set; }
        public double? Limit { get; set; }
        public string Unit { get; set; }
        public double? Depth { get; set; }
        public bool? Passed { get; set; }
        public string Criterion { get; set; }
        public string Reference => PileReinforcement.SeismicSource;
    }
    public sealed class PileSeismicResult
    {
        public string Reference => PileReinforcement.SeismicSource;
        public string SourceUrl => PileReinforcement.SeismicUrl;
        public bool Active { get; set; }
        public double RequiredHeadLength { get; set; }
        public double HeadEnd { get; set; }
        public bool IntersectsHeadZone { get; set; }
        public string Scope { get; set; }
        public List<PileSeismicRule> Checks { get; set; } = new List<PileSeismicRule>();
        public string Status => !Active?"Non attivo":Checks.Any(c=>c.Passed==false)?"Non soddisfatto":Checks.Any(c=>!c.Passed.HasValue)?"Parziale: dati o controlli da completare":"Soddisfatto nel perimetro NTC §7.2.5 implementato";
    }
    public sealed partial class PileReinforcement
    {
        public const string SeismicSource="NTC 2018 §7.2.5, Fondazioni su pali; G.U. 20/02/2018, S.O.8, PDF 217 / pagina stampata 213";
        public const string SeismicUrl="https://www.gazzettaufficiale.it/eli/gu/2018/02/20/42/so/8/sg/pdf#page=217";
        public static double SeismicHeadEnd(double diameterMm,double pileLength,double? assignedLength=null)
        {
            if(!Finite(diameterMm)||diameterMm<=0||!Finite(pileLength)||pileLength<=0||
                (assignedLength.HasValue&&(!Finite(assignedLength.Value)||assignedLength<=0)))
                throw new ArgumentException("Zona sismica: diametro e lunghezze positivi finiti richiesti.");
            return Math.Min(pileLength,assignedLength??10*diameterMm/1000);
        }
        public static PileSeismicZone DescribeSeismicHead(double diameterMm,double pileLength,double? assignedLength=null)
            =>new PileSeismicZone{AdoptedEnd=SeismicHeadEnd(diameterMm,pileLength,assignedLength),NominalLength=10*diameterMm/1000,MinimumEnd=SeismicHeadEnd(diameterMm,pileLength)};
        /// <summary>NTC 7.2.5 head-zone details and simplified conditions without a specific ductility assessment.
        /// Uses concomitant section actions and existing nominal resistances. No seismic action generation.</summary>
        public PileSeismicResult CheckHeadSeismic(PileSeismicSettings settings,double start,double end,double pileLength,IReadOnlyList<PileSectionCheck> sections)
        {
            if(settings==null)throw new ArgumentNullException(nameof(settings));
            var result=new PileSeismicResult{Active=settings.Enabled,Scope="Zona dissipativa presso la testa, se non si esclude il raggiungimento della capacità. Prescrizioni semplificate in assenza di valutazione specifica di duttilità. Azioni assegnate; non genera combinazioni, effetti cinematici, zone profonde o verifiche del nodo palo-plinto. Dettagli di giunzione e ancoraggio restano separati."};
            if(!settings.Enabled)return result;
            double zone=SeismicHeadEnd(p.Diameter,pileLength,settings.HeadLength),required=10*p.Diameter/1000;
            if(!Finite(start+end)||start<0||end<=start||end>pileLength||sections==null||!Enum.IsDefined(typeof(PileSeismicLinks),settings.Links))
                throw new ArgumentException("Tratto o tipologia staffe non validi per i controlli sismici.");
            var points=sections.Where(c=>c.Action.Depth>=start-1e-9&&c.Action.Depth<=end+1e-9).ToArray();
            if(points.Any(c=>new[]{c.Action.Depth,c.Action.N,c.Action.V,c.Action.M}.Any(v=>!Finite(v))))throw new ArgumentException("Azioni sismiche finite richieste.");
            result.RequiredHeadLength=required;result.HeadEnd=zone;result.IntersectsHeadZone=start<zone&&end>0;
            if(pileLength<required)result.Scope+=" Palo più corto di 10D: tutti i tratti sono controllati come zona di testa, senza estensione fittizia oltre la punta.";
            void Add(string key,string title,double? actual,double? limit,string unit,bool? passed,string criterion,double? depth=null)
                =>result.Checks.Add(new PileSeismicRule{Key=key,Title=title,Actual=actual,Limit=limit,Unit=unit,Passed=passed,Criterion=criterion,Depth=depth});
            var nominalStandard=new GPC.Model.Standards.StandardNTC2018Concrete();
            bool standard=settings.StandardNtc2018&&p.Standard is GPC.Model.Standards.StandardNTC2018Concrete&&typeof(GPC.Model.Standards.StandardModelCode2010).GetProperties().Where(prop=>prop.CanRead&&prop.PropertyType==typeof(double)&&(prop.Name.StartsWith("Gamma")||prop.Name=="AlphaCC")).All(prop=>Math.Abs((double)prop.GetValue(p.Standard)-(double)prop.GetValue(nominalStandard))<1e-10);
            Add("SeismicStandard","Normativa NTC 2018",null,null,"",standard?(bool?)true:null,"Coefficienti ordinari NTC richiesti; coefficienti unitari/custom non certificano la verifica NTC.");
            Add("SeismicHeadLength","Estensione della zona di testa",zone,Math.Min(required,pileLength),"m",zone>=Math.Min(required,pileLength),"Almeno 10D dalla testa; sul palo più corto si controlla l'intera lunghezza.");
            // Base pile minima always apply when this seismic mode is active, independently of the ordinary checkbox.
            double steel=geometry.Bars.Sum(b=>b.Area),phi=geometry.Bars.Min(b=>b.Diameter);
            bool head=result.IntersectsHeadZone;
            Add("SeismicSteelArea",head?"Armatura longitudinale in zona dissipativa":"Armatura longitudinale fuori zona dissipativa",steel,(head ? .01 : .003)*area,"mm²",steel>=(head ? .01 : .003)*area,head?"As ≥ 1% Ac in ogni tratto che interseca la zona di testa.":"As ≥ 0,3% Ac lungo il palo.");
            Add("SeismicLinkDiameter","Diametro armatura trasversale",p.LinkDiameter,8,"mm",p.LinkDiameter>=8,"φst ≥ 8 mm lungo il palo.");
            Add("SeismicLinkSpacing",head?"Passo staffe in zona dissipativa":"Passo staffe fuori zona dissipativa",p.LinkSpacing,(head?6:8)*phi,"mm",p.LinkSpacing<=(head?6:8)*phi,head?"s ≤ 6φL,min; staffe singole.":"s ≤ 8φL,min. Il minimo diametro longitudinale è la scelta conservativa per barre diverse.");
            if(head)Add("SeismicSingleHoops","Staffe singole nella zona di testa",null,null,"",settings.Links==PileSeismicLinks.Unspecified?(bool?)null:settings.Links==PileSeismicLinks.SingleHoops,"Staffe singole richieste; una spirale non soddisfa questa prescrizione. Ganci, trattenimento e confinamento da dettagliare.");
            var headPoints=points.Where(c=>c.Action.Depth<=zone+1e-9).ToArray();
            void Demand(string key,string title,IEnumerable<PileSectionCheck> source,Func<PileSectionCheck,double> actual,Func<PileSectionCheck,double?> limit,string unit,bool confirmed,bool strict,string criterion)
            {
                var candidates=source.ToArray();
                if(!confirmed||candidates.Length==0){Add(key,title,null,null,unit,null,criterion+" Azioni/condizioni mancanti o non confermate.");return;}
                var invalid=candidates.FirstOrDefault(c=>!limit(c).HasValue||!Finite(limit(c).Value)||limit(c)<=0);
                if(invalid!=null){Add(key,title,actual(invalid),null,unit,null,criterion+" Resistenza non disponibile (fuori dominio o modello non confermato).",invalid.Action.Depth);return;}
                var critical=candidates.OrderByDescending(c=>actual(c)/limit(c).Value).First();double demand=actual(critical),capacity=limit(critical).Value;
                Add(key,title,demand,capacity,unit,strict?demand<capacity:demand<=capacity,criterion,critical.Action.Depth);
            }
            bool seismic=settings.SeismicActionsConfirmed&&p.DesignActionsConfirmed&&standard;
            Demand("SeismicShear","Margine sismico a taglio",points,c=>1.3*Math.Abs(c.Action.V),c=>p.ShearModelConfirmed?c.VRd:null,"kN",seismic,false,"1,3 |VEd| ≤ VRd. Controllo lungo tutto il palo, senza cambiare VEd nel FEM.");
            if(head)Demand("SeismicCompression","Compressione media nella zona dissipativa",headPoints,c=>Math.Max(0,c.Action.N)*1000/area,c=>.45*p.Fcd,"MPa",seismic,true,"σc,media < 0,45 fcd; N della stessa combinazione sismica, positivo a compressione.");
            Demand("SeismicElasticMoment","Momento da analisi elastica",points,c=>Math.Abs(c.Action.M),c=>c.Action.M>=0?1.5*c.MRdPositive:-1.5*c.MRdNegative,"kNm",seismic&&settings.ElasticMomentConfirmed,true,"|Mel| < 1,5 MRd(N), nel verso locale. Confermare che M assegnato provenga da analisi elastica non ridotta (q=1); N concomitante sismico. Resta distinta la verifica ordinaria MEd ≤ MRd.");
            if(points.Any(c=>c.DevelopmentAvailable==false))Add("SeismicBarDevelopment","Disponibilità dello sviluppo delle barre",null,null,"",null,"Resistenze nominali calcolate; sviluppo o giunti non disponibili ad alcune quote: non certificano il dettaglio sismico.");
            return result;
        }
    }
}
