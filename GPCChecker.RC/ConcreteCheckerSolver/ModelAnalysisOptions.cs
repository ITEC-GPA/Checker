using GPC.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checkers.ReinforcedConcrete.ConcreteCheckerSolver
{
    // https://csharpindepth.com/articles/singleton

    /// <summary>
    /// This is a singleton class that collects options related to the fem model 
    /// </summary>
    public sealed class ModelAnalysisOptions
    {

        #region Singleton setup

        private static readonly ModelAnalysisOptions instance = new ModelAnalysisOptions();

        public static ModelAnalysisOptions Instance
        {
            get
            {
                return instance;
            }
        }

        // Explicit static constructor to tell C# compiler not to mark type as beforefieldinit.
        // NON TOCCARE PER NESSUN MOTIVO
        static ModelAnalysisOptions()
        {

        }

        #endregion


        private ModelAnalysisOptions()
        {

        }
    }

}
