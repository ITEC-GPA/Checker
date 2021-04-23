using System;
using System.Linq;
using System.Collections.Generic;
using GPC.Model.Combinations;
using GPC.Checker.Glasses.Glasses;
using GPC.Checker.Glasses.Checkers;
using GPC.Checker.Glasses.Results;
using GPC.Model.Results;
using GPC.Model;

namespace GPC.Checker.Glasses.Models
{
    public class Model
    {
        #region Variables

        protected string _outputFolder;

        protected List<GlassSurface> _glassSurfaces;

        /// <summary>
        /// List of Global combinations with unique name
        /// </summary>
        protected UniqueNameCollection<Combination> _combinations;

        protected List<ResultPlateStress> _combinationResults;

        protected List<Checkers.Checker> _checkers;

        protected ModelOptions _options;

        #endregion

        #region Properties

        public List<GlassSurface> GlassSurfaces => _glassSurfaces;

        public string OutputFolder => _outputFolder;

        public ModelOptions Options => _options;

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

        public Model(List<GlassSurface> glassSurfaces, IEnumerable<Combination> combinations, string outputFolder)
        {
            this._glassSurfaces = glassSurfaces ?? new List<GlassSurface>();
            this._checkers = new List<Checkers.Checker>();

            this._combinations = new UniqueNameCollection<Combination>();
            _combinations.AddRange(combinations);

            if (!string.IsNullOrEmpty(outputFolder) && !string.IsNullOrWhiteSpace(outputFolder))
                if (!System.IO.Directory.Exists(outputFolder))
                    throw new ArgumentException("Output folder does not exist");
                else
                    _outputFolder = outputFolder;
            else
                throw new ArgumentNullException("Output folder cannot be null or empty");

            _options = new ModelOptions();
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
            // Validazione combo
            _combinations.Add(combination);
        }

        
        /// <summary>
        /// SetUp the FemModel of each surface, 
        /// </summary>
        public void FemModelSetup()
        {

            List<Combination> totalCombinations = new List<Combination>();
            totalCombinations.AddRange(_combinations);

            foreach (var surface in _glassSurfaces)
            {
                totalCombinations.AddRange(surface.Prototype.Combinations);


                Checkers.Checker checker = null;
                
                if (surface.Prototype.Standard == Prototype.Standards.EN16612)
                {
                    checker = new En16612Checker(surface, totalCombinations, _options);
                }
                else if (surface.Prototype.Standard == Prototype.Standards.ASTME1300)
                {
                    checker = new AstmChecker(surface, totalCombinations, _options);
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
