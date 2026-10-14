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
        Assert.ThrowsException<ArgumentException>(()=>new MicropileDepthLayer(1,BustamanteDoixSoil.Silt,double.NaN));
        Assert.ThrowsException<ArgumentException>(()=>new MicropileDepthSurvey(Array.Empty<MicropileDepthLayer>()));
        Assert.ThrowsException<ArgumentException>(()=>survey.AlongAxis(Math.PI/2));
    }
}
