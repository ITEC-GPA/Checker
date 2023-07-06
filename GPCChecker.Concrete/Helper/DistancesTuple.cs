namespace GPC.Checker.Helper
{
    public struct DistancesTuple
    {
        public double teta;
        // Rebars
        public int dMinRebarId;
        public double dminRebar;
        public int dMaxRebarId;
        public double dmaxRebar;
        // Concrete
        public int dMinVertexIndex;
        public double dminConcrete;
        public int dMaxVertexIndex;
        public double dmaxConcrete;
        // Structural steel sections
        public int dminStrucSteelSectionID; // section id
        public int dMinStrucSteelVertexIndex; // section vertex
        public double dminStrucSteel;
        public int dmaxStrucSteelSectionID; // section id
        public int dMaxStrucSteelVertexIndex; // section vertex
        public double dmaxStrucSteel;

        public DistancesTuple(double teta, int dMinRebarId, double dminRebar, int dMaxRebarId, double dmaxRebar,
            int dMinVertexIndex, double dminConcrete, int dMaxVertexIndex, double dmaxConcrete,
            int dminStrucSteelSectionID, int dMinStrucSteelVertexIndex, double dminStrucSteel,
            int dmaxStrucSteelSectionID, int dMaxStrucSteelVertexIndex, double dmaxStrucSteel)
        {
            this.teta = teta;
            this.dMinRebarId = dMinRebarId;
            this.dminRebar = dminRebar;
            this.dMaxRebarId = dMaxRebarId;
            this.dmaxRebar = dmaxRebar;
            this.dMinVertexIndex = dMinVertexIndex;
            this.dminConcrete = dminConcrete;
            this.dMaxVertexIndex = dMaxVertexIndex;
            this.dmaxConcrete = dmaxConcrete;
            this.dminStrucSteelSectionID = dminStrucSteelSectionID;
            this.dMinStrucSteelVertexIndex = dMinStrucSteelVertexIndex;
            this.dminStrucSteel = dminStrucSteel;
            this.dmaxStrucSteelSectionID = dmaxStrucSteelSectionID;
            this.dMaxStrucSteelVertexIndex = dMaxStrucSteelVertexIndex;
            this.dmaxStrucSteel = dmaxStrucSteel;
        }

        public override bool Equals(object obj)
        {
            return obj is DistancesTuple other &&
                   teta == other.teta &&
                   dMinRebarId == other.dMinRebarId &&
                   dminRebar == other.dminRebar &&
                   dMaxRebarId == other.dMaxRebarId &&
                   dmaxRebar == other.dmaxRebar &&
                   dMinVertexIndex == other.dMinVertexIndex &&
                   dminConcrete == other.dminConcrete &&
                   dMaxVertexIndex == other.dMaxVertexIndex &&
                   dmaxConcrete == other.dmaxConcrete &&
                   dminStrucSteelSectionID == other.dminStrucSteelSectionID &&
                   dMinStrucSteelVertexIndex == other.dMinStrucSteelVertexIndex &&
                   dminStrucSteel == other.dminStrucSteel &&
                   dmaxStrucSteelSectionID == other.dmaxStrucSteelSectionID &&
                   dMaxStrucSteelVertexIndex == other.dMaxStrucSteelVertexIndex &&
                   dmaxStrucSteel == other.dmaxStrucSteel;
        }

        public override int GetHashCode()
        {
            int hashCode = -1231195115;
            hashCode = hashCode * -1521134295 + teta.GetHashCode();
            hashCode = hashCode * -1521134295 + dMinRebarId.GetHashCode();
            hashCode = hashCode * -1521134295 + dminRebar.GetHashCode();
            hashCode = hashCode * -1521134295 + dMaxRebarId.GetHashCode();
            hashCode = hashCode * -1521134295 + dmaxRebar.GetHashCode();
            hashCode = hashCode * -1521134295 + dMinVertexIndex.GetHashCode();
            hashCode = hashCode * -1521134295 + dminConcrete.GetHashCode();
            hashCode = hashCode * -1521134295 + dMaxVertexIndex.GetHashCode();
            hashCode = hashCode * -1521134295 + dmaxConcrete.GetHashCode();
            hashCode = hashCode * -1521134295 + dminStrucSteelSectionID.GetHashCode();
            hashCode = hashCode * -1521134295 + dMinStrucSteelVertexIndex.GetHashCode();
            hashCode = hashCode * -1521134295 + dminStrucSteel.GetHashCode();
            hashCode = hashCode * -1521134295 + dmaxStrucSteelSectionID.GetHashCode();
            hashCode = hashCode * -1521134295 + dMaxStrucSteelVertexIndex.GetHashCode();
            hashCode = hashCode * -1521134295 + dmaxStrucSteel.GetHashCode();
            return hashCode;
        }

        public void Deconstruct(out double teta, out int dMinRebarId, out double dminRebar, out int dMaxRebarId, out double dmaxRebar,
            out int dMinVertexIndex, out double dminConcrete, out int dMaxVertexIndex, out double dmaxConcrete,
            out int dminStrucSteelSectionID, out int dMinStrucSteelVertexIndex, out double dminStrucSteel,
            out int dmaxStrucSteelSectionID, out int dMaxStrucSteelVertexIndex, out double dmaxStrucSteel)
        {
            teta = this.teta;
            dMinRebarId = this.dMinRebarId;
            dminRebar = this.dminRebar;
            dMaxRebarId = this.dMaxRebarId;
            dmaxRebar = this.dmaxRebar;
            dMinVertexIndex = this.dMinVertexIndex;
            dminConcrete = this.dminConcrete;
            dMaxVertexIndex = this.dMaxVertexIndex;
            dmaxConcrete = this.dmaxConcrete;
            dminStrucSteelSectionID = this.dminStrucSteelSectionID;
            dMinStrucSteelVertexIndex = this.dMinStrucSteelVertexIndex;
            dminStrucSteel = this.dminStrucSteel;
            dmaxStrucSteelSectionID = this.dmaxStrucSteelSectionID;
            dMaxStrucSteelVertexIndex = this.dMaxStrucSteelVertexIndex;
            dmaxStrucSteel = this.dmaxStrucSteel;
        }

        public static implicit operator (double teta, int dMinRebarId, double dminRebar, int dMaxRebarId, double dmaxRebar,
            int dMinVertexIndex, double dminConcrete, int dMaxVertexIndex, double dmaxConcrete,
            int dminStrucSteelSectionID, int dMinStrucSteelVertexIndex, double dminStrucSteel,
            int dmaxStrucSteelSectionID, int dMaxStrucSteelVertexIndex, double dmaxStrucSteel)
            (DistancesTuple value)
        {
            return (value.teta, value.dMinRebarId, value.dminRebar, value.dMaxRebarId, value.dmaxRebar,
                value.dMinVertexIndex, value.dminConcrete, value.dMaxVertexIndex, value.dmaxConcrete,
                value.dminStrucSteelSectionID, value.dMinStrucSteelVertexIndex, value.dminStrucSteel,
                value.dmaxStrucSteelSectionID, value.dMaxStrucSteelVertexIndex, value.dmaxStrucSteel);
        }

        public static implicit operator DistancesTuple((double teta, int dMinRebarId, double dminRebar, int dMaxRebarId, double dmaxRebar,
            int dMinVertexIndex, double dminConcrete, int dMaxVertexIndex, double dmaxConcrete,
            int dminStrucSteelSectionID, int dMinStrucSteelVertexIndex, double dminStrucSteel,
            int dmaxStrucSteelSectionID, int dMaxStrucSteelVertexIndex, double dmaxStrucSteel) value)
        {
            return new DistancesTuple(value.teta, value.dMinRebarId, value.dminRebar, value.dMaxRebarId, value.dmaxRebar,
                value.dMinVertexIndex, value.dminConcrete, value.dMaxVertexIndex, value.dmaxConcrete,
                value.dminStrucSteelSectionID, value.dMinStrucSteelVertexIndex, value.dminStrucSteel,
                value.dmaxStrucSteelSectionID, value.dMaxStrucSteelVertexIndex, value.dmaxStrucSteel);
        }
    }
}
