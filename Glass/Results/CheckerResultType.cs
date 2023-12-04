using GPC.Model;
using GPC.Model.Sections.Glass;
using GPC.Model.Standards;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace GPC.Checkers.GlassV2.Results
{
	[Serializable]
	public abstract class CheckerResultType : ModelObjectId, ISerializable
	{
		#region Variables

		protected readonly IGlassPanel _section;
		protected readonly Standard _standard;

		#endregion

		#region Properties

		public IGlassPanel ConcreteSection => _section;

		/// <summary>
		/// <inheritdoc cref="_standard"/>
		/// </summary>
		public Standard Standard => _standard;

		#endregion

		#region Constructor

		public CheckerResultType(IGlassPanel section, Standard standard, int id = IDUNASSIGNED)
			: base(id)
		{
			_section = section ?? throw new ArgumentNullException(nameof(section));
			_standard = standard ?? throw new ArgumentNullException(nameof(standard));
		}

		protected CheckerResultType(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			_section = (IGlassPanel)info.GetValue("Section", typeof(IGlassPanel));
			_standard = (Standard)info.GetValue("Standard", typeof(Standard));
		}

		#endregion

		#region Equals, hashcode, operators

		public override bool Equals(object obj)
		{
			return obj is CheckerResultType type &&
				   base.Equals(obj) &&
				   EqualityComparer<IGlassPanel>.Default.Equals(_section, type._section);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = -23;
				hashCode = hashCode * -17 + base.GetHashCode();
				hashCode = hashCode * -17 + _section.GetHashCode();
				hashCode = hashCode * -17 + _standard.GetHashCode();
				return hashCode;
			}
		}

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("Section", _section);
			info.AddValue("Standard", _standard);
		}

		#endregion
	}
}