using GPC.Checker.Glasses.Wrappers;
using GPC.Model.Elements.Glasses;
using GPC.Model.Loads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checker.Glasses.Checkers
{
    public abstract class GlassChecker : GPC.Checker.Common.Checker
    {
        public enum LaminatedAnalysisType
        {
            EquivalentThickness,
            MultiElement,
            MultiLayer,
        }

        protected Model _model;

        protected LaminatedAnalysisType _laminatedAnalysisType;

        public GlassChecker(Model model, LaminatedAnalysisType laminatedAnalysisType)
        {
            this._model = model ?? throw new ArgumentNullException(nameof(model));
            this._laminatedAnalysisType = laminatedAnalysisType;
        }

        protected List<GlassWrapper> GetWrappers()
        {
            List<GlassWrapper> wrappers = new List<GlassWrapper>();

            foreach (var surface in _model.GlassSurfaces)
            {
                if (surface.GlassProperty is MonolithicGlass mg)
                {
                    MonolithicGlassWrapper mgw = new MonolithicGlassWrapper(surface);

                    mgw.AddLoads(surface.Loads);

                    mgw.GeneratePlateMesh();

                    
                    wrappers.Add(mgw);
                }
                else
                {
                    throw new NotSupportedException("Glass property type unknow");
                }
            }
            return wrappers;
        }


        protected abstract override string GetCheckerName();

        public abstract void Run();



    }
}
