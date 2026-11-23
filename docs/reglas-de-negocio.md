# Reglas de negocio

CitaDent tiene 19 reglas de negocio, identificadas desde `RN-01` hasta `RN-19`.

Las reglas de agenda (`RN-01` a `RN-09`) son el núcleo del sistema y se prueban en el dominio antes de escribir la API. Las reglas de tratamientos y cobros (`RN-17` a `RN-19`) aplican desde la fase 2.

## Índice

- [Agenda](#agenda-rn-01-a-rn-09)
- [Citas](#citas-rn-07-a-rn-09)
- [Pacientes](#pacientes-rn-10-a-rn-12)
- [Clínico](#clínico-rn-13-a-rn-15)
- [Catálogo](#catálogo-rn-16)
- [Tratamientos](#tratamientos-rn-17)
- [Cobros](#cobros-rn-18-a-rn-19)

## Agenda (`RN-01` a `RN-09`)

| Código | Regla | Se valida en |
|---|---|---|
| `RN-01` | El fin de una cita es el inicio más la duración de su procedimiento. | Domain |
| `RN-02` | Un odontólogo no puede tener dos citas agendadas que se solapen. | Domain |
| `RN-03` | Un sillón no puede tener dos citas agendadas que se solapen. | Domain |
| `RN-04` | Dos citas seguidas (el fin de una es el inicio de la otra) no cuentan como cruce. | Domain |
| `RN-05` | La cita debe caer dentro del horario laboral del odontólogo y fuera de sus bloqueos (vacaciones, feriados). | Domain |
| `RN-06` | No se agenda en el pasado. | Application |

## Citas (`RN-07` a `RN-09`)

| Código | Regla | Se valida en |
|---|---|---|
| `RN-07` | Estados: Agendada, Completada, Cancelada, No asistió. Solo una cita Agendada cambia de estado; los demás estados son finales. | Domain |
| `RN-08` | Cancelar exige un motivo y libera el horario del odontólogo y del sillón. | Domain |
| `RN-09` | Solo el odontólogo asignado o un administrador marca la cita como Completada. | Application |

## Pacientes (`RN-10` a `RN-12`)

| Código | Regla | Se valida en |
|---|---|---|
| `RN-10` | La cédula (10 dígitos, validada con el algoritmo ecuatoriano) es única; los extranjeros usan pasaporte. | Domain |
| `RN-11` | Un paciente con citas no se elimina: se desactiva. | Application |
| `RN-12` | Un paciente menor de edad requiere representante con nombre y teléfono. | Domain |

## Clínico (`RN-13` a `RN-15`)

| Código | Regla | Se valida en |
|---|---|---|
| `RN-13` | El odontograma usa numeración FDI (11–48 permanentes, 51–85 temporales) y estado por cara: vestibular, lingual o palatina, mesial, distal, oclusal o incisal. | Domain |
| `RN-14` | Cada cambio del odontograma se guarda con fecha y odontólogo; no se sobrescribe el anterior. | Domain |
| `RN-15` | Las notas clínicas solo las ven el odontólogo y el administrador. | Application |

## Catálogo (`RN-16`)

| Código | Regla | Se valida en |
|---|---|---|
| `RN-16` | Un procedimiento tiene duración mayor a cero y precio mayor o igual a cero; cambiar el precio no altera citas ni presupuestos ya creados. | Domain |

## Tratamientos (`RN-17`)

| Código | Regla | Se valida en |
|---|---|---|
| `RN-17` | Cada ítem del plan es Pendiente, En curso o Realizado y conserva el precio que tenía al crearse. | Domain |

## Cobros (`RN-18` a `RN-19`)

| Código | Regla | Se valida en |
|---|---|---|
| `RN-18` | Un abono no puede superar el saldo; saldo = total del plan menos abonos. | Domain |
| `RN-19` | Anular un abono requiere rol administrador y queda registrado con fecha y motivo. | Application |
