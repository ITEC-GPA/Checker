"""Independent arithmetic for a proposed SCI P358 Check13/14 component.

No Checker import; N, mm, MPa. This is a derivation, not a .NET test run.
P358 printed pp.29,48-49,279; PDF pages39,58-59,289.
"""
from math import sqrt
import json


def source_case():
    a, hp, tw, fy, fu, beta_w, gamma_mu, force = 4.24, 290., 9.5, 275., 410., .85, 1.1, 175000.
    full_yield_throat = tw * fy * beta_w * gamma_mu / (sqrt(2) * fu)
    stress = force / (2 * a * hp * sqrt(2))
    equivalent = sqrt(stress ** 2 + 3 * stress ** 2)
    equivalent_resistance = fu / (beta_w * gamma_mu)
    normal_resistance = .9 * fu / gamma_mu
    directional_capacity = sqrt(2) * a * hp * fu / (beta_w * gamma_mu)
    normal_capacity = 2 * sqrt(2) * a * hp * .9 * fu / gamma_mu
    beam_yield = tw * hp * fy
    # Independent plate Mode1 arithmetic, with the published rounded throat4.24.
    m, n, ew = (90 - tw - 2 * .8 * a * sqrt(2)) / 2, 30., 33. / 4
    moment = .25 * hp * 10 ** 2 * fu / gamma_mu
    mode1 = (8 * n - 2 * ew) * moment / (2 * m * n - ew * (m + n))
    d, tw_support, gauge, hole, pitch, rows = 246.7, 13.8, 90., 22., 70., 4
    unit_moment = fu * tw_support ** 2 / (4 * gamma_mu)
    beta, gamma = gauge / d, hole / d
    eta = ((rows - 1) * pitch - rows * hole / 2) / d
    support_capacity = 8 * unit_moment / (1 - beta) * (eta + 1.5 * sqrt(1 - beta) * sqrt(1 - gamma))
    return {
        "weld": {
            "published_Check13_throat_min_mm": .4 * tw,
            "AppendixC1_full_beam_yield_throat_min_mm": full_yield_throat,
            "AppendixC1_hierarchy_utilization": full_yield_throat / a,
            "sigma_normal_and_tau_perpendicular_MPa": stress,
            "equivalent_stress_MPa": equivalent,
            "equivalent_resistance_MPa": equivalent_resistance,
            "normal_resistance_MPa": normal_resistance,
            "directional_resistance_N": directional_capacity,
            "normal_resistance_N": normal_capacity,
            "load_utilization": force / directional_capacity,
            "beam_web_yield_N_not_ultimate": beam_yield,
            "beam_web_ultimate_N": tw * hp * fu / gamma_mu,
            "plate_Mode1_N": mode1,
            "supplemental_plate_Mode1_to_weld_capacity_ratio": mode1 / directional_capacity,
        },
        "support": {
            "d2_mm": d, "unit_plastic_moment_Nmm_per_mm": unit_moment,
            "beta1": beta, "gamma1": gamma, "eta1": eta,
            "resistance_N": support_capacity,
            "utilization": force / support_capacity,
            "published_P358_resistance_kN": 405.,
            "independent_SDS2_resistance_kN": 405.4,
            "tw12_resistance_N": support_capacity * (12 / tw_support) ** 2,
        },
    }


if __name__ == "__main__":
    print(json.dumps(source_case(), indent=2))
