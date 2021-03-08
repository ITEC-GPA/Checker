using System;
using System.Collections.Generic;
using GPC.Model.Combinations;
using GPC.Checker.Glasses.Glasses;
using GPC.Checker.Glasses.Checkers;
using GPC.Checker.Glasses.Results;

namespace GPC.Checker.Glasses.Models
{
    public class Model 
    {
        #region Variables

        protected List<GlassSurface> _glassSurfaces;

        protected List<Combination> _combinations;


        protected string _outputFolder;


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

        public void AddSurface(GlassSurface glassSurface)
        {
            _glassSurfaces.Add(glassSurface);
        }

        public void AddCombination(Combination combination)
        {
            _combinations.Add(combination);
        } 

        public GlassResult PerformChecks()
        {
            GlassResult glassResult = null;

            foreach (var surface in _glassSurfaces)
            {
                if (surface.Prototype.Standard == Prototype.Standards.EN16612)
                {
                    En16612Checker checker = new En16612Checker(surface);
                    glassResult = checker.PerformCheck(_outputFolder);
                }
                else if (surface.Prototype.Standard == Prototype.Standards.ASTME1300)
                {
                    AstmChecker checker = new AstmChecker(surface);
                    glassResult = checker.PerformCheck(_outputFolder);
                }
                else
                {
                    throw new NotImplementedException();
                }

            }

            return glassResult;
        }

        #endregion
    }
}
