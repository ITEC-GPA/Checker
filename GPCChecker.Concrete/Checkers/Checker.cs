using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using GPC.Checkers.Concrete.Results;
using GPC.Model.Combinations;
using GPC.Model.LoadCases;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;

namespace GPC.Checkers.Concrete.Checkers
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
        protected CheckerStationResult[] _checkerStationResult;
        protected readonly CheckerAttribute _checkerAttributes;

        protected List<string> _errorLog;

        #endregion


        #region Properties

        public Standard Standard => _standard;

        public Options CheckerOptions => _options;

        public CheckerStationResult[] StationResults => _checkerStationResult;

        public CheckerAttribute CheckerAttribute => _checkerAttributes;

        #endregion


        #region Constructor

        public Checker(CheckerAttribute checkerAttribute, Options options, Standard standard, string name = "")
            : this(checkerAttribute, options, standard, Model.ModelObjectId.IDUNASSIGNED, name)
        {
            _errorLog = new List<string>();
        }

        public Checker(CheckerAttribute checkerAttribute, Options options, Standard standard, int id, string name = "")
            : base(id, name)
        {
            _checkerAttributes = checkerAttribute ?? throw new ArgumentNullException(nameof(checkerAttribute));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _standard = standard ?? throw new ArgumentNullException(nameof(standard));
            _errorLog = new List<string>();
        }

        public Checker(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _standard = (Standard)info.GetValue("Standard", typeof(Standard));
            _options = (Options)info.GetValue("CheckerOptions", typeof(Options));
            _checkerStationResult = (CheckerStationResult[])info.GetValue("CheckerStationResult", typeof(CheckerStationResult[]));
            _checkerAttributes = (CheckerAttribute)info.GetValue("CheckerAttribute", typeof(CheckerAttribute[]));
        }


        #endregion


        #region Public abstract method

        public abstract void PerformCheck();

        public abstract void ULSPerformCheck();

        public abstract void SLSPerformCheck();

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
            info.AddValue("CheckerStationResult", _checkerStationResult, typeof(CheckerStationResult[]));
            info.AddValue("CheckerAttribute", _checkerAttributes, typeof(CheckerAttribute));
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
