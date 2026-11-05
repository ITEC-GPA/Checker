using GPC.Model.Analysis;

namespace GPC.Checkers.CompositeBridge;

/// <summary>Elastic uncracked slab eigenstrain, N-mm-MPa. Generalized strains represent the external restraint effects;
/// the concrete eigenstrain must still enter the section checker once. No secondary actions are inferred here.</summary>
public static class CompositeShrinkageActions
{
    public static (double FreeStrain,double Curvature,double PreStrain,double ThermalGradient,double EigenForce,double EigenMoment,double Centroid)
        Calculate(CompositeSectionDefinition definition,double strain)
    {
        if(definition==null)throw new ArgumentNullException(nameof(definition));definition.Validate();
        var section=definition.PhysicalSection;
        if(!definition.IncludeConcrete||section.SteelSections.Count!=1)throw new ArgumentException("ShrinkageRequiresConcreteAndOneStructuralSteelSection");
        if(double.IsNaN(strain)||double.IsInfinity(strain))throw new ArgumentOutOfRangeException(nameof(strain));
        var steel=section.SteelSections.Single().Section.SteelMaterial;
        double n=definition.EffectiveShrinkageRatio,ea=steel.ElasticModulusTension,alpha=steel.AlfaThermalExpansion;
        if(double.IsNaN(alpha)||double.IsInfinity(alpha)||alpha==0)throw new ArgumentException("ShrinkageRequiresNonzeroSteelThermalExpansion");
        double phi=BridgeCreepCoefficient.FromModularRatio(n,ea,section.ConcreteMaterial.ElasticModulusCompression);
        var p=GPC.Model.Analysis.CompositeSectionProperties.Calculate(section,phi,true,definition.IncludeReinforcement,true);
        if(Math.Abs(p.JxyH)>1e-10*(p.JxxH+p.JyyH)||Math.Abs(p.J11H-p.JxxH)>1e-8*p.JxxH)
            throw new NotSupportedException("ShrinkageRequiresHorizontalMajorPrincipalAxis");
        // The existing bridge method applies to a slab above steel, with no overlapping structural steel area.
        foreach(var placement in section.SteelSections)
            if(placement.GetGlobalOutlines().Any(outline=>outline.GetBoundingBox().Max.Y>section.SectionShape.Shape.GetBoundingBox().Min.Y+1e-8))
                throw new NotSupportedException("ShrinkageSlabMustNotOverlapStructuralSteel");
        double ac=section.Area,sc=ac*section.Centroid.Y;
        if(definition.IncludeReinforcement)foreach(var bar in section.Rebars){ac-=bar.RebarSection.Area;sc-=bar.RebarSection.Area*bar.Position.Y;}
        double force=ea/n*ac*strain,moment=ea/n*(sc-ac*p.centroidH.Y)*strain;
        double epsilon=force/(ea*p.areaH/n),curvature=moment/(ea*p.JxxH/n);
        return (epsilon,curvature,-epsilon,curvature/alpha,force,moment,p.centroidH.Y);
    }
}
