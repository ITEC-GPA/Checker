using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Standards;
using GPC.Checkers.Steel.Checkers;
using GPC.Model.LoadCases;
using GPC.Checkers.Steel.Results;
using GPC.Model;
using GPC.Model.Sections.Steel;
using GPC.Model.Sections;
using GPC.Model.Combinations;
using System.Runtime.Serialization;

namespace GPC.Checkers.Steel.Checkers
{
    /// <summary>
    /// The purpose of this class is to perform a check of a single beam between all the ILoadCases
    /// </summary>
    
    [Serializable]
    public abstract class Checker : ModelObjectId, ISerializable
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
            : this(options, standard, IDUNASSIGNED, name)
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

		protected Checker(SerializationInfo info, StreamingContext context) 
            : base(info, context)
		{
            _standard = (Standard)info.GetValue("Standard", typeof(Standard));
            _options = (Options)info.GetValue("Options", typeof(Options));
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

        #region Equals - hashcode - operators - serialization

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
            base.GetObjectData(info, context);
            info.AddValue("Standard", _standard);
            info.AddValue("Options", _options);
        }

		public override bool Equals(object obj)
		{
            if (ReferenceEquals(this, obj))
                return true;

            return obj is Checker checker &&
                base.Equals(obj) &&
                _standard.Equals(checker._standard) &&
                _options.Equals(checker._options);
		}

		public override int GetHashCode()
		{
            unchecked
            {
                int hashCode = 23;
                hashCode = hashCode * -17 + base.GetHashCode();                
                hashCode = hashCode * -17 + _standard.GetHashCode();
                hashCode = hashCode * -17 + _options.GetHashCode();
                return hashCode;
            }
        }

		#endregion

		#region Nested Class Options

		public abstract class Options
        {
            #region Constructor

            public Options()
            {
            }

            #endregion

        }

		#endregion
	}
}
