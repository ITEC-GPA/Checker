using System;
using System.IO;
using System.Collections.Generic;
using GPC.Checker.Glasses.FemModel;
using GPC.Checker.Glasses.Wrappers;
using GPC.Checker.Glasses.Glasses;

namespace GPC.Checker.Glasses.Checkers
{
    public abstract class Checker : GPC.Checker.Common.Checker
    {
        protected GlassSurface _glassSurface;

        protected List<FemModelWrapper> _femModels;

        public Checker(GlassSurface glassSurface)
        {
            this._glassSurface = glassSurface ?? throw new ArgumentNullException(nameof(glassSurface));
            this._femModels = new List<FemModelWrapper>();
        }

        protected List<GlassWrapper> GetWrappers()
        {
            List<GlassWrapper> wrappers = new List<GlassWrapper>();


            if (_glassSurface.Prototype.Glass is Model.Elements.Glasses.MonolithicGlass mg)
            {
                MonolithicGlassWrapper mgw = new MonolithicGlassWrapper(_glassSurface);
                wrappers.Add(mgw);

            }
            else if (_glassSurface.Prototype.Glass is Model.Elements.Glasses.LaminatedGlass lg)
            {
                LaminatedGlassWrapper lgw = new LaminatedGlassWrapper(_glassSurface);
                wrappers.Add(lgw);
            }
            else if (_glassSurface.Prototype.Glass is Model.Elements.Glasses.DoubleInsulatingGlass dgu)
            {
                throw new NotImplementedException();
            }
            else if (_glassSurface.Prototype.Glass is Model.Elements.Glasses.TripleInsulatingGlass tgu)
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new NotSupportedException(_glassSurface.Prototype.Glass.GetType().ToString());
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

    }
}
