using GPC.Checker.Glasses.Wrappers;
using GPC.Model.Elements.Glasses;
using System;
using System.IO;
using GPC.Checker.Glasses.FemModel;
using System.Collections.Generic;

namespace GPC.Checker.Glasses.Checkers
{
    public abstract class GlassChecker : GPC.Checker.Common.Checker
    {
        protected Model _model;

        protected CheckParameters _checkParameters;

        protected List<FemModelWrapper> _femModels;

        public GlassChecker(Model model, CheckParameters checkParameters)
        {
            this._model = model ?? throw new ArgumentNullException(nameof(model));
            this._checkParameters = checkParameters;
            this._femModels = new List<FemModelWrapper>();
        }

        protected List<GlassWrapper> GetWrappers()
        {
            List<GlassWrapper> wrappers = new List<GlassWrapper>();

            foreach (var surface in _model.GlassSurfaces)
            {
                if (surface.Glass is MonolithicGlass mg)
                {
                    MonolithicGlassWrapper mgw = new MonolithicGlassWrapper(surface);

                    mgw.AddLoads(surface.Loads);

                    wrappers.Add(mgw);
                }
                else if (surface.Glass is LaminatedGlass lg)
                {
                    LaminatedGlassWrapper lgw = new LaminatedGlassWrapper(surface);

                    lgw.AddLoads(surface.Loads);

                    wrappers.Add(lgw);
                }
                else
                {
                    throw new NotSupportedException("Glass property type unknow");
                }
            }
            return wrappers;
        }


        #region abstract methods
        
        protected abstract override string GetCheckerName();

        public abstract void SetUpFemModels();

        public void ExportToSt7()
        {
            foreach(FemModelWrapper femModel in _femModels)
            {
                femModel.SaveToSt7(Path.Combine(_model.OutputFolder, Path.ChangeExtension(femModel.Name, "st7")));
            }
        }

        public void RunSt7Solver()
        {
            foreach (FemModelWrapper femModel in _femModels)
            {
                femModel.RunSt7Solver(Path.Combine(_model.OutputFolder, Path.ChangeExtension(femModel.Name, "st7")));
            }
        }

        #endregion



        public class CheckParameters
        {
            public enum LaminatedAnalysisType
            {
                /// <summary> Single plate with an equivalent thickness</summary>
                EquivalentThickness,
                /// <summary> Single plate for each monolithic connected by connection/links to interlayer modelled as brick</summary>
                MultiElementBrickIntelayer,
                /// <summary> Single plate for each monolithic connected by connection/links to interlayer modelled as plate</summary>
                MultiElementPlateInterlayer,
                /// <summary> Single plate that takes into account the interlayer slip </summary>
                MultiLayer,
            }

            public enum AnalysisType
            {
                LinearStaticAnalisys,
                NonLinearStaticAnalysis,
            }

            public enum CheckMethod
            {
                /// <summary> Ref prEn16612 annex A </summary>
                DominantLoad = 0,
                /// <summary> Ref prEn16612 annex A </summary>
                ShorterLoad = 1,
                /// <summary> Ref CNR-DT 210/2013 pag 224  </summary>
                PalmgrenMiner = 2,
                /// <summary> Ref. ASTM E1300-16 §X5 </summary>
                ASTME1300 = 3
            }

            public enum LaminatedEqThicknessMethod
            {
                /// <summary> Ref CNR DT 210-13 </summary>
                EET = 0,
                /// <summary> Ref prEn16612 </summary>
                Omega = 1,
                /// <summary> Ref ASTM E1300-16 §X9 </summary>
                ASTME1300 = 2,
                /// <summary> Ref NEN 2608:2014 §F </summary>
                NEN = 3,
            }

            private LaminatedAnalysisType _laminatedAnalysisType;
            private AnalysisType _analysisType;
            private CheckMethod _checkMethod;
            private LaminatedEqThicknessMethod _laminatedEqThicknessMethod;

            public CheckParameters()
            {
                _laminatedAnalysisType = LaminatedAnalysisType.MultiElementPlateInterlayer;
                _analysisType = AnalysisType.LinearStaticAnalisys;
                _checkMethod = CheckMethod.PalmgrenMiner;
                _laminatedEqThicknessMethod = LaminatedEqThicknessMethod.EET;
            }

            public CheckParameters(LaminatedAnalysisType laminatedAnalysisType, AnalysisType analysisType, CheckMethod checkMethod, LaminatedEqThicknessMethod laminatedEqThicknessMethod)
            {
                _laminatedAnalysisType = laminatedAnalysisType;
                _analysisType = analysisType;
                _checkMethod = checkMethod;
                _laminatedEqThicknessMethod = laminatedEqThicknessMethod;
            }

            public LaminatedAnalysisType GetLaminatedAnalysisType() => _laminatedAnalysisType;
            public AnalysisType GetAnalysisType() => _analysisType;
            public CheckMethod GetCheckMethod() => _checkMethod;
            public LaminatedEqThicknessMethod GetLaminatedEqThicknessMethod() => _laminatedEqThicknessMethod;

            public void SetLaminatedAnalysisType(LaminatedAnalysisType value)
            {
                _laminatedAnalysisType = value;
            }

            public void SetAnalysisType(AnalysisType value)
            {
                _analysisType = value;
            }

            public void SetCheckMethod(CheckMethod value)
            {
                _checkMethod = value;
            }

            public void SetLaminatedEqThicknessMethod(LaminatedEqThicknessMethod value)
            {
                _laminatedEqThicknessMethod = value;
            }

        }
    }
}
