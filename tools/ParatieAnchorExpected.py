"""Independent anchor expectations, N/mm/MPa. Uses arithmetic and pi only; never overwrites a fixture."""
import json
import math
import sys

# Manually read chart ordinates: SG1(2 MPa)=0.25, AL1(1 MPa)=0.18;
# lengths along the bonded axis, D=200 mm, alpha=1.5 / 1.8.
segments = [math.pi * 200 * 1.5 * 2000 * .25, math.pi * 200 * 1.8 * 4000 * .18]
samples = [sum(segments), 1_000_000.0]
characteristic = min(sum(samples) / 2 / 1.75, min(samples) / 1.70)
data = {
    "source": "Arithmetic fixed before the GPC kernel; chart points from Viggiani pp.394-395, NTC2018 Tab.6.6.I-III",
    "segments": segments,
    "raw_total": sum(segments),
    "calculated_samples": samples,
    "calculated_characteristic": characteristic,
    "calculated_permanent_design": characteristic / 1.2,
    "calculated_utilization_400kN": 400_000 / (characteristic / 1.2),
    "measured_samples": [900_000, 1_200_000, 1_350_000],
    "measured_characteristic": min(1_150_000 / 1.3, 900_000 / 1.2),
    "measured_temporary_design": min(1_150_000 / 1.3, 900_000 / 1.2) / 1.1,
}
with open(sys.argv[1], "x", encoding="utf-8") as stream:
    json.dump(data, stream, indent=2, ensure_ascii=False)
    stream.write("\n")
