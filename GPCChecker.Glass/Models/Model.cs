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

        /// <summary>
        /// List of Global combinations
        /// </summary>
        protected List<Combination> _combinations;

        protected List<ResultPlateStress> _combinationResults;

        protected List<Checkers.Checker> _checkers;

        #endregion

        #region Properties

        public List<GlassSurface> GlassSurfaces => _glassSurfaces;

        public string OutputFolder => _outputFolder;

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
            this._glassSurfaces = glassSurfaces ?? new List<GlassSurface>();
            this._combinations = combinations ?? new List<Combination>();
            this._checkers = new List<Checkers.Checker>();

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
        public void AddSurface(GlassSurface glassSurface)
        {
            _glassSurfaces.Add(glassSurface);
        }


        /// <summary>
        /// Add a combination to the model
        /// </summary>
        public void AddCombination(Combination combination)
        {
            _combinations.Add(combination);
        }

        
        /// <summary>
        /// SetUp the FemModel of each surface, 
        /// </summary>
        public void FemModelSetup()
        {
            foreach (var surface in _glassSurfaces)
            {
                if (surface.Prototype.Standard == Prototype.Standards.EN16612)
                {
                    _checkers.Add(new En16612Checker(surface, _combinations));
                }
                else if (surface.Prototype.Standard == Prototype.Standards.ASTME1300)
                {
                    _checkers.Add(new AstmChecker(surface, _combinations));
                }
                else
                {
                    throw new NotImplementedException();
                }

                _checkers.Last().FemModelSetup(_outputFolder);
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
