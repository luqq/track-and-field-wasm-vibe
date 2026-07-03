# Parámetros de ajuste de dificultad

Referencia de todas las constantes que gobiernan la curva de dificultad, dónde viven y
qué efecto tiene moverlas. Todas las velocidades están en **cm/s** y los tiempos en
segundos; la simulación corre a paso fijo de **60 Hz** (las cantidades "por frame" se
multiplican por 60 para pensarlas por segundo).

## 1. Modelo de carrera global (`Input.cs` → `RunMeter`)

El corazón de la dificultad de todo el juego. Cada flanco ascendente de cualquiera de
los dos botones de CARRERA suma `TapGain`; cada frame la fricción resta un porcentaje.

| Parámetro | Valor | Efecto |
|---|---|---|
| `TapGain` | 55 (jabalina: 78) | cm/s añadidos por pulsación. **La palanca principal**: subirlo hace más fácil TODO evento de carrera. |
| `Friction` | 0.008 | Fracción de velocidad perdida por frame (≈48 %/s). Subirla exige ritmo más sostenido. |
| `MaxSpeed` | 1700 | Techo absoluto del acumulador. |

**Fórmula de equilibrio** (velocidad estable para un ritmo de pulsación dado):

```
velocidad ≈ pulsaciones/s × TapGain / (Friction × 60)
         ≈ pulsaciones/s × TapGain × 2.08
```

Con los valores por defecto: 6 p/s → ~690 · 8 p/s → ~917 · 10 p/s → ~1146 · 13 p/s → ~1490.
`TapGain` se puede sobrescribir por evento (`_run.TapGain = ...` en su `Reset`), como hace
la jabalina.

## 1b. Dificultad seleccionable (`Settings.cs`)

El menú de opciones aplica tres multiplicadores globales encima de todo lo demás
(cada evento los lee en su `Reset`):

| Multiplicador | FÁCIL | NORMAL | DIFÍCIL | Se aplica a |
|---|---|---|---|---|
| `TimeF` | ×1.12 | ×1.0 | ×0.94 | Marcas de tiempo (100m, vallas) — más alto = más margen |
| `DistF` | ×0.85 | ×1.0 | ×1.08 | Marcas de distancia/altura (longitud, jabalina, martillo, altura) |
| `TapF` | ×1.25 | ×1.0 | ×0.88 | `TapGain` del `RunMeter` en todos los eventos de carrera |

## 2. Matriz de clasificación (`Reset(int match)` de cada evento)

La dificultad se congela en el Match 3 (`Game.MatchLevel()` capa a 3).

| Evento | Match 1 | Match 2 | Match ≥3 |
|---|---|---|---|
| 100m (`Dash100`) | ≤ 13.50 s | ≤ 10.50 s | ≤ 10.00 s |
| Longitud (`LongJump`) | ≥ 6.50 m | ≥ 8.50 m | ≥ 9.00 m |
| Jabalina (`Javelin`) | ≥ 70.00 m | ≥ 75.00 m | ≥ 80.00 m |
| 110m vallas (`Hurdles110`) | ≤ 14.00 s | ≤ 13.00 s | ≤ 12.50 s |
| Martillo (`Hammer`) | ≥ 75.00 m | ≥ 80.00 m | ≥ 85.00 m |
| Altura (`HighJump`) | ≥ 2.28 m | ≥ 2.35 m | ≥ 2.40 m |

## 3. 100m lisos (`EventsTrack.cs` → `Dash100`)

| Parámetro | Valor | Efecto |
|---|---|---|
| Ventana de pre-carga legal | `_phT > 0.12` | Pulsar en los últimos 0.12 s antes del disparo NO es salida falsa y pre-carga velocidad. Ampliarla premia más el riesgo. |
| Salidas falsas para DQ | 3 | — |
| Latencia de incorporación | `_standT = 0.30`, factor `0.4` | 0.3 s a velocidad efectiva ×0.4 al arrancar desde agachado. |
| Ritmo del rival | `qual × 0.99`, rampa `0.7` s | El rival acaba justo por debajo de la marca. Bajar el 0.99 lo hace más rápido (afecta al huevo del empate). |

Referencia: clasificar el Match 1 (13.50 s) exige mantener ~6.5 p/s; el Match 3 (10.00 s), ~8.7 p/s.

## 4. Salto de longitud (`EventsField.cs` → `LongJump`)

| Parámetro | Valor | Efecto |
|---|---|---|
| Pasillo / línea de batida | `FoulCm = 4500` | Más corto = menos tiempo para acumular velocidad. |
| Subida del ángulo | 1.5°/frame (90°/s) | Más lento = más fácil clavar 45°. |
| Bono de línea | gap ≤ 15 cm → **+100** | El "aumento colosal" por pisar la línea. |
| Tabla angular | 45°→+60 · 44/46°→+30 · 43/47°→+10 | `EventBase.AngleBonusCms`, compartida con martillo. |
| Compresión de velocidad | `vEff = 600 + 0.55·(v+bonos)` | El 0.55 comprime la horquilla: subirlo agranda la diferencia entre machacar bien y mal. |
| Alcance | `vEff²·sin2θ/981 × 0.5` | El ×0.5 va plegado en `_fvx`. Subirlo alarga todos los saltos por igual. |
| Fricción durante el plantado | `_run.Step(0)` en `Hold` | Aguantar el ajuste drena velocidad (~0.8 %/frame). |

Referencia: 6.50 m ≈ 950 cm/s con 45° y línea; 9.00 m ≈ 11 p/s + línea + 45° exactos.

## 5. Jabalina (`EventsField.cs` → `Javelin`)

| Parámetro | Valor | Efecto |
|---|---|---|
| `TapGain` propio | **78** | Hace alcanzable el tope con ~8 p/s. La palanca de dificultad del evento. |
| Tope de portador | 1300 | Histórico — no tocar si se quiere fidelidad. |
| Empuje del brazo | +330 fijos | Ídem. |
| Herencia | `floor(floor(m_prev)/2)` cm/s | Ídem (80 m → +40 al siguiente intento). |
| Subida del ángulo | 1.2°/frame, máx 88° | Más lento = puntería más fina. |
| Frenado al plantar | ×0.35 | Más bajo = menos riesgo de nulo cruzando la línea durante el ajuste. |
| Gravedad escalada | `GEff = 981/3.2` | El 3.2 es el multiplicador de alcance: con tope+brazo a 43° salen ~86.7 m. Subirlo acerca el rollover. |
| Deriva aerodinámica | `θ+2°` en el seno | Mueve el óptimo real a ~43° (compensa el bug del registro X). |
| Bug del registro X | bono angular **omitido** | Histórico — no restaurar. |
| Rollover | ≥ 100 m → resta 100 | Histórico. |
| Viento en vuelo | +1.5 cm/s de `vx` por pulsación | "Milímetros residuales" machacando con la jabalina en el aire. |
| Huevo del pájaro | ángulo ≥ 80° y portador ≥ 1300 | — |

Referencia: ~7 p/s + 41-45° ≈ 70 m (clasifica Match 1); a tope ≈ 86-90 m.

## 6. 110m vallas (`EventsTrack.cs` → `Hurdles110`)

| Parámetro | Valor | Efecto |
|---|---|---|
| Posición de vallas | `1372 + i·914`, 10 uds | Espaciado real de la prueba. |
| Duración del salto | `JumpDur = 0.46` s | — |
| Altura del salto | `220·n·(1−n)` (ápex 55 cm) | — |
| Umbral de choque | altura < **45** cm al cruzar | Bajarlo perdona saltos más justos de timing. |
| Castigo por choque | velocidad ×**0.10**, tropiezo 0.7 s | El "estado comatoso". Subir el 0.10 hace el choque menos terminal. |

## 7. Martillo (`EventsField2.cs` → `Hammer`)

| Parámetro | Valor | Efecto |
|---|---|---|
| Aceleración de giro | `min(2.6, 0.7 + revs·0.22)` rev/s | A 9 revoluciones gira a ~2.6 rev/s ≈ 15.6°/frame: ahí está la dificultad de clavar 45°. Bajar el techo 2.6 facilita la puntería a máxima potencia. |
| Velocidad de lanzamiento | `920 + min(revs,9)·70 + bono angular` | 6 revs→1340 · 7→1410 · 9→1550 (+60 con 45° exactos). |
| Sector válido | 5°–85° | Fuera → falta. |
| Mareo | > 11 revoluciones | Falta automática. |
| Gravedad escalada | `GEff = 981/3.6` | 9 revs + 45° ≈ 95 m; 7 revs + ~45° ≈ 77 m (Match 1). |

## 8. Salto de altura (`EventsField2.cs` → `HighJump`)

| Parámetro | Valor | Efecto |
|---|---|---|
| `LaunchV` | **880** | Impulso total. Su relación con `G` fija el ápex: `(880·sinθ)²/2800` ≈ 2.76 m a 85°. Subirlo es la forma más directa de facilitar el evento. |
| `G` | 1400 | Gravedad del evento (el resto del juego usa 981 o escaladas). |
| `ClawGain` | 12 | cm/s de `vy` devueltos por cada pulsación de carrera en el aire ("reptar"). Imprescindible por encima de ~2.35 m. |
| Caída del ángulo | −1.0°/frame desde 90°, mín 40° | Presión corta = 90° puro = choque seguro. |
| Zona de pulsado | desde `TakeoffCm−150` (5.10 m) | Dónde se puede iniciar el hold. |
| Despegue forzoso | `BarXCm − 15` | Si llegas ahí aún sosteniendo, salta solo. |
| Subida de listón | +0.03 m por altura superada | Ritmo de progresión dentro del evento. |
| Nulos totales | 3 | Fin del evento. |
| Corrupción visual | > 2.47 m huecos · > 2.56 m desmoronamiento | Estético (bug de VRAM), no afecta a la física. |

## 9. Economía de partida (`Game.cs`)

| Parámetro | Valor | Efecto |
|---|---|---|
| Vida extra | cada 100 000 pts | `_nextLifeAt`. |
| Tope de marcador | 9 999 990 | Histórico. |
| Huevos de pascua | +1000 pts | `TriggerEgg()`. |
| Puntos por evento | pista: `(qual−t)·2000+1000` · longitud: `(m−qual)·2000+1000` · lanzamientos: `(m−qual)·100+1000` · altura: `(m−qual)·4000+1000` | Redondeados a decenas. Suben el ritmo de vidas extra. |

## Recetario rápido

- **Todo más fácil**: `TapGain` 55 → 60-65 (una línea, afecta a 100m, vallas y longitud).
- **Un evento concreto más fácil**: sobrescribir `_run.TapGain` en su `Reset`, como la jabalina.
- **Lanzamientos más largos** sin tocar la carrera: subir el divisor de `GEff` (3.2 jabalina, 3.6 martillo) o el ×0.5 de la longitud.
- **Altura más permisiva**: `LaunchV` 880 → 900-920, o `ClawGain` 12 → 16.
- **Menos castigo en vallas**: umbral 45 → 40 cm y factor de choque 0.10 → 0.25.
- **Marcas de clasificación**: los `switch` sobre `match` en cada `Reset` — la tabla Centuri
  documentada arriba; cambiarlas rompe la fidelidad histórica pero es el ajuste más directo.
