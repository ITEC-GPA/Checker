using GPC.Geometry;

namespace GPC.Checker.Helper
{
    internal struct BoundaryDistances
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
        public Point2d dMinStrucSteelVertex; // section vertex
        public double dminStrucSteel;
        public int dmaxStrucSteelSectionID; // section id
        public Point2d dMaxStrucSteelVertex; // section vertex
        public double dmaxStrucSteel;

        public BoundaryDistances(double teta, int dMinRebarId, double dminRebar, int dMaxRebarId, double dmaxRebar,
            int dMinVertexIndex, double dminConcrete, int dMaxVertexIndex, double dmaxConcrete,
            int dminStrucSteelSectionID, Point2d dMinStrucSteelVertex, double dminStrucSteel,
            int dmaxStrucSteelSectionID, Point2d dMaxStrucSteelVertex, double dmaxStrucSteel)
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
            this.dMinStrucSteelVertex = dMinStrucSteelVertex;
            this.dminStrucSteel = dminStrucSteel;
            this.dmaxStrucSteelSectionID = dmaxStrucSteelSectionID;
            this.dMaxStrucSteelVertex = dMaxStrucSteelVertex;
            this.dmaxStrucSteel = dmaxStrucSteel;
        }

        public override bool Equals(object obj)
        {
            return obj is BoundaryDistances other &&
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
                   dMinStrucSteelVertex == other.dMinStrucSteelVertex &&
                   dminStrucSteel == other.dminStrucSteel &&
                   dmaxStrucSteelSectionID == other.dmaxStrucSteelSectionID &&
                   dMaxStrucSteelVertex == other.dMaxStrucSteelVertex &&
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
            hashCode = hashCode * -1521134295 + dMinStrucSteelVertex.GetHashCode();
            hashCode = hashCode * -1521134295 + dminStrucSteel.GetHashCode();
            hashCode = hashCode * -1521134295 + dmaxStrucSteelSectionID.GetHashCode();
            hashCode = hashCode * -1521134295 + dMaxStrucSteelVertex.GetHashCode();
            hashCode = hashCode * -1521134295 + dmaxStrucSteel.GetHashCode();
            return hashCode;
        }
    }
}
