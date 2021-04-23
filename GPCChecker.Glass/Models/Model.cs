using System;
using System.Linq;
using System.Collections.Generic;
using GPC.Model.Combinations;
using GPC.Checker.Glasses.Glasses;
using GPC.Checker.Glasses.Checkers;
using GPC.Checker.Glasses.Results;
using GPC.Model.Results;

namespace GPC.Checker.Glasses.Models
{
    public class Model
    {
        #region Variables

        protected string _outputFolder;
        protected List<GlassSurface> _glassSurfaces;
        protected List<Combination> _combinations;
        protected List<ResultPlateStress> _combinationResults;
        protected List<Checkers.Checker> _checkers;

        #endregion

        #region Properties

        public string OutputFolder => _outputFolder;

        public IEnumerable<GlassSurface> GlassSurfaces => _glassSurfaces;

        /// <summary>
        /// List of Global combinations
        /// </summary>
        public IEnumerable<Combination> Combinations => _combinations;


        #endregion


        #region Public constructors

        public Model(string outputFolder)
            : this(new List<GlassSurface>(), new List<Combination>(), outputFolder)
        {

        }

        public Model(List<GlassSurface> glassSurfaces, string outputFolder)
            : this(glassSurfaces, new List<Combination>(), outputFolder)
        {

        }

        public Model(List<GlassSurface> glassSurfaces, List<Combination> combinations, string outputFolder)
        {
            _glassSurfaces = glassSurfaces ?? new List<GlassSurface>();
            _combinations = combinations ?? new List<Combination>();
            _checkers = new List<Checkers.Checker>();

            if (!string.IsNullOrEmpty(outputFolder) && !string.IsNullOrWhiteSpace(outputFolder))
                if (!System.IO.Directory.Exists(outputFolder))
                    throw new ArgumentException("Output folder does not exist");
                else
                    _outputFolder = outputFolder;
            else
                throw new ArgumentNullException("Output folder cannot be null or empty");
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Add a surface to the model
        /// </summary>
        public bool AddSurface(GlassSurface glassSurface)
        {
            _glassSurfaces.Add(glassSurface);
            return true;
        }

        public bool RemoveSurface(GlassSurface glassSurface)
        {
            return _glassSurfaces.Remove(glassSurface);
        }


        /// <summary>
        /// Add a combination to the model
        /// </summary>
        public bool AddCombination(Combination combination)
        {
            try
            {
                _combinations.Add(combination);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        
        /// <summary>
        /// SetUp the FemModel of each surface, 
        /// </summary>
        public void FemModelSetup()
        {
            foreach (var surface in _glassSurfaces)
            {
                Checkers.Checker checker = null;
                
                if (surface.Prototype.Standard == Prototype.Standards.EN16612)
                {
                    checker = new En16612Checker(surface, _combinations);
                }
                else if (surface.Prototype.Standard == Prototype.Standards.ASTME1300)
                {
                    checker = new AstmChecker(surface, _combinations);
                }
                else
                {
                    throw new NotImplementedException();
                }


                if (checker.FemModelSetup(_outputFolder))
                    _checkers.Add(checker);
                else
                {
                    throw new ArgumentException("Unable to create checker");
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <remarks> <see cref="FemModelSetup"/> Must be called before calling this method</remarks>
        // TODO: glass, cambiare facendo in modo che se il checker non è stato creato lo crei lui, cosi da farlo andare avanti in qualsiasi caso.
        public void PerformChecks()
        {

            foreach(var checker in _checkers)
            {
                checker.PerformCheck();
            }

        }


        public List<List<ResultPlateStress>> GetPlateCombinationsResult()
        {
            List<List<ResultPlateStress>> results = new List<List<ResultPlateStress>>();

            foreach (var checker in _checkers)
            {
                results.Add(checker.GetPlateCombinationsResults());
            }

            return results;
        }



        public List<List<ResultNodeDisplacement>> GetNodeDisplacementCombinationsResult()
        {
            List<List<ResultNodeDisplacement>> results = new List<List<ResultNodeDisplacement>>();

            foreach (var checker in _checkers)
            {
                results.Add(checker.GetNodeDisplacementCombinationResults());
            }

            return results;
        }


        public void GetWorkingRatio()
        {
            foreach (var checker in _checkers)
            {
                checker.GetWorkinRatio();
            }
        }


        #endregion
    }
}
