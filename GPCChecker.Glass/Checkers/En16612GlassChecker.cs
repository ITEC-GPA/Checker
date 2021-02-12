using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Checker.Glasses.Wrappers;
using GPC.Checker.Glasses.FemModel;
using GPC.Model.Loads;
using GPC.Model.Elements;
using GPC.Geometry.Meshes;
using GPC.Geometry;

namespace GPC.Checker.Glasses.Checkers
{
    public class En16612GlassChecker : GlassChecker
    {

        public En16612GlassChecker(Model model, CheckParameters checkParameters) 
            : base(model, checkParameters)
        {

        }


        #region Override public methods

        /// <summary>
        /// Metodo responsabile della verifica
        /// 1 - Genera la mesh
        /// 2 - Orchestra il modello fem
        /// 3 - Prende i risultati 
        /// 4 - Fa la verifica
        /// </summary>
        /// 
        public override void SetUpFemModels()
        {
            List<GlassWrapper> wrappers = GetWrappers();

            //femWrapper.SetAnalysisType(_checkParameters.GetAnalysisType());

            
            foreach (var wrapper in wrappers)
            {
                FemModelWrapper2 femWrapper = new FemModelWrapper2("fem" + wrappers.IndexOf(wrapper), _checkParameters.GetAnalysisType());
                if (wrapper is MonolithicGlassWrapper mgw)
                {
                    var geometryMesh = mgw.Mesh;
                    var embeddedGeometriesMapVertex = mgw.EmbeddedGeometriesMapVertex;

                    var restrains = mgw.GetRestrains();

                    mgw.GetLoads(out List<Load> uniformPressureLoads, out List<Load> notUniformPressureLoads);

                    var monolithicGlassProperty = new MonolithicGlassProperty(mgw.Glass);

                    femWrapper.SetUpMonolithic(geometryMesh, embeddedGeometriesMapVertex, restrains, mgw.Glass, uniformPressureLoads, notUniformPressureLoads);
                }
                else if (wrapper is LaminatedGlassWrapper lgw)
                {
                    var geometryMesh = lgw.Mesh;
                    var embeddedGeometriesMapVertex = lgw.EmbeddedGeometriesMapVertex;

                    var restrains = lgw.GetRestrains();

                    lgw.GetLoads(out List<Load> uniformPressureLoads, out List<Load> notUniformPressureLoads);

                    femWrapper.SetUpLaminated(geometryMesh, embeddedGeometriesMapVertex, restrains, lgw, uniformPressureLoads, notUniformPressureLoads, CheckParameters.LaminatedAnalysisType.MultiElementPlateInterlayer);
                }
                else if (wrapper is InsulatedGlassWrapper igw)
                {
                    throw new NotImplementedException();
                }
                else
                    throw new NotSupportedException("Glass wrapper not supported");

                _femModels.Add(femWrapper);
            }
        }

        protected override string GetCheckerName() => "EN 16612 - 2019"; 
       
        #endregion

    }
}
