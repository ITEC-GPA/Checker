using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checker.Glasses.Wrappers;
using GPC.Checker.Glasses.FemModel;

namespace GPC.Checker.Glasses.Checkers
{
    public class PrEnGlassChecker : GlassChecker
    {

        public PrEnGlassChecker(Model model, LaminatedAnalysisType laminatedAnalysisType) 
            : base(model, laminatedAnalysisType)
        {

        }

        public override void Run()
        {
            List<GlassWrapper> wrappers = GetWrappers();

            var meshes = new List<List<FemMesh>>();
            foreach (var wrapper in wrappers)
            {
                if (wrapper is MonolithicGlassWrapper mgw)
                {
                    meshes.Add(mgw.GeneratePlateMesh());

                }
                else if (wrapper is InsulatedGlassWrapper igw)
                {

                }
                else
                    throw new NotSupportedException("Glass wrapper not supported");
            }

            //femWrapper.ExportMeshMSHFormat(System.IO.Path.Combine(this._model.OutputFolder, "mesh.msh"), l);

            FemModelWrapper femWrapper = new FemModelWrapper(meshes);


            femWrapper.ToSt7(System.IO.Path.Combine(this._model.OutputFolder, "1.st7"));
        }

        protected override string GetCheckerName() => "prEN 16612 - 2019";

    }
}
