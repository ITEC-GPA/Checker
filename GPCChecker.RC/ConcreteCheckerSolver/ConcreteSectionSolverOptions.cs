namespace GPC.Checkers.ReinforcedConcrete.ConcreteCheckerSolver
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
        public int[] AxialForceDiscretizations { get; set; }

        /// <summary>
        /// Rapresent the discretization of the moments around the axial force axis
        /// </summary>
        public int MomentsDiscretizations { get; set; }

        private ConcreteSectionSolverOptions()
        {
            AxialForceDiscretizations = new int[]{ 1, 1, 1, 30, 2, 1, 4 };
            MomentsDiscretizations = 32;
        }
    }    
}
