using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPC.Model.Sections;

namespace GPC.Checker.Steel.EC
{
    public class ECBeamCheckerResistance
    {

        #region internalfunction
        public double Nt_Rd(double A, double fy, double Anet, double fu, double gm0, double gm2)
        {
            double Nt_Rd = Math.Min(A * fy / gm0, 0.9 * Anet * fu / gm2);
            return Nt_Rd;
        }

        public double Nc_Rd(double A, double fy, double gm0)
        {
            double Nc_Rd = A * fy / gm0;
            return Nc_Rd;
        }

        public void M_Rd(Section section, double fy, double Ned, double VEd, double VRd, double gm0, out double MRdNy, out double MRdNz)
        {
            double A = section.Area;
            double Wply = section.Wpl22;
            double Wplz = section.Wpl11;
            Type typeShape = section.GetType();

            double rho = Math.Min(Math.Pow(2.0 * Math.Abs(VEd) / VRd - 1.0, 2.0), 1.0);

            if (VEd <= 0.5 * VRd)
            {
                rho = 0.0;
            }
            
            double Mrdy = Wply * (1.0 - rho) * fy / gm0;
            double Mrdz = Wplz * (1.0 - rho) * fy / gm0;

            MRdNy = 0.0;
            MRdNz = 0.0;
            double NplRd = fy * A / gm0;
            double n = Math.Abs(Ned) / NplRd;

            if (typeShape == typeof(SectionRectangular)) {
                MRdNy = Mrdy * Math.Pow(1.0 - Math.Abs(Ned) / NplRd, 2.0);
                MRdNz = Mrdz * Math.Pow(1.0 - Math.Abs(Ned) / NplRd, 2.0);
            } else if (typeShape == typeof(SectionH)) {
                SectionH secH = (SectionH) section;
                if (secH.LenghtBottomFlange == secH.LenghtTopFlange && secH.ThicknessTopFlange == secH.ThicknessBottomFlange)
                {
                    double a = Math.Min((A - 2.0 * secH.LenghtTopFlange * secH.ThicknessTopFlange) / A, 0.5);
                    double MNyRd = Math.Min(Mrdy * (1.0 - n) / (1.0 - 0.5 * a), Mrdy);
                    double MNzRd = 0.0;
                    if (n <= a)
                    {
                        MNzRd = Mrdz;
                    }
                    else
                    {
                        MNzRd = Mrdz * (1.0 - Math.Pow((n - a) / (1.0 - a), 2.0));
                    }
                }
                else
                {
                    MRdNy = 0.0;
                    MRdNz = 0.0;
                }
            } else if (typeShape == typeof(SectionCircular))
            {
                MRdNy = Mrdy * (1.0 - Math.Pow(n, 1.7));
                MRdNz = Mrdz * (1.0 - Math.Pow(n, 1.7));
            } else if (typeShape == typeof(SectionRHS))
            {
                SectionRHS secRHS = (SectionRHS)section;
                double b = secRHS.B;
                double h = secRHS.H;
                double thk_flange = secRHS.ThicknessFlange;
                double thk_web = secRHS.ThicknessWeb;

                double aw = Math.Min((A - 2.0 * b * thk_flange) / A, 0.5);
                double af = Math.Min((A - 2.0 * h * thk_web) / A, 0.5);

                MRdNy = Math.Min(Mrdy * (1.0 - n) / (1 - 0.5 * aw), Mrdy);

                MRdNz = Math.Min(Mrdz * (1.0 - n) / (1 - 0.5 * af), Mrdz);
            } else
            {
                throw new Exception("");
            }
        }
        #endregion
    }
}
