using System;
using System.IO;
using System.Collections.Generic;
using GPC.Checker.Glasses.FemModel;
using GPC.Checker.Glasses.Wrappers;
using GPC.Checker.Glasses.Glasses;
using GPC.Model.Glasses;
using GPC.Geometry.Meshes;
using GPC.Checker.Glasses.Results;

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

        protected GlassWrapper GetWrapper()
        {
            var glass = _glassSurface.Prototype.Glass;

            if (glass is MonolithicGlass mg)
            {
                return new MonolithicGlassWrapper(_glassSurface, mg);
            }
            else if (glass is LaminatedGlass lg)
            {
                return new LaminatedGlassWrapper(_glassSurface, lg);
            }
            else if (glass is DoubleInsulatingGlass dgu)
            {
                return new DoubleInsulatingGlassWrapper(_glassSurface, dgu);
            }
            else if (glass is TripleInsulatingGlass tgu)
            {
                return new TripleInsulatingGlassWrapper(_glassSurface, tgu);
            }
            else
            {
                throw new NotSupportedException();
            }
        }


        public abstract GlassResult PerformCheck(string folderPath);
        
        protected abstract override string GetCheckerName();

        public abstract void SetUpFemModels();

        public void RunSt7Solver()
        {
            //foreach (FemModelWrapper femModel in _femModels)
            //{
            //    femModel.RunSt7Solver(Path.Combine(_model.OutputFolder, Path.ChangeExtension(femModel.Name, "st7")));
            //}
        }


    }
}
