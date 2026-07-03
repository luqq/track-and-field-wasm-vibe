# Track & Field (1983) — Clon arcade en .NET 10 WebAssembly

Réplica del clásico de Konami/Centuri conforme a la especificación arquitectónica: motor
100 % C# compilado a WASM (AOT en Release), resolución nativa 256×224 escalada por GPU
con `image-rendering: pixelated`, y JavaScript reducido al mínimo imprescindible.

## Ejecutar

```
dotnet run --project TrackAndField.csproj          # desarrollo (interpretado, arranque rápido)
dotnet publish -c Release                          # producción: RunAOTCompilation + trimming full
```

El puerto puede fijarse con `ASPNETCORE_URLS=http://127.0.0.1:5807` (el `firstPort` de
`runtimeconfig.template.json` no lo respeta el WasmAppHost actual).

## Controles

| Función | Teclas (por defecto) | Gamepad (por defecto) |
|---|---|---|
| CARRERA (2 botones no discriminados, como la placa original) | `Z` / `X` o `←` / `→` | A / B |
| ACCIÓN (salto / lanzamiento) | `Espacio` o `↑` | X / RB |
| START | `Enter` | Start |

Todas las teclas y botones de mando son **redefinibles** desde OPCIONES → REDEFINIR
TECLAS (los botones del mando aparecen como `PAD0`–`PAD15`). Los ajustes se persisten
en `localStorage`.

## Menú de opciones

- **Dificultad**: FÁCIL / NORMAL / DIFÍCIL — multiplica las marcas de clasificación y la
  ganancia por pulsación (ver TUNING.md §Dificultad seleccionable).
- **Idioma**: castellano, english, català (fuente con glifos Ñ y Ç).
- **Voz**: locutor local (`speechSynthesis`) que canta marcas, tiempos, nulos y veredictos
  en el idioma activo.
- **Modo 2 jugadores**: turnos alternos por prueba, marcadores y vidas independientes,
  camiseta roja (J1) y verde (J2); eliminación individual y fin de partida cuando no queda nadie.

## Sustituir los atletas vectoriales por sprites (por código)

El render pasa por la interfaz `IAthleteRenderer` (fachada estática `Athlete`). Para usar
sprites propios basta con asignar el renderizador al arrancar (p. ej. en `Program.cs`):

```csharp
Athlete.Renderer = new SpriteAthlete
{
    RunFrames = new[]
    {
        new[] { "...hh...", "...ss...", ".JJJJJ..", "..ss.s..", ".s...s.." }, // frame 1
        new[] { "...hh...", "...ss...", ".JJJJJ..", "...ss...", "..s.s..." }, // frame 2
    },
    // CrouchFrame, FlyFrame, ThrowFrame, FallenFrame... (las poses no definidas
    // caen automáticamente al esqueleto vectorial)
};
```

Caracteres: `J` = camiseta (se tiñe por jugador), `s` = piel, `h` = pelo, `w` = blanco,
`.` = transparente. Anclaje: centro-inferior en el punto (x, y) del suelo.

## Arquitectura

- **Framebuffer compartido**: `Gfx.Fb` es un `uint[256*224]` *pinned* (`GC.AllocateArray(pinned:true)`).
  C# renderiza todo por software; JS superpone un `Uint8ClampedArray` sobre la memoria lineal
  (puntero obtenido una sola vez vía `[JSExport] GetFrameBufferAddress`) y hace un único
  `putImageData` por fotograma. Cero serialización, cero alocaciones en el hot path.
- **Bucle**: `requestAnimationFrame` (V-Sync) → `Engine.Update(ts)` con paso fijo de 60 Hz
  y acumulador (máx. 4 pasos por frame, recorte de saltos al cambiar de pestaña).
- **Entrada**: flanco ascendente contabilizado en `Input.OnButton`; los dos botones de
  carrera suman al mismo acumulador (machacar uno solo rinde igual que alternar — fiel al original).
- **Audio**: `[JSImport]` → WebAudio, onda cuadrada monofónica + jingles secuenciados.
- **Velocidades**: internamente en cm/s con decaimiento por fricción (`RunMeter`), como el BCD original.

## Fidelidad histórica implementada

- **100m**: salidas falsas (3 → descalificación), ventana legal de pre-carga 0.12 s antes
  del disparo, latencia de incorporación desde la posición agachada, rival CPU.
- **Salto de longitud**: bono de batida sobre la línea blanca (+100 cm/s si ≤15 cm),
  tabla angular exacta: 45°→+60, 44/46°→+30, 43/47°→+10 cm/s.
- **Jabalina**: tope categórico de 1300 cm/s, empuje fijo del brazo +330 cm/s, herencia
  `floor(floor(metros_previos)/2)` cm/s, **bug del registro X** (el bono angular jamás se
  aplica; el óptimo real queda en 42–43°), **rollover >99.99 m** (100.12 → 0.12), viento
  residual machacando carrera con la jabalina en vuelo, huevo del pájaro a ≥80° a tope.
- **110m vallas**: intento único; el choque reduce la inercia al 10 % ("estado comatoso").
- **Martillo**: giro iniciado con una sola pulsación, 6–7 revoluciones bastan, máximo a la 9ª,
  mareo/falta pasada la 11ª, sector válido 5–85°, **bono angular íntegro** (sin bug).
- **Salto de altura**: aproximación automática, presión sostenida aplana el arco (90° puro
  = choque), **reptar en el aire** (los taps de carrera cancelan parcialmente la caída),
  pértiga con huecos >2.47 m y desmoronamiento >2.56 m (corrupción de VRAM).
- **Progresión**: Match 1/2/3 con la matriz Centuri (13.50/10.50/10.00 s, 6.50/8.50/9.00 m…),
  dificultad congelada a partir del Match 3, vida extra cada 100 000 puntos, tope 9 999 990.
- **Huevos de pascua** (+1000 pts, patrón pub/sub → overlay): empate perfecto en pista
  (Tutankham), marca idéntica ×3 en longitud, pájaro en jabalina, topo en altura tras
  dos nulos y un tercero limpio.

## Ajuste de dificultad

Todos los parámetros de dificultad (modelo de carrera, matriz de clasificación, físicas
por evento, castigos y economía de puntos) están documentados en [TUNING.md](TUNING.md),
con las fórmulas de equilibrio y un recetario de ajustes rápidos.

## Archivos

| Archivo | Contenido |
|---|---|
| `Program.cs` | Superficie `[JSExport]`: framebuffer, `Update`, botones |
| `Gfx.cs` / `Font.cs` | Render por software + fuente 5×7 |
| `Athlete.cs` | Atleta procedural articulado + sprites de props |
| `Input.cs` | Botonera con flancos + `RunMeter` (cm/s) |
| `Audio.cs` | Interop WebAudio |
| `Scene.cs` | Estadio, pista, campo, HUD |
| `EventsTrack.cs` | 100m y 110m vallas |
| `EventsField.cs` | Longitud y jabalina |
| `EventsField2.cs` | Martillo y altura |
| `Game.cs` | Máquina de estados, Match, puntuación, huevos |
| `wwwroot/main.js` | rAF, blit del framebuffer, teclado/gamepad, WebAudio |
