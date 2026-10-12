using GPC.Model.Geotechnics;

namespace GPC.Checkers.Geotechnics.Foundations;

internal static class FoundationSoilProfile
{
    internal static ShallowFoundationInput Prepare(ShallowFoundationInput source)
    {
        var profile=source.SoilProfile;if(profile is null)return source;
        if(!source.HomogeneousHalfSpace)throw new NotSupportedException("La portanza richiede l'ipotesi esplicita di strato portante omogeneo e semispazio; il metodo per strato portante di spessore finito non è implementato.");
        if(profile.Layers.Count>50)throw new ArgumentException("Profilo terreno: massimo 50 strati.");
        double elevation=profile.GroundSurface-source.Depth;
        if(elevation<=profile.Base)throw new ArgumentException("Il profilo deve descrivere terreno sotto il piano di posa; completare gli spessori.");
        var bearing=profile.LayerAt(elevation).Soil;
        foreach(var layer in profile.Layers.Where(l=>l.Bottom<elevation))
        {
            var s=layer.Soil;
            if(s.UnitWeight!=bearing.UnitWeight||s.SaturatedUnitWeight!=bearing.SaturatedUnitWeight||s.FrictionAngle!=bearing.FrictionAngle||s.EffectiveCohesion!=bearing.EffectiveCohesion||s.UndrainedShearStrength!=bearing.UndrainedShearStrength)
                throw new NotSupportedException("Proprietà diverse sotto il piano di posa: la portanza multistrato non è implementata. Il profilo è conservato e non viene sostituito con valori medi.");
        }
        var x=source.Copy();x.FrictionAngle=bearing.FrictionAngle;x.Cohesion=bearing.EffectiveCohesion;
        x.UndrainedStrength=bearing.UndrainedShearStrength??0;x.UnitWeight=bearing.UnitWeight;x.SaturatedUnitWeight=bearing.SaturatedUnitWeight;
        x.WaterEnabled=profile.GroundwaterElevation.HasValue;x.WaterDepth=profile.GroundSurface-(profile.GroundwaterElevation??profile.GroundSurface);x.WaterUnitWeight=profile.WaterUnitWeight;
        x.ProfileOverburden=source.Condition==FoundationSoilCondition.Drained?profile.VerticalEffectiveStress(elevation):profile.VerticalTotalStress(elevation);
        return x;
    }
}
