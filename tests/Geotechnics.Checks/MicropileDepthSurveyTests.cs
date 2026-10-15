using GPC.Checkers.Geotechnics.Piles;
using GPC.Model.Geotechnics;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Standards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class MicropileDepthSurveyTests
{
    [TestMethod]
    public void ThicknessSurveyPreservesCompleteCapacityCurvesWithoutSoilMechanics()
    {
        foreach(double angle in new[]{0d,15d,30d}) foreach(bool active in new[]{false,true})
        {
            var soil=new Soil("Only legacy profile",18e-6,20e-6,0,0,"before API");
            var profile=new SoilProfile("Survey",new[]{new SoilLayer(soil,0,-4000),new SoilLayer(soil,-4000,-12000)},"test");
            var original=new MicropileSurvey(profile,new[]{(BustamanteDoixSoil.MediumSand,1.4,active),(BustamanteDoixSoil.Gravel,1.8,true)});
            var layers=new[]{new MicropileDepthLayer(4000,BustamanteDoixSoil.MediumSand,1.4,active),new MicropileDepthLayer(8000,BustamanteDoixSoil.Gravel,1.8)};
            var survey=new MicropileDepthSurvey(layers);layers[0]=new(100,BustamanteDoixSoil.Clay,1);
            var pile=new Micropile(250,10300,angle*Math.PI/180,MicropileInjection.IGU,3,1200,15,new SectionCHS(114.3,6.3),new SteelMaterial("S355",210000,355,510),compressionAction:200000,tensionAction:100000);
            var factors=PileResistanceFactors.FromStandard(new StandardNTC2018Geotechnics(),1);
            var before=AxialPileCapacity.Calculate(pile,new[]{original},factors,PileGroupEfficiency.None());
            var after=AxialPileCapacity.CalculateFromDepths(pile,new[]{survey},factors,PileGroupEfficiency.None());
            Assert.AreEqual(before.Depths.Count,after.Depths.Count);
            foreach(var key in before.Curves.Keys)
            {
                CollectionAssert.AreEqual(before.Curves[key].Design.ToArray(),after.Curves[key].Design.ToArray());
                CollectionAssert.AreEqual(before.Curves[key].Mean.ToArray(),after.Curves[key].Mean.ToArray());
                CollectionAssert.AreEqual(before.Curves[key].Minimum.ToArray(),after.Curves[key].Minimum.ToArray());
            }
            for(int i=0;i<before.Depths.Count;i++)
            {
                var a=before.Depths[i];var b=after.Depths[i];Assert.AreEqual(a.Weight,b.Weight);Assert.AreEqual(a.Depth,b.Depth);
                Assert.AreEqual(a.Surveys[0].Shaft,b.Surveys[0].Shaft);Assert.AreEqual(a.Surveys[0].Base,b.Surveys[0].Base);
            }
            CollectionAssert.AreEqual(before.Warnings.ToArray(),after.Warnings.ToArray());
            CollectionAssert.AreEqual(before.CompressionActions.ToArray(),after.CompressionActions.ToArray());
            CollectionAssert.AreEqual(before.TensionActions.ToArray(),after.TensionActions.ToArray());
            Assert.AreEqual(4000d,survey.Layers[0].Thickness);
        }
    }
    /// <summary>
    /// PV-4: α is needed only in the active layers that intersect the grouted length [sb, L]. Micropile L = 12 m, sb = 3 m, IGU, D 0.2 m, CHS
    /// 88.9x8, p_i = 1.5 MPa: an active layer 0–3 m with α = 0 (or NaN) above the grouted zone and a layer below the tip without α give the same
    /// result as α = 1.15 in the layer above (it never contributes); an active layer inside the zone without α is rejected naming vertical and layer.
    /// </summary>
    [TestMethod]
    public void AlphaIsRequiredOnlyInTheGroutedLength()
    {
        var pile=new Micropile(200,12000,0,MicropileInjection.IGU,1.5,3000,null,new SectionCHS(88.9,8),new SteelMaterial("S355",210000,355,510),compressionAction:300000);
        var factors=PileResistanceFactors.FromStandard(new StandardNTC2018Geotechnics(),1);
        MicropileDepthSurvey Survey(double above,double below)=>new(new[]{new MicropileDepthLayer(3000,BustamanteDoixSoil.MediumSand,above),
            new MicropileDepthLayer(12000,BustamanteDoixSoil.MediumSand,1.15),new MicropileDepthLayer(5000,BustamanteDoixSoil.Clay,below)});
        var reference=AxialPileCapacity.CalculateFromDepths(pile,new[]{Survey(1.15,1.2)},factors,PileGroupEfficiency.None());
        foreach(var (above,below) in new[]{(0d,1.2),(double.NaN,1.2),(1.15,0d),(-1d,double.NaN)})
        {
            var r=AxialPileCapacity.CalculateFromDepths(pile,new[]{Survey(above,below)},factors,PileGroupEfficiency.None());
            foreach(var key in reference.Curves.Keys) CollectionAssert.AreEqual(reference.Curves[key].Design.ToArray(),r.Curves[key].Design.ToArray(),$"α {above}/{below}");
        }
        var inside=new MicropileDepthSurvey(new[]{new MicropileDepthLayer(3000,BustamanteDoixSoil.MediumSand,1.15),new MicropileDepthLayer(12000,BustamanteDoixSoil.MediumSand,0)});
        var e=Assert.ThrowsException<ArgumentException>(()=>AxialPileCapacity.CalculateFromDepths(pile,new[]{inside},factors,PileGroupEfficiency.None()));
        StringAssert.StartsWith(e.Message,"Vertical 1: Layer 2 (grouted from 3 to 12 m along the axis): α IGU: a positive value is required.");
    }
    [TestMethod]
    public void DepthConversionAndInvalidDataAreExplicit()
    {
        var survey=new MicropileDepthSurvey(new[]{new MicropileDepthLayer(3000,BustamanteDoixSoil.Silt,1.2),new MicropileDepthLayer(2000,BustamanteDoixSoil.Silt,double.NaN,false)});
        var layers=survey.AlongAxis(Math.PI/3);
        Assert.AreEqual(6000d,layers[0].Bottom,1e-9);Assert.AreEqual(10000d,layers[1].Bottom,1e-9);
        Assert.IsFalse(layers[1].ShaftActive);
        foreach(double invalid in new[]{0d,-1d,double.NaN,double.PositiveInfinity})
            Assert.ThrowsException<ArgumentException>(()=>new MicropileDepthLayer(invalid,BustamanteDoixSoil.Silt,1));
        Assert.ThrowsException<ArgumentException>(()=>new MicropileDepthLayer(1,(BustamanteDoixSoil)999,1));
        Assert.ThrowsException<ArgumentException>(()=>new MicropileDepthSurvey(Array.Empty<MicropileDepthLayer>()));
        Assert.ThrowsException<ArgumentException>(()=>survey.AlongAxis(Math.PI/2));
    }
}
