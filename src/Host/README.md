# Host

Raiz de composição. `TrackLeadFlow.Api/Composition` registra módulos, barramento e cofre.

O HTTP de cada área fica em `Infrastructure/Http` do módulo dono. O host chama esse registro e não concentra regra de negócio.
