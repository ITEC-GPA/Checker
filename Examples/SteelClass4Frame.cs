using System.Linq;
using GPC.Model.Checker;
using GPC.Model.Checker.Configuration;
using GPC.Model.Data.Steel;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Structure.Assignments;

namespace GPC.Examples
{
    /// <summary>Headless class4 variant of SteelReferenceFrame. Assigned factored
    /// analytical actions, not an FE solution. RHS400x200x4 columns, SCI UKB406x140x39
    /// crossbar. Same explicit restraints and two concomitant states; N,mm,MPa,Nmm.</summary>
    public static class SteelClass4Frame
    {
        public static GPC.Model.Models.Model Create(bool split=false,bool reverseSecond=false)
            =>SteelReferenceFrame.Create(split,reverseSecond,model=> {
                var column=new SteelSection(new SectionRHS(400,200,4,4,4,4,"RHS400x200x4"),SteelMaterialEN1993Data.S355);
                var crossbar=new SteelSection(new SectionH(398,6.4,141.8,8.6,141.8,8.6,"UKB406x140x39",10.2),SteelMaterialEN1993Data.S355);
                model.AddProperty(column);model.AddProperty(crossbar);
                foreach(var beam in model.BeamElements.Values) {
                    var section=beam.Assignments.LogicalMember=="B1"?crossbar:column;
                    beam.BeamProperty=section;beam.Assignments.Sections.Clear();
                    beam.Assignments.Sections.Add(new BeamSectionAssignment{Start=0,End=1,Property=section});
                }
            });

        public static VerificationConfiguration Configure(GPC.Model.Models.Model model,bool ntc)
        {
            var config=SteelReferenceFrame.Configure(model,ntc);
            foreach(var engine in config.Engines) {
                var p=engine.Parameters;
                if(engine.Kind==SteelMemberPipelineChecker.EngineId&&engine.Id.StartsWith("C")) {
                    p.Remove("CompressionCurve1");p.Remove("CompressionCurve2");p.Remove("TorsionalCurve");p["CurveSelection"]="Automatic";
                }
                if(engine.Kind==SteelMemberPipelineChecker.EngineId&&engine.Id.StartsWith("B"))
                    p["Mcr.Source"]="Conservative elastic fork H value: pi/L sqrt(E I2 G It), warping stiffness omitted; uniform moment, L=4000mm, E=210000MPa, nu=.3. Full-member model assessed separately.";
                if(engine.Kind==SteelPipelineMaterialChecker.EngineId&&engine.Id.StartsWith("B")) {
                    p.Remove("ShearBucklingExcluded2");p.Remove("ShearBucklingSource2");
                    p["HWebResistance"]="NonRigidEndPost";
                    p["HWebSupportSource"]="Example boundary condition: support stiffeners, welds and force introduction independently adequate; web contribution only, non-rigid end posts.";
                }
            }
            return config;
        }
    }
}
