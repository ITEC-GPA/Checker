using GPC.Geometry;

namespace GPC.Checkers.Concrete.SectionSolvers
{
    // https://csharpindepth.com/articles/singleton

    /// <summary>
    /// This is a singleton class that collects options related to the concrete section solver
    /// </summary>
    public sealed class SectionSolverOptions
    {
        #region Singleton setup

        private static readonly SectionSolverOptions instance = new SectionSolverOptions();

        public static SectionSolverOptions Instance
        {
            get
            {
                return instance;
            }
        }

        static SectionSolverOptions()
        {

        }

        #endregion



        public int GaussIntegrationQuadLowPoints { get; set; }
        public int GaussIntegrationQuadMidPoints { get; set; }
        public int GaussIntegrationQuadHighPoints { get; set; }
        public int GaussIntegrationTriLowPoints { get; set; }
        public int GaussIntegrationTriMidPoints { get; set; }
        public int GaussIntegrationTriHighPoints { get; set; }

        private SectionSolverOptions()
        {
            GaussIntegrationQuadLowPoints = 12;
            GaussIntegrationQuadMidPoints = 49;
            GaussIntegrationQuadHighPoints = 400;
            GaussIntegrationTriLowPoints = 6;
            GaussIntegrationTriMidPoints = 33;
            GaussIntegrationTriHighPoints = 79;

        }
    }    
}
