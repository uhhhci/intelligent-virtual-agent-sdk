# Meridian Station — Operations Handbook

> **This document is entirely fictional.** Meridian Station, its crew, its equipment, and every
> procedure below were invented for the purpose of demonstrating retrieval-augmented generation in
> the IVA SDK. Nothing here describes a real place, a real organisation, or a real person. Any
> resemblance to an actual facility is coincidental.

Meridian Station is a crewed deep-ocean research facility operated by the (fictional) Thalassa
Institute. It sits on the Kerrin Shelf at a depth of 840 metres. This handbook is the reference
material the station's virtual operations assistant answers from.

---

## Station layout

Meridian Station is arranged as five pressure modules connected by a central spine corridor called
the Causeway. Moving from the surface-side airlock inward, the modules are:

- **Module A — Ingress.** Houses the wet porch, the dive locker, and the two personnel transfer
  capsules. Ceiling height is 2.1 metres, the lowest on the station.
- **Module B — Habitat.** Six sleeping berths, the galley, and the crew commons. This is the only
  module with a viewport, a 900 mm acrylic hemisphere rated to 1,400 metres.
- **Module C — Command.** Station control, communications, and the environmental monitoring desk.
  Continuously crewed.
- **Module D — Laboratory.** Wet lab, dry lab, and the sample cold store. Kept at 14 °C, noticeably
  cooler than the rest of the station.
- **Module E — Plant.** Scrubbers, power conditioning, the battery stack, and the ballast trim
  system. Entry requires a second crew member present.

The Causeway runs the full 61 metre length of the station. Each module can be isolated from the
Causeway by a hatch that seals in under four seconds.

## Power and life support

Primary power arrives via an umbilical from the surface tender *Kerrin Dawn* and delivers 240 kW.
The station carries a lithium-titanate battery stack in Module E sized for 34 hours of full
operation with no umbilical, or roughly 96 hours in the reduced load profile described under
emergency procedures.

Atmosphere is maintained at 1 bar with an oxygen fraction between 20.4 % and 21.2 %. Carbon dioxide
is removed by two redundant amine scrubber loops, each individually capable of supporting the full
six-person crew. Scrubber cartridges are replaced every 21 days on a staggered schedule so that the
two loops never come due in the same week.

Humidity is held between 45 % and 60 %. The station runs slightly dry on purpose, because
condensation on the cold-side hull was the single largest source of nuisance faults during the first
operating season.

## Communications

Meridian Station has three communication paths, used in this order of preference:

1. **Umbilical fibre.** Full bandwidth, effectively no latency. Carries voice, video, and telemetry.
2. **Acoustic modem.** Roughly 9 kbit/s, with a 1.1 second round trip to the surface tender. Text
   and telemetry only. Used whenever the umbilical is disconnected for trim work.
3. **Emergency transponder.** A single burst beacon with a fixed 40-character message set. It is
   never used for routine traffic.

A communications blackout of more than 30 minutes on all three paths triggers a surface-side
response by protocol, whether or not the crew has requested assistance.

## Daily schedule

The station runs on a fixed 24-hour cycle referenced to surface time, deliberately, because crews on
free-running schedules drifted badly during the trial season.

- 06:30 — Wake, atmosphere check, scrubber log entry.
- 07:00 — Breakfast in Module B.
- 07:45 — Morning brief in Command. Attendance is mandatory for all crew.
- 08:00 — First work block. Dive operations, if any, are scheduled here.
- 12:30 — Lunch.
- 13:15 — Second work block. Laboratory processing of morning samples.
- 17:30 — Evening brief and handover to the night watch.
- 18:00 — Dinner. This is the only meal the whole crew is expected to take together.
- 22:30 — Quiet hours begin. Lighting in the Causeway drops to 15 %.

One crew member holds the night watch in Command through quiet hours, rotating on a six-day cycle.

## Dive operations

All dives launch from the wet porch in Module A. The station supports two dive modes:

- **Tethered ambient dives**, limited to 40 minutes bottom time and a 120 metre radius from the
  station. Requires two divers plus a tender in the wet porch.
- **Atmospheric suit excursions**, limited to 4 hours and a 600 metre radius. Requires one suit
  operator plus a tender and a Command watchstander dedicated to the excursion.

No dive of any kind proceeds without a verified acoustic modem link, even when the umbilical is up.
This rule exists because the umbilical is the thing most likely to be lost in exactly the situations
where a diver is outside.

## Emergency procedures

**Loss of primary power.** Command announces load shed. Module D drops to cold-store-only power,
non-essential lighting goes out, and the station transitions to the reduced load profile. Do not
open the cold store during a load shed; it holds temperature for 11 hours sealed and under 90 minutes
if opened repeatedly.

**Scrubber loop failure.** The surviving loop carries the full crew, so this is not an immediate
emergency. Command logs the fault, the crew moves to the single-loop cartridge schedule of
replacement every 10 days, and repair is scheduled at the next surface window.

**Hull breach or flooding.** Seal the affected module at the Causeway hatch. Crew accountability is
taken in Module C. Under no circumstances is a sealed module reopened to recover equipment. Meridian
Station remains positively buoyant with any single module fully flooded, but not with two.

**Fire.** Fires in Modules B, C, and D are fought with the portable clean-agent extinguishers at
each hatch. A fire in Module E is not fought by hand; the module is sealed and the fixed suppression
system is discharged from Command.

## Maintenance intervals

| System | Interval | Notes |
|---|---|---|
| Scrubber cartridges | 21 days, staggered | 10 days when running on a single loop |
| Viewport inspection | 30 days | Visual crazing check under raking light |
| Battery stack capacity test | 90 days | Requires umbilical power throughout |
| Ballast trim calibration | 14 days | Station must be on acoustic comms during the test |
| Hatch seal inspection | 7 days | All five module hatches, logged individually |
| Umbilical strain relief | 3 days | Checked at the Module A penetrator |

## Environmental conditions on the Kerrin Shelf

Ambient water temperature at station depth is stable between 3.8 °C and 4.4 °C year-round. Ambient
pressure is approximately 85 bar. Visibility averages 18 metres under station floodlights and drops
sharply during the spring sediment bloom, when it can fall below 3 metres for several days at a
time.

Current across the shelf runs predominantly north-north-east at 0.2 to 0.6 knots, with a
semi-diurnal tidal component that briefly reverses it. Dive operations are suspended when current
exceeds 1.2 knots.
