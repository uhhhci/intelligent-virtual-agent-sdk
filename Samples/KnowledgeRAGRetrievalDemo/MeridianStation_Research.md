# Meridian Station — Research Programme Reference

> **This document is entirely fictional.** The Thalassa Institute, Meridian Station, the research
> programmes below, and every named individual are inventions created to demonstrate
> retrieval-augmented generation in the IVA SDK. No real person, organisation, dataset, or study is
> described here.

This is the companion reference to the Operations Handbook. It covers what Meridian Station's crew
actually study, who is aboard, and how samples and data are handled.

---

## Programme summary

Meridian Station runs four concurrent research programmes during a standard 90-day rotation. They
are referred to on station by their short codes.

| Code | Programme | Lead | Duration |
|---|---|---|---|
| SILT | Sediment transport on the Kerrin Shelf | Dr. Ines Halloway | Full rotation |
| LUME | Bioluminescent signalling in midwater fauna | Dr. Petra Osgood | Full rotation |
| ANVIL | Long-baseline seismic monitoring | Tobias Renn | Continuous, uncrewed between rotations |
| KELP | Cold-water macroalgae transplant trial | Dr. Ines Halloway | Days 20–70 only |

## SILT — Sediment transport

SILT studies how sediment moves across the Kerrin Shelf, and in particular what drives the spring
bloom that collapses visibility every year. The programme deploys sixteen sediment traps in two
concentric rings at 200 and 500 metres from the station.

Traps are recovered on a 14-day cycle. Each recovery is a tethered ambient dive for the inner ring
and an atmospheric suit excursion for the outer ring. Recovered traps go straight to the Module D
wet lab, where the sample is split: one half is wet-preserved for grain-size analysis, the other is
dried at 60 °C for mass balance.

The headline finding of the current rotation is that roughly 70 % of the annual sediment flux
arrives in an 11-day window during the bloom, rather than being distributed across the season as the
original station proposal assumed. This has already changed the maintenance schedule — viewport
inspections were moved off the bloom window entirely.

## LUME — Bioluminescent signalling

LUME records bioluminescent displays in midwater fauna using a ring of six low-light cameras mounted
on the station's exterior at 4 metre spacing. The cameras run continuously and generate about 240 GB
per day, which is why LUME is the single largest consumer of umbilical bandwidth on the station.

The programme's working hypothesis is that at least three of the observed display patterns are
communicative rather than defensive. Distinguishing the two is the central methodological problem:
a defensive flash and a signalling flash look nearly identical in a single camera, so LUME depends
on the six-camera ring to reconstruct which direction a display was aimed.

Camera calibration is performed every 30 days and requires all station floodlights to be off for
90 minutes. This is scheduled during quiet hours to avoid disrupting other work, and it is the one
routine activity that suspends dive operations for the whole crew.

## ANVIL — Seismic monitoring

ANVIL is the only programme that continues when the station is uncrewed. It maintains four ocean
bottom seismometers on a long baseline, the nearest 1.2 km from the station and the furthest 14 km
out on the shelf edge.

The instruments are autonomous and record to internal storage. Crew involvement is limited to a
battery and data swap once per rotation, performed from the surface tender rather than from the
station itself, because the outer instruments are far beyond excursion range.

ANVIL exists primarily to characterise the background seismicity of the shelf. A secondary and
increasingly prominent use is correlating small slope failures with the SILT sediment record.

## KELP — Macroalgae transplant trial

KELP is a 50-day trial testing whether three cold-water macroalgae strains can establish on
artificial substrate at station depth. It runs only between rotation day 20 and day 70, because the
transplant material has to arrive with the mid-rotation resupply and be recovered before the crew
changeover.

Nine substrate panels are set out in a grid 40 metres from Module A, well within tethered dive
range. Panels are photographed every third day and the images are scored for percentage cover.

The trial has been run twice. In the first attempt all nine panels were lost when the anchoring
scheme proved inadequate during the bloom; the current attempt uses a heavier gravity anchor and
has retained all nine panels through one bloom so far.

## Crew roster for the current rotation

All six crew members are fictional characters created for this sample.

- **Dr. Ines Halloway** — Station scientist and rotation lead. Leads SILT and KELP. Eleven previous
  rotations aboard Meridian. Holds the atmospheric suit qualification.
- **Dr. Petra Osgood** — Optical ecologist. Leads LUME. Second rotation aboard. Responsible for the
  camera ring and its calibration.
- **Tobias Renn** — Geophysicist. Leads ANVIL. Splits his time between the station and the surface
  tender, and is frequently the crew member absent from the morning brief for that reason.
- **Marta Quilty** — Station engineer. Owns Module E, the battery stack, and the ballast trim
  system. Every entry into Module E is either with her or cleared by her.
- **Sam Okonjo** — Dive supervisor and medic. Every dive on the station is authorised by him. Also
  maintains the wet porch and the dive locker in Module A.
- **Wren Adeyemi** — Data and communications officer. Manages the umbilical link, the acoustic
  modem, and the daily data push to shore. Holds the night watch most often by preference.

## Data handling

All station data is written to the Module C array and pushed to shore over the umbilical each night
during quiet hours. The push is scheduled then because LUME's camera traffic saturates the link
during working hours.

Data that has not been confirmed as received on shore is never deleted from the station array,
regardless of how full the array is getting. When the array passes 85 % capacity, LUME reduces its
camera ring from six cameras to three until the backlog clears. This is the only programme with a
standing instruction to degrade its own data collection, and it exists because LUME is also the
programme generating the backlog.

## Sample custody

Physical samples are logged in the Module D custody book at the moment they enter the wet lab, not
when they are collected. Each entry records the programme code, the recovery date, the diver or
excursion that recovered it, and the storage location.

Cold store capacity is 340 litres. During the bloom, SILT alone can fill 60 % of that in a single
14-day recovery cycle, so the standing rule is that SILT dries and discards its mass-balance half
within 48 hours rather than holding both halves wet.

## Frequently asked questions

**How deep is the station?** 840 metres, on the Kerrin Shelf.

**How many people are aboard?** Six during a crewed rotation. The station is uncrewed between
rotations, when only ANVIL continues to record.

**What is the bloom?** The spring sediment bloom, an 11-day window that carries roughly 70 % of the
annual sediment flux and drops visibility below 3 metres.

**Which programme uses the most bandwidth?** LUME, by a wide margin — about 240 GB per day from the
six-camera ring.

**Who authorises a dive?** Sam Okonjo, the dive supervisor. No dive proceeds without a verified
acoustic modem link.

**What happens if the station loses all communications?** A blackout longer than 30 minutes on all
three paths triggers a surface-side response automatically, whether or not the crew asked for it.
