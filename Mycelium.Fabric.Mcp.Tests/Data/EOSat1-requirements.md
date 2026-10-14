Requirements of the EOSat-1 Earth observation satellite, used to test the extraction of requirements.

# EOSat-1 System Requirements

## 1. Introduction

This document lists the requirements of EOSat-1. The identifiers follow the convention `REQ-<area>-<number>`.

## 2. System requirements

| ID | Requirement | Priority |
|----|-------------|----------|
| REQ-SYS-001 | The satellite launch mass, computed as the total dry mass of all units plus a 20% system margin, shall not exceed 150 kg. | H |
| REQ-SYS-002 | The payload (optical camera and mass memory) shall consume less than 70 W in imaging mode. | H |
| REQ-SYS-003 | The AOCS shall provide an attitude knowledge accuracy better than 10 arcsec, using at least two star trackers for redundancy. | H |

## 3. Payload requirements

### 3.1 Camera

| ID | Requirement | Priority |
|----|-------------|----------|
| REQ-PAY-001 | The camera shall have a ground sampling distance of at most 5 m from an altitude of 600 km. | H |
| REQ-PAY-002 | The camera mass shall not exceed 40 kg, as allocated by REQ-SYS-001. | M |

### 3.2 Data handling ###

REQ-PAY-003: The mass memory shall store at least 512 Gbit of images.

REQ-PAY-004: The images shall be downlinked within a responsive time (target TBD).

```text
# This line is in a code block, not a heading.
```

## 4. Ground segment

The ground station follows REQ-GND-001, defined in the ground segment specification.
