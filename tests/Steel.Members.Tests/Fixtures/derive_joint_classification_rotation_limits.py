"""Independent numerical limits from EN1993-1-8 Figures5.4/5.5 and6.4.

No production imports or fixture changes. N, mm, MPa; moments Nmm.
This script reports limits, not whole-joint adequacy or a capacity in radians.
"""
from math import sqrt


if __name__ == "__main__":
    rb = 210000.0 * 500000000.0 / 6000.0
    print("Stiffness_Nmm_per_rad", {"EI_over_L": rb, "pinned": 0.5 * rb,
          "rigid_bracing80": 8 * rb, "rigid_other": 25 * rb})
    mb, mc = 300e6, 200e6
    for name, multiplier in [("top", 1), ("within_same_continuous_column", 2)]:
        full = min(mb, multiplier * mc)
        print("Strength_Nmm", name, {"full": full, "pinned_numerical_limit": 0.25 * full})
    print("Plate_limit_M20_S275_mm", 0.36 * 20 * sqrt(800 / 275))
    print("Panel_slenderness_limit_S355", 69 * sqrt(235 / 355))
    jrc_limit = 0.36 * 24 * sqrt(1000 / 235)
    print("JRC96658_negative_criterion_case", {
        "bending_thickness_limit_mm": jrc_limit,
        "plate_15mm_passes_thickness": 15 <= jrc_limit,
        "column_flange_16_5mm_passes_thickness": 16.5 <= jrc_limit,
        "joint_moment_kNm": 224.5,
        "beam_moment_kNm": 515.59,
        "overstrength_exemption_limit_kNm": 1.2 * 515.59,
        "published_governing_component": "Column web in compression",
        "conclusion_from_EN_criteria": "NotDemonstratedBySimplifiedRules",
        "rotation_capacity_rad": None,
    })
