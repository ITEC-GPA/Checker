using GPC.Model.Materials;
using GPC.Model.Sections;
namespace GPC.Checkers.Geotechnics.Piles;

public sealed class ElasticPileSection
{
    public string Reference { get; set; } = "";
    public string Material { get; set; } = "";
    public string Assumption { get; set; } = "";
    public double DiameterMm { get; set; }
    public double ThicknessMm { get; set; }
    public double ModulusMpa { get; set; }
    public double InertiaMm4 { get; set; }
    public double BaseEI { get; set; }
    public double EI { get; set; }
    public string OverrideReason { get; set; } = "";
    public static ElasticPileSection Concrete(double diameterMetres,double fck)
    {
        Positive(diameterMetres);if(fck<12||fck>90||double.IsNaN(fck))throw new ArgumentException("fck fuori campo 12–90 MPa.");
        var material=new ConcreteMaterialEN1992("Calcestruzzo",fck,ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
        var section=new SectionCircular(diameterMetres*1000);
        return Build(section.J11,material.Ecm,diameterMetres*1000,0,"sezione",$"Calcestruzzo fck={fck:G6} MPa; Ecm da Model EN1992", "Sezione circolare integra lorda: Ecm·J; contributo aggiuntivo delle armature, fessurazione e viscosità esclusi.");
    }
    public static ElasticPileSection Tube(double diameterMm,double thicknessMm,double modulusMpa)
    {
        Positive(diameterMm);Positive(thicknessMm);Positive(modulusMpa);if(2*thicknessMm>=diameterMm)throw new ArgumentException("CHS: 2t deve essere minore di D.");
        return Build(new SectionCHS(diameterMm,thicknessMm).J11,modulusMpa,diameterMm,thicknessMm,"sezione","Acciaio CHS", "Solo tubolare d'acciaio: EJ=Es·J. Malta, iniezione e terreno non contribuiscono alla rigidezza strutturale.");
    }
    static ElasticPileSection Build(double j,double e,double d,double t,string reference,string material,string assumption)
    {double ei=e*j/1e9;Positive(ei);return new ElasticPileSection{Reference=reference,Material=material,Assumption=assumption,DiameterMm=d,ThicknessMm=t,ModulusMpa=e,InertiaMm4=j,BaseEI=ei,EI=ei};}
    public void Override(double ei,string reason){Positive(ei);if(string.IsNullOrWhiteSpace(reason))throw new ArgumentException("Motivazione EJ obbligatoria.");EI=ei;OverrideReason=reason;}
    static void Positive(double v){if(double.IsNaN(v)||double.IsInfinity(v)||v<=0)throw new ArgumentException("Geometria, modulo ed EJ devono essere finiti e positivi.");}
    public static double TotalLength(double embedded,double free){Positive(embedded);if(double.IsNaN(free)||double.IsInfinity(free)||free<0)throw new ArgumentException("Tratto libero non valido.");return embedded+free;}
    // e is the existing eccentricity above ground; C is conjugate to theta=y' downward.
    public static double HeadCouple(double force,double eccentricityFromGround,double free)=>force*(free-eccentricityFromGround);
    public static double GroundEccentricity(double force,double free,double legacyMoment,double legacyEccentricity)
    {if(force==0){if(legacyMoment!=0)throw new ArgumentException("Momento puro legacy: migrazione automatica non rappresentabile mediante H ed eccentricità. Conservare il modello legacy e scegliere esplicitamente i dati comuni.");return free;}return free-legacyMoment/force-legacyEccentricity;}
}
public sealed class ElasticPileNode
{
    public double Depth { get; set; }
    public double TributaryTop { get; set; }
    public double TributaryBottom { get; set; }
    public double EquivalentSpring { get; set; }
}
public sealed class ElasticPileSectionDemand
{
    public double AxialForce { get; set; }
    public double Depth { get; set; }
    public string Side { get; set; } = "";
    public double Shear { get; set; }
    public double Moment { get; set; }
    public string SectionReference { get; set; } = "";
    public List<string> ExtremeReferences { get; set; } = new List<string>();
}
