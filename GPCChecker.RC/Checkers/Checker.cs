using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Standards;
using GPC.Model.LoadCases;
using GPC.Model.Sections.Steel;
using GPC.Model.Sections;
using GPC.Model.Combinations;
using System.Runtime.Serialization;
using GPC.Checkers.ReinforcedConcrete.Results;

namespace GPC.Checkers.ReinforcedConcrete.Checkers
{
    /// <summary>
    /// The purpose of this class is to perform a check of a single beam between all the ILoadCases
    /// </summary>

    [Serializable]
    public abstract class Checker : Model.ModelObjectId, ISerializable
    {
        #region Variables

        protected readonly Standard _standard;
        protected readonly Options _options;

        protected List<string> _errorLog;

        #endregion


        #region Properties

        public Standard Standard => _standard;

        public Options CheckerOptions => _options;

        #endregion


        #region Constructor

        public Checker(Options options, Standard standard, string name = "")
            : this(options, standard, Model.ModelObjectId.IDUNASSIGNED, name)
        {
            _errorLog = new List<string>();
        }

        public Checker(Options options, Standard standard, int id, string name = "")
            : base(id, name)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _options = options;

            _standard = standard ?? throw new ArgumentNullException(nameof(standard));
            _errorLog = new List<string>();
        }

        public Checker(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _standard = (Standard)info.GetValue("Standard", typeof(Standard));
            _options = (Options)info.GetValue("CheckerOptions", typeof(Options));
        }


        #endregion


        #region Public abstract method

        public abstract void PerformCheck();

        #endregion

        public List<string> GetErrorLog()
        {
            return _errorLog;
        }  

        protected double GetWorkingRatio(double force, double capacity)
        {
            double result = Math.Abs(force / capacity);

            if (Math.Abs(capacity) < 0.01)
                throw new ArgumentException("Capacity can not be null");

            if (Math.Abs(force) < 0.001)
                return 0.001;
            if (result < 0.001)
                return 0.001;

            return result;
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Standard", _standard, typeof(Standard));
            info.AddValue("CheckerOptions", _options, typeof(Options));
        }

        public override bool Equals(object obj)
        {
            // TODO: implementare 
            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            // TODO: implementare 
            throw new NotImplementedException();
        }

        public abstract class Options
        {

            public Options()
            {
                
            }

        }
    }
}
