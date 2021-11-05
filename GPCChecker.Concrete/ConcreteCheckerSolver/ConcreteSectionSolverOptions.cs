using GPC.Geometry;

namespace GPC.Checkers.Concrete.ConcreteCheckerSolver
{
    // https://csharpindepth.com/articles/singleton

    /// <summary>
    /// This is a singleton class that collects options related to the concrete section solver
    /// </summary>
    public sealed class ConcreteSectionSolverOptions
    {
        #region Singleton setup

        private static readonly ConcreteSectionSolverOptions instance = new ConcreteSectionSolverOptions();

        public static ConcreteSectionSolverOptions Instance
        {
            get
            {
                return instance;
            }
        }

        static ConcreteSectionSolverOptions()
        {

        }

        #endregion

        /// <summary>
        /// Rapresent the discretization of the axial force in the solver
        /// </summary>
        public (ConcreteSectionSolverULS.FailureIndices, int)[] AxialForceDiscretizations { get; set; }

        /// <summary>
        /// Rapresent the discretization of the moments around the axial force axis
        /// </summary>
        public int MomentsDiscretizations { get; set; }

        public double SLSconvergenceTolerance { get; set; }

        public Point3d DistanceFromCentroid { get; set; }

        public int GaussIntegrationQuadLowPoints { get; set; }
        public int GaussIntegrationQuadMidPoints { get; set; }
        public int GaussIntegrationQuadHighPoints { get; set; }
        public int GaussIntegrationTriLowPoints { get; set; }
        public int GaussIntegrationTriMidPoints { get; set; }
        public int GaussIntegrationTriHighPoints { get; set; }

        private ConcreteSectionSolverOptions()
        {
            AxialForceDiscretizations = new (ConcreteSectionSolverULS.FailureIndices, int)[] { (ConcreteSectionSolverULS.FailureIndices.Iz1, 1),
                                                                                               (ConcreteSectionSolverULS.FailureIndices.Iz2, 1),
                                                                                               (ConcreteSectionSolverULS.FailureIndices.Iz3, 1),
                                                                                               (ConcreteSectionSolverULS.FailureIndices.Iz4, 30),
                                                                                               (ConcreteSectionSolverULS.FailureIndices.Iz5, 2),
                                                                                               (ConcreteSectionSolverULS.FailureIndices.Iz6, 1),
                                                                                               (ConcreteSectionSolverULS.FailureIndices.Iz7, 4) };

            MomentsDiscretizations = 32;

            SLSconvergenceTolerance = 1e-5;

            DistanceFromCentroid = new Point3d(0, 0, 0);

            GaussIntegrationQuadLowPoints = 12;
            GaussIntegrationQuadMidPoints = 49;
            GaussIntegrationQuadHighPoints = 121;
            GaussIntegrationTriLowPoints = 6;
            GaussIntegrationTriMidPoints = 33;
            GaussIntegrationTriHighPoints = 79;

        }
    }    
}
