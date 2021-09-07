using GPC.Checkers.Glasses.Checkers;
using GPC.Checkers.Glasses.Glasses;
using GPC.Model;
using GPC.Model.Combinations;
using GPC.Model.Results;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GPC.Checkers.Glasses.Models
{
    public class Model
    {
        #region Variables

        protected string _outputFolder;
        protected UniqueIdCollection<GlassSurface> _glassSurfaces;
        protected UniqueNameCollection<Combination> _combinations;

        #endregion

        #region Properties

        public string OutputFolder => _outputFolder;

        public IEnumerable<GlassSurface> GlassSurfaces => _glassSurfaces;

        public IEnumerable<Combination> Combinations => _combinations;

        #endregion

        #region Public constructors

        public Model(string outputFolder)
            : this(new UniqueIdCollection<GlassSurface>(), new List<Combination>(), outputFolder)
        {
        }

        public Model(UniqueIdCollection<GlassSurface> glassSurfaces, string outputFolder)
            : this(glassSurfaces, new List<Combination>(), outputFolder)
        {
        }

        public Model(UniqueIdCollection<GlassSurface> glassSurfaces, IEnumerable<Combination> combinations, string outputFolder)
        {
            _glassSurfaces = glassSurfaces ?? new UniqueIdCollection<GlassSurface>();

            _combinations = new UniqueNameCollection<Combination>();

            if (!_combinations.AddRange(combinations))
                throw new ArgumentException("Duplicate names in combinations collection");


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

        internal bool AddSurface(GlassSurface glassSurface)
        {
            Checker checker;
            if (glassSurface.Prototype.Standard == Prototype.Standards.EN16612)
            {
                checker = new EN16612Checker(glassSurface, MergeCombinations(_combinations, glassSurface.Prototype.Combinations), glassSurface.Prototype.ModelOptions);
            }
            else if (glassSurface.Prototype.Standard == Prototype.Standards.ASTME1300)
            {
                checker = new AstmChecker(glassSurface, MergeCombinations(_combinations, glassSurface.Prototype.Combinations), glassSurface.Prototype.ModelOptions);
            }
            else
            {
                return false;
            }

            glassSurface.Checker = checker;

            _glassSurfaces.Add(glassSurface);

            return true;
        }


        public bool RemoveSurface(GlassSurface glassSurface)
        {
            return _glassSurfaces.Remove(glassSurface);
        }

        public bool FemModelsSetup(string fileNamePrefix = "")
        {
            bool ret = false;

            foreach (var glassSurface in _glassSurfaces)
            {
                ret = glassSurface.FemModelSetup(_outputFolder, fileNamePrefix);
                if (!ret)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Add a combination to the model
        /// </summary>
        public bool AddCombination(Combination combination)
        {
            try
            {
                // Validazione combo
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
        public void RebuildAllCheckers()
        {
            foreach (var glassSurface in _glassSurfaces)
            {
                Checker checker = null;

                if (glassSurface.Prototype.Standard == Prototype.Standards.EN16612)
                {
                    checker = new EN16612Checker(glassSurface, MergeCombinations(_combinations, glassSurface.Prototype.Combinations), glassSurface.Prototype.ModelOptions);
                }
                else if (glassSurface.Prototype.Standard == Prototype.Standards.ASTME1300)
                {
                    checker = new AstmChecker(glassSurface, MergeCombinations(_combinations, glassSurface.Prototype.Combinations), glassSurface.Prototype.ModelOptions);
                }
                else
                {
                    throw new NotImplementedException();
                }

                if (checker.FemModelSetup(_outputFolder))
                {
                    glassSurface.Checker = checker;
                }
                else
                {
                    throw new ArgumentException("Unable to create checker");
                }
            }
        }


        /// <summary>
        /// Run the <see cref="Checker.PerformCheck()"/> that run the femModels e fill the results
        /// </summary>
        /// <remarks> <see cref="RebuildAllCheckers"/> Must be called before calling this method</remarks>
        public void PerformChecks()
        {
            using (var gse = _glassSurfaces.GetEnumerator())
            {
                while (gse.MoveNext())
                {
                    gse.Current.Checker.PerformCheck();
                }
            }

        }


        
        public List<List<ResultStress>> GetPlateCombinationsResult()
        {
            List<List<ResultStress>> results = new List<List<ResultStress>>();

            //foreach (var checker in _checkers)
            //    results.Add(checker.GetPlateCombinationsResults());
            //foreach (var surface in _glassSurfaces)
            //    results.Add(surface.Checker.GetPlateCombinationsResults());

            return results;
        }



        public List<List<ResultDisplacement>> GetNodeDisplacementCombinationsResult()
        {
            List<List<ResultDisplacement>> results = new List<List<ResultDisplacement>>();

            //foreach (var checker in _checkers)
            //    results.Add(checker.GetNodeDisplacementCombinationResults());
            //foreach (var surface in _glassSurfaces)
            //    results.Add(surface.Checker.GetNodeDisplacementCombinationResults());

            return results;
        }


        public void GetWorkingRatio()
        {
            //foreach (var checker in _checkers)
            //    checker.GetWorkinRatio();
            foreach (var surface in _glassSurfaces)
                surface.Checker.GetWorkinRatio();
        }


        #endregion

        #region Private methods

        /// <summary>
        /// This method merge the two list of combinations.
        /// <para>A <see cref="Combination"/> from <paramref name="specificCombinations"/> will be added only if it does not have the same coefficients/loadcase of a <paramref name="globalCombinations"/></para>
        /// </summary>
        /// <param name="globalCombinations"></param>
        /// <param name="specificCombinations"></param>
        /// <returns></returns>
        /// <remarks>The <see cref="Combination"/> will be cloned </remarks>
        private List<Combination> MergeCombinations(IEnumerable<Combination> globalCombinations, IEnumerable<Combination> specificCombinations)
        {
            var merge = new UniqueNameCollection<Combination>();

            merge.AddRange(globalCombinations.Select(i => (Combination)i.Clone()));

            foreach (var combo in specificCombinations)
            {
                var tuples = combo.GetLoadCaseCoefficientsTuple();

                if (globalCombinations.Where(i => i.ContainsLoadCaseCoefficients(tuples)).Count() > 0)
                {
                    // Se vero esiste una combinazione in globalCombinations con stessi coefficienti e loadcase di combo
                    // Non aggiungo a merge
                    continue;
                }
                else
                {
                    merge.Add(combo.Duplicate($"P{combo.Name}"));
                }
            }

            return merge.ToList();
        }

        #endregion
    }
}
