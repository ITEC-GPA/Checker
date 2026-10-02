using System.Globalization;
using System.Text.Json.Nodes;
using GPC.Checkers.Geotechnics.Piles;
using GPC.Model.Geotechnics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static GeotechnicsTests.GeotechnicsFixture;

namespace GeotechnicsTests;

/// <summary>Reading of the pile fixtures (JSON lines of the ANTHEA harness) and conversion of the legacy sheets to the typed inputs with Model types.</summary>
internal static class PilesFixture
{
    public static IEnumerable<JsonObject> Lines(string file) => File.ReadLines(Path.Combine(Folder, file)).Skip(1).Select(l => JsonNode.Parse(l)!.AsObject());

    /// <summary>A number of the legacy JSON (number or text); null when absent or blank.</summary>
    public static double? Num(JsonNode? n) => n == null ? null : double.TryParse(n.ToString().Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;
    public static double Req(JsonNode? n, string what) => Num(n) ?? throw new AssertFailedException("Missing number " + what);
    public static string Str(JsonNode? n) => n?.ToString() ?? "";
    public static bool Bool(JsonNode? n, bool fallback = false) => n is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;
    public static bool IsError(JsonNode? result) => result is JsonObject o && (o.ContainsKey("error") || o.ContainsKey("errore") && Str(o["errore"]).Length > 0);

    public static readonly Dictionary<string, BustamanteDoixSoil> BdSoils = Enum.GetValues<BustamanteDoixSoil>().ToDictionary(BustamanteDoix.Label, s => s);

    /// <summary>Profile from the legacy layers (thicknesses from the ground at elevation 0), with the water table at the given depth (m).</summary>
    public static SoilProfile Profile(JsonArray layers, double? waterDepth, Func<JsonObject, int, Soil> soil)
    {
        var list = new List<SoilLayer>(); double top = 0;
        for (int i = 0; i < layers.Count; i++)
        {
            double bottom = top + Req(layers[i]!["spessore"], "spessore");
            list.Add(new SoilLayer(soil(layers[i]!.AsObject(), i), -top * M, -bottom * M)); top = bottom;
        }
        return new SoilProfile("survey", list, "ANTHEA fixture", waterDepth.HasValue ? -waterDepth.Value * M : null);
    }

    /// <summary>Model soil of a legacy pile layer: γ, γsat (γ when blank), φ', c', cu (null when blank or zero).</summary>
    public static Soil PileSoil(JsonObject l, int i)
    {
        double gamma = Req(l["peso_specifico"], "γ"), sat = Num(l["peso_specifico_saturo"]) ?? gamma, cu = Num(l["coesione_non_drenata"]) ?? 0;
        return new Soil("Layer " + (i + 1), gamma * KN3, sat * KN3, (Num(l["angolo_attrito"]) ?? 0) * Deg, (Num(l["coesione_efficace"]) ?? 0) * KPa, "ANTHEA fixture",
            undrainedShearStrength: cu > 0 ? cu * KPa : null);
    }

    public static SoilBehaviour Behaviour(JsonObject l) => Str(l["tipologia"]) == "Coesivo" ? SoilBehaviour.Cohesive : SoilBehaviour.Granular;

    public static int Profiles(string text) => text == "≥10" ? 10 : int.Parse(text, CultureInfo.InvariantCulture);

    /// <summary>"CHS 139.7 × 8" → (139.7, 8).</summary>
    public static (double D, double T) Designation(string profile)
    {
        var p = profile.Replace("CHS", "").Split('×').Select(s => double.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
        return (p[0], p[1]);
    }
}
