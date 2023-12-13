using GPC.Checkers.GlassV2.Attributes;
using GPC.Model.Standards;
using System;
using System.Runtime.Serialization;

namespace GPC.Checkers.GlassV2.Checkers
{
	public class AstmChecker : GlassChecker
	{
		public AstmChecker(GlassCheckerAttribute checkerAttribute, Standard standard, AstmCheckerOptions options, int id = IDUNASSIGNED)
			: base(checkerAttribute, standard, options, id)
		{
		}

		protected AstmChecker(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{

		}



		#region Nested class

		[Serializable]
		public class AstmCheckerOptions : GlassCheckerOptions, ISerializable
		{
			public AstmCheckerOptions()
			{

			}

			protected AstmCheckerOptions(SerializationInfo info, StreamingContext context)
			{

			}

			public override bool Equals(object obj)
			{
				return obj is AstmCheckerOptions options;
			}

			public override int GetHashCode()
			{
				unchecked
				{
					int hashCode = -17;
					hashCode = hashCode * base.GetHashCode();
					return hashCode;
				}
			}

			public override void GetObjectData(SerializationInfo info, StreamingContext context)
			{
			}

			public static bool operator ==(AstmCheckerOptions left, AstmCheckerOptions right)
			{
				return left.Equals(right);
			}

			public static bool operator !=(AstmCheckerOptions left, AstmCheckerOptions right)
			{
				return !(left == right);
			}
		}

		#endregion
	}
}
