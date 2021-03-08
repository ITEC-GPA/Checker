using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using GPC.Checker.Glasses.Wrappers;
using GPC.Checker.Glasses.FemModel;
using GPC.Model.Loads;
using GPC.Model.Elements;
using GPC.Model.FEM.Properties;
using GPC.Checker.Glasses.Glasses;
using GPC.Model.Glasses;
using GPC.Geometry.Meshes;
using GPC.Model.Restrains;
using GPC.Checker.Glasses.Results;

namespace GPC.Checker.Glasses.Checkers
{
    public class En16612Checker : Checker
    {


        public En16612Checker(GlassSurface glassSurface)
            : base(glassSurface)
        {
            if (_glassSurface.Prototype.Standard != Models.Prototype.Standards.EN16612)
                throw new ArgumentException($"Standard not supported for {Models.Prototype.Standards.EN16612.ToString()} {GetCheckerName()} Checker");

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

            //femWrapper.SetAnalysisType(_checkParameters.GetAnalysisType());

            
            //foreach (var wrapper in wrappers)
            //{
            //    //FemModelWrapper femWrapper = new FemModelWrapper("fem" + wrappers.IndexOf(wrapper), _checkParameters.GetAnalysisType());
            //    //if (wrapper is MonolithicGlassWrapper mgw)
            //    //{
            //    //    var geometryMesh = mgw.Mesh;
            //    //    var embeddedGeometriesMapVertex = mgw.EmbeddedGeometriesMapVertex;

            //    //    var restrains = mgw.GetRestrains();

            //    //    mgw.GetLoads(out List<Load> uniformPressureLoads, out List<Load> notUniformPressureLoads);

            //    //    var monolithicGlassProperty = new MonolithicGlassProperty(mgw.Glass);

            //    //    femWrapper.SetUpMonolithic(geometryMesh, embeddedGeometriesMapVertex, restrains, mgw.Glass, uniformPressureLoads, notUniformPressureLoads);
            //    //}
            //    //else if (wrapper is LaminatedGlassWrapper lgw)
            //    //{
            //    //    var geometryMesh = lgw.Mesh;
            //    //    var embeddedGeometriesMapVertex = lgw.EmbeddedGeometriesMapVertex;

            //    //    var restrains = lgw.GetRestrains();

            //    //    lgw.GetLoads(out List<Load> uniformPressureLoads, out List<Load> notUniformPressureLoads);

            //    //    femWrapper.SetUpLaminated(geometryMesh, embeddedGeometriesMapVertex, restrains, lgw, uniformPressureLoads, notUniformPressureLoads, CheckParameters.LaminatedAnalysisType.MultiElementPlateInterlayer);
            //    //}
            //    //else if (wrapper is InsulatedGlassWrapper igw)
            //    //{
            //    //    throw new NotImplementedException();
            //    //}
            //    //else
            //    //    throw new NotSupportedException("Glass wrapper not supported");

            //    //_femModels.Add(femWrapper);
            //}
        }

        protected override string GetCheckerName() => "EN 16612 - 2019";



        #endregion


    }
}
