namespace GPC.Design.Bridges;
public static partial class BridgeConcept
{
    private static IReadOnlyList<T> Own<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
    private static Result Own(Result r) => r with
    {
        Spans=Own(r.Spans), Supports=Own(r.Supports), Quantities=Own(r.Quantities), Details=Own(r.Details),
        CostQuantities=Own(r.CostQuantities), Stations=Own(r.Stations), Warnings=Own(r.Warnings),
        Advanced=r.Advanced is {} a ? a with { Members=Own(a.Members),Dimensions=Own(a.Dimensions),FoundationSchedule=Own(a.FoundationSchedule) } : null
    };
}
