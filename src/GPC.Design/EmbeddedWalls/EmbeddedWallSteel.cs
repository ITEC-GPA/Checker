using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Checkers.Steel.Members;
using GPC.Model.Checking.Contracts;
using GPC.Model.Design.Steel;
using GPC.Model.Geotechnics.EmbeddedWalls;
using GPC.Model.Standards;

namespace GPC.Design.EmbeddedWalls;

public sealed record EmbeddedWallSteelSupport(string SupportId, AxialSteelMember Member);
public enum EmbeddedWallSteelCheckKind { StaticUltimate, SeismicUltimate, Serviceability, Analysis }

/// <summary>One physical support, one stage and one independently analysed design combination.</summary>
public sealed record EmbeddedWallSteelResult(string CaseId, string StageId, string SupportId, EmbeddedWallSupportKind Kind,
    double NormalForce, double AnalysedAxialStiffness, bool AtModelCapacity, AxialSteelResult? Verification,
    EngineeringOutcome Outcome, string Diagnostic);

public static class EmbeddedWallSteel
{
    /// <summary>Forces are already design values. No nonlinear superposition, partial action factor, spacing multiplier
    /// or cosine projection is applied. Missing definitions remain explicit. These are checks of the steel axial member,
    /// not of the anchorage, connection, ground bond or the complete retaining system.</summary>
    public static IReadOnlyList<EmbeddedWallSteelResult> CheckSupports(WallAnalysisResult analysis,
        IReadOnlyList<EmbeddedWallSteelSupport> definitions, StandardEN1993p11? standard, EmbeddedWallSteelCheckKind kind,
        CancellationToken cancellation = default)
    {
        Guard.NotNull(analysis); Guard.NotNull(definitions);
        if (!analysis.IsComplete) throw new InvalidOperationException("An incomplete nonlinear history cannot be certified by support checks.");
        if (!PortableEnum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        var members = new Dictionary<string, AxialSteelMember>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (definition is null || string.IsNullOrWhiteSpace(definition.SupportId) || definition.Member is null
                || !members.TryAdd(definition.SupportId, definition.Member))
                throw new ArgumentException("Unique support identifiers and owned steel member definitions required.");
        }
        var rows = new List<EmbeddedWallSteelResult>();
        foreach (var stage in analysis.Stages)
        foreach (var support in stage.Supports)
        {
            cancellation.ThrowIfCancellationRequested();
            AxialSteelResult? verification = kind == EmbeddedWallSteelCheckKind.StaticUltimate && members.TryGetValue(support.Id, out var member)
                ? Ntc2018AxialSteel.Check(member, support.NormalForce, standard, support.Kind == EmbeddedWallSupportKind.Strut) : null;
            string diagnostic = kind != EmbeddedWallSteelCheckKind.StaticUltimate ? "Percorso verificato per SLU statici. Sisma, SLE e azioni non classificate richiedono verifiche distinte."
                : verification is null ? "Definizione della membratura metallica assente: verifica non eseguita."
                : "Forza per singolo sostegno; rigidezza assiale usata nell'analisi conservata separatamente. Verifiche dell'asta centrata; collegamenti, ancoraggi, bulbo e durabilità separati.";
            if (support.AtCapacity) diagnostic += " Sostegno al limite costitutivo assegnato nel modello: la verifica dell'asta non dimostra la sicurezza del meccanismo complessivo.";
            rows.Add(new(analysis.CaseId, stage.Id, support.Id, support.Kind, support.NormalForce, support.AxialStiffness,
                support.AtCapacity, verification, verification?.Outcome ?? EngineeringOutcome.NotEvaluated, diagnostic));
        }
        cancellation.ThrowIfCancellationRequested();
        return rows.AsReadOnly();
    }
}
