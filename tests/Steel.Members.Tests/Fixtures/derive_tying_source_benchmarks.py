"""Independent tying calculations from SCI P358 and CTICM/SCI SBE_MS5.

No production imports, calibration, or existing fixture updates. N, mm, MPa.
Published rounded values and calculations from stated inputs remain separate.
These are COMPONENT benchmarks; the SBE source does not define its support.
"""
from math import sqrt
import json


def modes(moment1, moment2, m, edge, footprint, bolt_count, bolt_fu, bolt_as, gamma_mu):
    n = min(edge, 1.25 * m)
    ew = footprint / 4
    denominator = 2 * m * n - ew * (m + n)
    assert denominator > 0
    bolt = 0.9 * bolt_fu * bolt_as / gamma_mu
    return {"m_mm": m, "n_mm": n, "ew_mm": ew,
            "plastic_moment1_Nmm": moment1, "plastic_moment2_Nmm": moment2,
            "bolt_tension_N": bolt, "bolt_count": bolt_count,
            "mode1_N": (8 * n - 2 * ew) * moment1 / denominator,
            "mode2_N": (2 * moment2 + n * bolt_count * bolt) / (m + n),
            "mode3_N": bolt_count * bolt}


def p358_partial(a=4.24):
    # Published example2 pp44-49: 406x178x74 beam, 305x305x137 column,
    # plate150x10, height290; 4x2 M20/22, As245; a4.24 (6mm leg rounded).
    rows, pitch, gauge, end, edge, tw, tp, fu, gamma_mu = 4, 70, 90, 40, 30, 9.5, 10, 410, 1.1
    effective_pitch = min(pitch, gauge - tw - 2 * a * sqrt(2) + 22)
    effective_end = min(end, .5 * (gauge - tw - 2 * a * sqrt(2)) + 22 / 2)
    leff = 2 * effective_end + (rows - 1) * effective_pitch
    m = (gauge - tw - 2 * .8 * a * sqrt(2)) / 2
    moment = .25 * leff * tp ** 2 * fu / gamma_mu
    result = modes(moment, moment, m, edge, 33, 2 * rows, 800, 245, gamma_mu)
    result.update({"source": "SCI P358(2014) example2 pp44-49; separate tying F175kN",
        "throat_used_mm": a, "effective_end_mm": effective_end,
        "effective_pitch_mm": effective_pitch, "effective_length_mm": leff,
        "beam_web_tying_N": tw * 290 * fu / gamma_mu,
        "published_rounded_N": {"mode1": 381000, "mode2": 670000, "mode3": 1280000, "beam_web": 1027000}})
    # Reproduce source's rounded intermediate calculation independently as such.
    result["published_rounded_intermediate_arithmetic_N"] = {
        "mode1": (8 * 30 - 2 * 8.25) * 2.7e6 / (2 * 35.4 * 30 - 8.25 * (35.4 + 30)),
        "mode2": (2 * 2.7e6 + 30 * 8 * 160e3) / (35.4 + 30), "mode3": 8 * 160e3}
    # Check14: an OUT-OF-PLANE supporting web model, NOT a 4b in-plane web check.
    # Dimensional note: fu*tw^2/(4gamma) has units N = Nmm/mm, NOT Nmm.
    depth, support_tw = 246.7, 13.8
    beta = gauge / depth
    gamma = 22 / depth
    eta = ((rows - 1) * pitch - rows * 22 / 2) / depth
    unit_moment = fu * support_tw ** 2 / (4 * gamma_mu)
    support_capacity = 8 * unit_moment / (1 - beta) * (eta + 1.5 * sqrt(1 - beta) * sqrt(1 - gamma))
    result["support_web_check14_NOT_automatically_qualified"] = {
        "depth_between_fillets_mm": depth, "web_thickness_mm": support_tw,
        "beta": beta, "gamma": gamma, "eta": eta,
        "plastic_moment_per_unit_length_Nmm_per_mm": unit_moment,
        "resistance_N": support_capacity, "published_rounded_N": 405000}
    return result


def sbe_double_angle():
    # Published example4.4 pp68-82: IPE A550 tw9, two L90x90x10 root11,
    # 6 rows M20/22, pitch70, end/free-edge40, transverse support gauge109.
    # Ftying370kN and Vordinary450kN are distinct design situations.
    rows, pitch, end, edge, tw, t, root, gauge = 6, 70, 40, 40, 9, 10, 11, 109
    fu, fy, gamma_mu, gamma_m0 = 430, 275, 1.1, 1
    leff = 2 * min(end, .5 * (gauge - tw - 2 * root) + 22 / 2) + (rows - 1) * min(pitch, gauge - tw - 2 * root + 22)
    m = (gauge - tw - 2 * t - 2 * .8 * root) / 2
    moment = .25 * leff * t ** 2 * fu / gamma_mu
    result = modes(moment, moment, m, edge, 37, 2 * rows, 800, 245, gamma_mu)
    k1 = min(2.8 * end / 22 - 1.7, 1.4 * pitch / 22 - 1.7, 2.5)
    alpha = min(edge / (3 * 22), 800 / fu, 1)
    shear_per_plane = .6 * 800 * 245 / gamma_mu
    angle_bearing = 2 * rows * k1 * alpha * fu * 20 * t / gamma_mu
    beam_bearing = rows * k1 * alpha * fu * 20 * tw / gamma_mu
    height = 2 * end + (rows - 1) * pitch
    # Net/block paths under longitudinal TYING, not the vertical-shear paths.
    angle_ant1 = 2 * t * ((rows - 1) * pitch - (rows - 1) * 22)
    angle_anv1 = 4 * t * (edge - 22 / 2)
    angle_ant2 = 2 * t * (end + (rows - 1) * pitch - (rows - .5) * 22)
    angle_anv2 = 2 * t * (edge - 22 / 2)
    beam_net = tw * (height - rows * 22)
    beam_ant = tw * ((rows - 1) * pitch - (rows - 1) * 22)
    beam_anv = 2 * tw * (edge - 22 / 2)
    def block(ant, anv):
        return ant * fu / gamma_mu + anv * fy / (sqrt(3) * gamma_m0)
    result.update({"source": "CTICM/SCI SBE_MS5 example4.4 pp78-82; support unspecified",
        "effective_length_mm": leff, "gammaMu": gamma_mu,
        "beam_bolts_double_shear_N": 2 * rows * shear_per_plane,
        "angle_beam_leg_bearing_N": angle_bearing, "beam_web_bearing_N": beam_bearing,
        "angle_block_case1_areas_mm2": [angle_ant1, angle_anv1],
        "angle_block_case2_areas_mm2": [angle_ant2, angle_anv2],
        "angle_block_case1_N": block(angle_ant1, angle_anv1),
        "angle_block_case2_N": block(angle_ant2, angle_anv2),
        "beam_web_net_area_mm2": beam_net, "beam_web_net_tension_N": .9 * beam_net * fu / gamma_mu,
        "beam_web_block_areas_mm2": [beam_ant, beam_anv], "beam_web_block_N": block(beam_ant, beam_anv),
        "tying_beam_fastener_Lj_in_force_direction_mm": 0,
        "published_rounded_N": {"mode1": 696000, "mode2": 1190000, "mode3": 1920000,
            "beam_bolts_double_shear": 1284000, "angle_bearing": 1428000,
            "angle_block1": 2060000, "angle_block2": 2195000,
            "beam_bearing": 642000, "beam_net": 944000, "beam_block": 927000}})
    result["published_rounded_intermediate_arithmetic_N"] = {
        "mode1": (8 * 39 - 2 * 9.25) * 4.2e6 / (2 * 31 * 39 - 9.25 * (31 + 39)),
        "mode2": (2 * 4.2e6 + 39 * 12 * 160e3) / (31 + 39), "mode3": 12 * 160e3}
    return result


def p358_geometry_sensitivity(tw, tp, a):
    """Analytical perturbation of Check11/12, explicitly NOT a published example."""
    effective_end = min(40, (90 - tw - 2 * a * sqrt(2) + 22) / 2)
    effective_pitch = min(70, 90 - tw - 2 * a * sqrt(2) + 22)
    leff = 2 * effective_end + 3 * effective_pitch
    m = (90 - tw - 1.6 * a * sqrt(2)) / 2
    moment = .25 * leff * tp * tp * 410 / 1.1
    result = modes(moment, moment, m, 30, 33, 8, 800, 245, 1.1)
    result.update({"source": "Independent analytical variation of P358 Check11/12, not published geometry",
                   "beam_web_thickness_mm": tw, "plate_thickness_mm": tp, "throat_mm": a,
                   "effective_end_mm": effective_end, "effective_length_mm": leff,
                   "beam_web_tying_N": tw * 290 * 410 / 1.1})
    return result


if __name__ == "__main__":
    result = {"p358_stated_throat_4_24": p358_partial(),
              "p358_exact_6mm_leg_optional_unrounded_geometry": p358_partial(6 / sqrt(2)),
              "sbe_double_angle_source_geometry": sbe_double_angle(),
              "p358_end_length_capped_sensitivity": p358_geometry_sensitivity(16, 10, 7),
              "p358_beam_web_governs_sensitivity": p358_geometry_sensitivity(4.25, 12, 3)}
    print(json.dumps(result, indent=2, allow_nan=False))
