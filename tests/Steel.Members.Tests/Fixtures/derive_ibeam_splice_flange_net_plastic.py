"""Independent arithmetic for the category C flange net-plastic component.

Geometry is SCI P398 D.1; these category C values are analytical calculations,
not claimed to be a published result. No Checker import or fixture mutation.
"""
from math import pow

h, b, tw, tf, area, iy = 453.4, 189.9, 8.5, 12.7, 8550.0, 294000000.0
aw = (h - 2 * tf) * tw
iw = tw * pow(h - 2 * tf, 3) / 12
lever = h - tf
n, v, m = -150000.0, -150000.0, -200000000.0
top = (1 - aw / area) * n / 2 - (1 - iw / iy) * m / lever
bottom = (1 - aw / area) * n / 2 + (1 - iw / iy) * m / lever
print(f"Aw={aw:.12f} mm2; Iw={iw:.12f} mm4; z={lever:.12f} mm")
print(f"Ntop={top:.12f} N; Nbottom={bottom:.12f} N; V={v:.12f} N")
for name, width, thickness in (("Cover", 180, 12), ("Member", b, tf)):
    net = (width - 2 * 22) * thickness
    for policy, gamma in (("EC", 1.0), ("NTC", 1.05)):
        resistance = net * 275 / gamma
        print(f"{name}.{policy}: Anet={net:.12f} mm2; R={resistance:.12f} N; "
              f"u_top={max(0, top) / resistance:.12f}; u_bottom={max(0, bottom) / resistance:.12f}")
axial_only_each_flange = (1 - aw / area) * 1600000 / 2
print(f"N=1.6MN,V=M=0: each flange={axial_only_each_flange:.12f} N")
print(f"old cover rupture={0.9 * 1632 * 410 / 1.1:.12f} N")
print(f"new cover net plastic={1632 * 275:.12f} N")
