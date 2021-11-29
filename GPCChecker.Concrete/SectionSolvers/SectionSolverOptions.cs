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

        /// <summary>
        /// Rapresent the discretization of the axial force in the solver
        /// </summary>
        public (SectionSolver.FailureZones, int)[] FailureZonesDiscretizations { get; set; }

        /// <summary>
        /// Rapresent the discretization of the moments around the axial force axis
        /// </summary>
        public int MomentsDiscretizations { get; set; }


        public int GaussIntegrationQuadLowPoints { get; set; }
        public int GaussIntegrationQuadMidPoints { get; set; }
        public int GaussIntegrationQuadHighPoints { get; set; }
        public int GaussIntegrationTriLowPoints { get; set; }
        public int GaussIntegrationTriMidPoints { get; set; }
        public int GaussIntegrationTriHighPoints { get; set; }

        private SectionSolverOptions()
        {
            FailureZonesDiscretizations = new (SectionSolver.FailureZones, int)[] { (SectionSolver.FailureZones.F1, 1),
                                                                                    (SectionSolver.FailureZones.F2A, 1),
                                                                                    (SectionSolver.FailureZones.F2B, 1),
                                                                                    (SectionSolver.FailureZones.F3A, 30),
                                                                                    (SectionSolver.FailureZones.F3B, 2),
                                                                                    (SectionSolver.FailureZones.F4, 1),
                                                                                    (SectionSolver.FailureZones.F5, 4) };

            MomentsDiscretizations = 64;

            GaussIntegrationQuadLowPoints = 12;
            GaussIntegrationQuadMidPoints = 49;
            GaussIntegrationQuadHighPoints = 400;
            GaussIntegrationTriLowPoints = 6;
            GaussIntegrationTriMidPoints = 33;
            GaussIntegrationTriHighPoints = 79;

        }
    }    
}
