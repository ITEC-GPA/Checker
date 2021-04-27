using System;
using System.Linq;
using System.Collections.Generic;
using GPC.Model.Combinations;
using GPC.Checkers.Glasses.Glasses;
using GPC.Checkers.Glasses.Checkers;
using GPC.Checkers.Glasses.Results;
using GPC.Model.Results;
using GPC.Model;

namespace GPC.Checkers.Glasses.Models
{
    public class Model
    {
        #region Variables

        protected string _outputFolder;
        protected List<GlassSurface> _glassSurfaces;
        protected UniqueNameCollection<Combination> _combinations;
        protected List<ResultPlateStress> _combinationResults;
        protected ModelOptions _options;

        #endregion

        #region Properties

        public string OutputFolder => _outputFolder;

        public IEnumerable<GlassSurface> GlassSurfaces => _glassSurfaces;

        /// <summary>
        /// List of Global combinations with unique name
        /// </summary>
        protected IEnumerable<Combination> Combinazions => _combinations;

        public ModelOptions Options => _options;

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

        public Model(List<GlassSurface> glassSurfaces, IEnumerable<Combination> combinations, string outputFolder)
        {
            _glassSurfaces = glassSurfaces ?? new List<GlassSurface>();

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

            _options = new ModelOptions();
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Add a surface to the model. Create the <see cref="Checker"/> and call <see cref="Checker.FemModelSetup(string)"/>
        /// </summary>
        public bool AddSurface(GlassSurface glassSurface)
        {
            Checkers.Checker checker;
            if (glassSurface.Prototype.Standard == Prototype.Standards.EN16612)
            {
                checker = new En16612Checker(glassSurface, MergeCombinations(_combinations, glassSurface.Prototype.Combinations), _options);
            }
            else if (glassSurface.Prototype.Standard == Prototype.Standards.ASTME1300)
            {
                checker = new AstmChecker(glassSurface, MergeCombinations(_combinations, glassSurface.Prototype.Combinations), _options);
            }
            else
            {
                return false;
            }

            if (checker.FemModelSetup(_outputFolder))
                glassSurface.Checker = checker;
            else
                return false;

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
            foreach (var surface in _glassSurfaces)
            {
                Checkers.Checker checker = null;

                if (surface.Prototype.Standard == Prototype.Standards.EN16612)
                {
                    checker = new En16612Checker(surface, MergeCombinations(_combinations, surface.Prototype.Combinations), _options);
                }
                else if (surface.Prototype.Standard == Prototype.Standards.ASTME1300)
                {
                    checker = new AstmChecker(surface, MergeCombinations(_combinations, surface.Prototype.Combinations), _options);
                }
                else
                {
                    throw new NotImplementedException();
                }

                if (checker.FemModelSetup(_outputFolder))
                {
                    surface.Checker = checker;
                }
                else
                {
                    throw new ArgumentException("Unable to create checker");
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <remarks> <see cref="RebuildAllCheckers"/> Must be called before calling this method</remarks>
        // TODO: glass, cambiare facendo in modo che se il checker non è stato creato lo crei lui, cosi da farlo andare avanti in qualsiasi caso.
        public void PerformChecks()
        {
            //foreach(var checker in _checkers)
            //    checker.PerformCheck();
            foreach (var surface in _glassSurfaces)
                surface.Checker.PerformCheck();
        }


        public List<List<ResultPlateStress>> GetPlateCombinationsResult()
        {
            List<List<ResultPlateStress>> results = new List<List<ResultPlateStress>>();

            //foreach (var checker in _checkers)
            //    results.Add(checker.GetPlateCombinationsResults());
            foreach (var surface in _glassSurfaces)
                results.Add(surface.Checker.GetPlateCombinationsResults());

            return results;
        }



        public List<List<ResultNodeDisplacement>> GetNodeDisplacementCombinationsResult()
        {
            List<List<ResultNodeDisplacement>> results = new List<List<ResultNodeDisplacement>>();

            //foreach (var checker in _checkers)
            //    results.Add(checker.GetNodeDisplacementCombinationResults());
            foreach (var surface in _glassSurfaces)
                results.Add(surface.Checker.GetNodeDisplacementCombinationResults());

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
