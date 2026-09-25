using GPC.Model;
using GPC.Model.Standards;
using System;
using System.Runtime.Serialization;

namespace GPC.Checkers.Concrete.Checkers
{
    [Serializable]
    public abstract class Checker : ModelObjectId, ISerializable
    {
        #region Variables

        protected readonly Standard _standard;
        protected readonly Options _options;

        #endregion

        #region Properties

        public Standard Standard => _standard;

        public Options CheckerOptions => _options;

        #endregion

        #region Constructor

        public Checker(Standard standard, Options options, int id)
            : base(id)
        {
            _standard = standard ?? throw new ArgumentNullException(nameof(standard));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        protected Checker(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            _standard = (Standard)info.GetValue("Standard", typeof(Standard));
            _options = (Options)info.GetValue("Options", typeof(Options));
        }


        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Standard", _standard);
            info.AddValue("Options", _options);
        }

        #endregion

        [Serializable]
        public abstract class Options
        {

        }
    }
}
