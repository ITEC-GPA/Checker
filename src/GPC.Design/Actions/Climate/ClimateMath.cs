using GPC.Model.Actions.Climate;

namespace GPC.Design.Actions.Climate;

internal static class ClimateMath
{
    internal const string Ntc = "Italia · NTC 2018 (DM 17/01/2018) · Circolare 21/01/2019 n. 7";
    internal const string SnowEn = "EN 1991-1-3:2003 + AC:2009";
    internal const string WindEn = "EN 1991-1-4:2005 + A1:2010 + AC:2010";
    internal const string ThermalEn = "EN 1991-1-5:2003 + AC:2009";
    internal const string Cnr = "CNR-DT 207 R1/2018";
    internal static void Require(bool condition, string message) { if (!condition) throw new ArgumentException(message); }
    internal static double Range(double x, double min, double max, string name)
    { Require(!double.IsNaN(x) && !double.IsInfinity(x) && x >= min && x <= max, $"{name}: richiesto valore finito tra {min} e {max}."); return x; }
    internal static double Positive(double x, string name) { Range(x, 0, double.MaxValue, name); Require(x > 0, name + ": richiesto valore positivo."); return x; }
    internal static void EnumValue<T>(T value) where T : struct { Require(Enum.IsDefined(typeof(T), value), "Scelta non riconosciuta: " + typeof(T).Name); }
    internal static void National(ClimateStandard standard, string source)
    { EnumValue(standard); if (standard == ClimateStandard.EurocodeFirstGeneration) Require(!string.IsNullOrWhiteSpace(source), "Indicare paese, parametri nazionali e fonte del progetto EN (nessuna appendice nazionale selezionata automaticamente)."); }
    internal static double Clamp(double x, double min, double max) => Math.Max(min, Math.Min(max, x));
    internal static double Lerp(double x, double a, double b, double ya, double yb) => ya + (yb - ya) * (x - a) / (b - a);
    internal static ClimateValue V(string symbol, string description, double value, string unit, string formula, string reference)
    { Range(value, -double.MaxValue, double.MaxValue, symbol); return new ClimateValue { Symbol = symbol, Description = description, Value = value, Unit = unit, Formula = formula, Reference = reference }; }
    internal static ClimateCase Case(ClimateResult r, string name, string description = "")
    { var c = new ClimateCase { Name = name, Description = description }; r.Cases.Add(c); return c; }
    internal static ClimateResult Result(ClimateStandard standard, string en, string scope, string source)
    {
        National(standard, source);
        var r = new ClimateResult { Standard = standard == ClimateStandard.Ntc2018 ? Ntc : en, Scope = scope };
        r.Notes.Add("Valori caratteristici; coefficienti parziali e combinazioni con altre azioni da applicare nel modello strutturale.");
        if (standard == ClimateStandard.EurocodeFirstGeneration) r.Notes.Add("Eurocodice di prima generazione, valori raccomandati salvo parametri assegnati. Fonte dei parametri nazionali/progettuali: " + source);
        return r;
    }
}
