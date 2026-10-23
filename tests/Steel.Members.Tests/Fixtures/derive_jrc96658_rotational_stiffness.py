"""Independent EN1993-1-8 rotational-stiffness arithmetic for JRC96658.

Sources: JRC96658 (2015), pp156-175; EN1993-1-8:2005 6.3/Table6.11.
No Checker imports, no test/fixture regeneration. Units N, mm, MPa, Nmm/rad.
Three deliberately separate paths: publication, rounded source Av with zeq,
and geometric section area/Av with zeq. The publication is not recalibrated.
"""
import json
import math


E = 210000.0
HEIGHTS = [527.0, 442.0, 362.0]
COLUMN_LENGTHS = [166.05, 82.5, 163.55]
PLATE_LENGTHS = [120.0, 234.69, 175.98]
PLATE_M = [25.95, 49.24, 49.24]
K2 = 0.7 * 274.81 * 9.5 / 243.0


def row_components():
    rows = []
    for h, lc, lp, mp in zip(HEIGHTS, COLUMN_LENGTHS, PLATE_LENGTHS, PLATE_M):
        k3 = 0.7 * lc * 9.5 / 243.0
        k4 = 0.9 * lc * 16.5**3 / 33.65**3
        k5 = 0.9 * lp * 15.0**3 / mp**3
        k10 = 1.6 * 353.0 / (15.0 + 16.5 + (15.0 + 19.0) / 2.0)
        keff = 1.0 / (1.0 / k3 + 1.0 / k4 + 1.0 / k5 + 1.0 / k10)
        rows.append({"h_mm": h, "k3_mm": k3, "k4_mm": k4, "k5_mm": k5,
                     "k10_mm": k10, "keff_mm": keff})
    return rows


def assemble_with_zeq(av):
    rows = row_components()
    first_moment = sum(row["keff_mm"] * row["h_mm"] for row in rows)
    second_moment = sum(row["keff_mm"] * row["h_mm"]**2 for row in rows)
    zeq = second_moment / first_moment
    keq = first_moment / zeq
    k1 = 0.38 * av / zeq  # beta=1; same explicitly chosen equivalent lever
    compliance = 1.0 / k1 + 1.0 / K2 + 1.0 / keq
    sj = E * zeq**2 / compliance
    return {"Av_mm2": av, "rows": rows, "k2_mm": K2, "zeq_mm": zeq,
            "keq_mm": keq, "k1_mm": k1, "compliance_per_mm": compliance,
            "Sj_ini_Nmm_per_rad": sj, "Sj_ini_MNm_per_rad": sj / 1e9}


def results():
    area = 2 * 300 * 16.5 + (330 - 2 * 16.5) * 9.5 + 4 * (1 - math.pi / 4) * 27**2
    av = area - 2 * 300 * 16.5 + (9.5 + 2 * 27) * 16.5
    published = {
        "source": "JRC96658, pp174-175, Table3.8/Eq3.63-3.65",
        "Sj_ini_MNm_per_rad_published": 75.214,
        "k1_mm_published": 3.53, "k2_mm_published": 7.52,
        "keff_mm_published": [2.43, 1.23, 1.70],
        "zeq_mm_published": 466.44, "keq_mm_published": 5.23,
        "Sj_ini_MNm_from_published_final_rounded_values_with_sum_and_correct_units":
            E * 466.44**2 / (1.0 / 3.53 + 1.0 / 7.52 + 1.0 / 5.23) / 1e9,
        "approximate_panel_lever_mm": (527.0 + 442.0) / 2.0,
        "k1_mm_from_approximate_panel_lever": 0.38 * 4495.0 / ((527.0 + 442.0) / 2.0),
    }
    geometric = assemble_with_zeq(av)
    geometric["A_geometric_mm2"] = area
    return {"publication_as_printed": published,
            "source_Av4495_recalculated_with_zeq": assemble_with_zeq(4495.0),
            "geometric_area_and_Av_recalculated_with_zeq": geometric}


if __name__ == "__main__":
    print(json.dumps(results(), indent=2))
