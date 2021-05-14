using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPC.Checkers.Glasses.Models
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

        #endregion


        /// <summary>
        /// If true the bricks exported to straus7 will have the bubble function option active.
        /// </summary>
        public bool Straus7BrickBubbleFunction { get; set; }


        // Explicit static constructor to tell C# compiler not to mark type as beforefieldinit
        static ModelAnalysisOptions()
        {

        }

        private ModelAnalysisOptions()
        {
            Straus7BrickBubbleFunction = false;
        }

    }

}
