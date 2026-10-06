# EOSat-1 mission specification

## Mission

EOSat-1 is a small Earth observation satellite in a 600 km sun-synchronous orbit, designed for a 5-year mission. It takes
multispectral images with a 5 m ground sampling distance, stores them on board and downlinks them in X-band. Telemetry
and telecommands use S-band.

## System breakdown

The spacecraft has a structure and an electrical harness, and seven subsystems. The preliminary equipment list gives,
for each unit, the quantity, the dry mass and the nominal power consumption.

| Subsystem | Unit | Quantity | Mass [kg] | Power [W] | Other properties |
|---|---|---|---|---|---|
| (spacecraft) | Structure (aluminium honeycomb panels) | 1 | 28.0 | 0 | |
| (spacecraft) | Harness | 1 | 7.0 | 0 | |
| Electrical power | Solar array wing (GaAs cells) | 2 | 6.5 | 0 | 90 W generated at end of life |
| Electrical power | Li-ion battery | 1 | 5.8 | 0 | 420 Wh usable capacity |
| Electrical power | Power control and distribution unit | 1 | 4.2 | 6 | |
| Attitude and orbit control | Reaction wheel | 4 | 1.9 | 10 | |
| Attitude and orbit control | Star tracker | 2 | 1.4 | 7 | 6 arcsec accuracy |
| Attitude and orbit control | Sun sensor | 4 | 0.2 | 0.5 | |
| Attitude and orbit control | Magnetorquer | 3 | 0.8 | 2 | |
| Data handling | On-board computer | 1 | 2.1 | 12 | |
| Communication | S-band transceiver | 1 | 1.1 | 18 | 2 Mbps downlink |
| Communication | X-band transmitter | 1 | 1.6 | 35 | 120 Mbps downlink |
| Communication | S-band patch antenna | 2 | 0.3 | 0 | |
| Communication | X-band high-gain antenna | 1 | 0.6 | 0 | |
| Payload | Multispectral push-broom camera | 1 | 38.0 | 55 | |
| Payload | Mass memory | 1 | 1.5 | 9 | |
| Thermal control | Heater | 6 | 0.1 | 5 | |
| Thermal control | Radiator panel | 2 | 1.2 | 0 | |
| Propulsion | 1 N hydrazine thruster | 4 | 0.3 | 0 | |
| Propulsion | Propellant tank (dry) | 1 | 4.5 | 0 | |

A GNSS receiver for orbit determination is also foreseen; its supplier, mass and power are still to be determined.

## Requirements

- **REQ-SYS-001**: The satellite launch mass, computed as the total dry mass of all units plus a 20% system margin, shall
  not exceed 150 kg.
- **REQ-SYS-002**: The payload (camera and mass memory) shall consume less than 70 W in imaging mode.
- **REQ-SYS-003**: The attitude and orbit control subsystem shall provide an attitude knowledge accuracy better than
  10 arcsec, using at least two star trackers for redundancy.
- **REQ-SYS-004**: The attitude and orbit control subsystem shall include at least four reaction wheels, so that the
  mission survives the failure of one wheel.
- **REQ-SYS-005**: The X-band downlink shall provide a data rate of at least 150 Mbps.
- **REQ-SYS-006**: The battery shall provide at least 300 Wh to power the satellite during the longest eclipse.
