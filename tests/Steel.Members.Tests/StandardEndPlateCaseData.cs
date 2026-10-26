using GPC.Checkers.Steel.Connections;
using GPC.Checkers.Steel.Inputs;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;

namespace Steel.Members.Tests;

// Runnable application example. All actions below are the real load case.
// No baseline, capacity token, stiffness length or fake zero-load case is built here.
internal static class StandardEndPlateCaseData
{
    internal static StandardEndPlateInput Input(bool ntc=false,bool flush=false,double n=10000,double moment=5e6,double shear=20000,
        bool single=false,string problem="",double columnN=-10000)
    {
        var code=ntc?ConnectionDesignCode.Ntc2018():ConnectionDesignCode.Eurocode3(1,1.1,1.25,"Declared EC parameters");
        var policy=ntc?ColumnWebPolicy.Ntc2018(code):ColumnWebPolicy.Eurocode3(code,1);
        double tw=problem=="slender"?10.1:20;
        double area=2*209.3*15.6+501.9*tw+(4-Math.PI)*12.7*12.7;
        var shape=new SectionH(533.1,tw,209.3,15.6,209.3,15.6,"Analytic application detail",12.7);
        var section=new SteelSectionInput(new SteelSection(shape,new SteelMaterial("Actual steel",210000,275,410),
            Section.SectionTypes.Rolled,Section.FormedTypes.HotFinished),true);
        var beam=new EndPlateBeamSectionInput(code,new(533.1,209.3,tw,15.6,12.7,area,275,410),section,
            new(n,0,shear,problem=="torsion"?1000:0,moment,0),275,410,false,"Actual product strengths", "Actual Model geometry",
            problem=="noCombination"?"":"FEM combination ULS1 at column flange face",ntc?null:1.2,ntc?null!:"EC eta1.2");
        var column=new StandardEndPlateColumn(new(266.7,258.8,12.8,20.5,12.7,13600,265,410),265,410,1000,1000,
            "Column product strengths","Actual column sway restraint");
        double[] rows=single?new[]{60d}:flush?new[]{60d,150,383.1,473.1}:new[]{-40d,60,150,383.1,473.1};
        if(problem=="unordered")Array.Reverse(rows);
        bool supplied=single||problem=="missingAlpha";
        var layout=new StandardEndPlateLayout(new(250,25,345,470),new(24,26,353,ConnectionBoltGrade.Class8_8,true),100,rows,
            flush?0:90,46.9,16,12,12,supplied?EndPlateAlphaMethod.SuppliedChartValues:EndPlateAlphaMethod.SciP398Approximation,
            single?EndPlateFlangeBoundaries.UpperOnly:EndPlateFlangeBoundaries.BothFlanges,"Actual plate/bolt/weld geometry",
            single?new TStubChartAlpha(8,"Independent supplied chart reading"):null);
        var bolts=new StandardEndPlateFasteners(new(36,39.55,36,39.55,41.6,41.6,"Actual M24 hardware"),70.5,
            "25+20.5+4+4+(15+19)/2 mm", "Qualified standard holes", "Symmetric uniform vertical shear in all bolts");
        var weldDetail=new EndPlateWeldDetail(FilletWeldEndCondition.FullSize,FilletWeldEndCondition.FullSize,FilletWeldEndCondition.FullSize,
            problem=="bearingOnly"?EndPlateCompressionTransfer.QualifiedDirectBearing:EndPlateCompressionTransfer.FullStrengthWelds,
            "Continuous physical fillets","Normal force transfer including reversal","All V through web welds","Matching consumable","Qualified transitions/eccentricity","Actual contact");
        var welds=flush?StandardEndPlateWelds.TopFlushCjp(FilletWeldSteelGrade.S275,FilletWeldSteelGrade.S355,weldDetail,
            new(15.6,209.3,12,0,355,510,true,"CJP qualification","Inspection","Actual inner toe","Through-thickness qualification"))
            :StandardEndPlateWelds.PairedFillets(FilletWeldSteelGrade.S275,FilletWeldSteelGrade.S355,weldDetail);
        double translated=moment-133.35*shear;
        var upper=new SteelActions(columnN,0,n/2,0,-translated/2,0);
        var lower=new SteelActions(columnN-shear,0,-n/2+(problem=="badCuts"?1000:0),0,translated/2,0);
        return new(policy,beam,column,layout,bolts,welds,new(209.3,15.6,209.3,15.6,"Actual initially closed flange bands",problem=="gap"?.1:0),
            upper,lower,"Actual cuts on continuous axes projected to column centroid at beam midheight",true,"Panel separately modelled");
    }
}
