using Common = GPC.Checkers.Steel.EffectiveSections.EffectivePlateReduction;
using Plate = GPC.Checkers.Steel.EffectiveSections.EffectivePlate;

namespace GPC.Checkers.CompositeBridge;

/// <summary>Compatible bridge facade over the shared Steel plate-local kernel. Compression negative; mm/MPa.</summary>
public static class EffectivePlateReduction
{
    public static BridgePlate InternalPlate(double b,double t,double startStress,double endStress,double fy)
        => Map(Common.InternalPlate(b,t,startStress,endStress,fy));
    public static BridgePlate Outstand(double b,double t,double stress,double fy)
        => Map(Common.Outstand(b,t,stress,fy));
    private static BridgePlate Map(Plate p)=>new(p.Width,p.Thickness,p.Psi,p.KSigma,p.Lambda,p.Rho,p.CompressedWidth,
        p.EffectiveAtStart,p.EffectiveAtEnd,p.StartStress,p.EndStress);
}
