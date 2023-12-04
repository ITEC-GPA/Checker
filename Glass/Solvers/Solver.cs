using GPC.Model;
using GPC.Model.Sections.Glass;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GPC.Checkers.GlassV2.Solvers
{
	[Serializable]
	public abstract class Solver : ModelObjectId, ISerializable
	{
		#region Constant 

		// Units conversions
		public static readonly double FROM_N_TO_KN = 0.001;
		public static readonly double FROM_KN_TO_N = 1000;

		public static readonly double FROM_NM_TO_KNM = 0.000001;
		public static readonly double FROM_KNM_TO_NM = 1000000;

		#endregion

		#region Variables

		protected IGlassPanel _concreteSection;
		protected Standard _standard;
		protected List<string> _log;

		#endregion

		public IGlassPanel ConcreteSection => _concreteSection;

		#region Constructor

		internal Solver(IGlassPanel section, Standard standard, int id)
			: base(id)
		{
			_concreteSection = section ?? throw new ArgumentNullException(nameof(section));
			_standard = standard ?? throw new ArgumentNullException(nameof(standard));
			_log = new List<string>();
		}

		protected Solver(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			_concreteSection = (IGlassPanel)info.GetValue("GlassPanel", typeof(IGlassPanel));
			_standard = (Standard)info.GetValue("Standard", typeof(Standard));
			_log = (List<string>)info.GetValue("Log", typeof(List<string>));
		}

		#endregion

		#region Equals - hashcode - operators - serialization

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj))
				return true;

			return obj is Solver solver && _concreteSection.Equals(solver._concreteSection);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = 23;
				hashCode = hashCode * -17 + base.GetHashCode();
				hashCode = hashCode * -17 + EqualityComparer<IGlassPanel>.Default.GetHashCode(_concreteSection);
				hashCode = hashCode * -17 + _standard.GetHashCode();
				return hashCode;
			}
		}

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("GlassPanel", _concreteSection);
			info.AddValue("Standard", _standard);
			info.AddValue("Log", _log);
		}

		public List<string> GetLog()
		{
			return _log;
		}

		#endregion
	}
}
