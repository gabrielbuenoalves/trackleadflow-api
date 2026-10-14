# Backend — monolito modular

Um processo .NET 10, um banco, um schema por módulo. Sem tabela compartilhada e sem chave estrangeira entre módulos. O nicho (critérios, campos, tipos de cobrança, textos) é configuração do tenant.

O host compõe. A regra de negócio mora no módulo dono do dado.

## Árvore da solução

O arquivo da solução é `TrackLeadFlow.slnx`, o formato que o SDK do .NET 10 gera. A solução inclui o módulo Operator e os testes da decisão da triagem, da rota, do espelho na Meta e da proposta.

```
TrackLeadFlow.slnx
Directory.Build.props
src/
  Host/
    TrackLeadFlow.Api/
      Composition/
  BuildingBlocks/
    TrackLeadFlow.SharedKernel/
      Domain/
      Events/
      Messaging/
      Results/
      Tenancy/
    TrackLeadFlow.Infrastructure/
      Messaging/
      Persistence/
      Security/
      Time/
  Modules/
    Identity/
    WhatsApp/
    Qualification/
    FollowUp/
    Attendance/
    Payments/
    Marketing/
tests/
  Architecture/
  Identity/
  WhatsApp/
  Qualification/
  FollowUp/
  Attendance/
  Payments/
  Marketing/
```

Cada módulo repete quatro projetos:

```
src/Modules/<Modulo>/
  TrackLeadFlow.Modules.<Modulo>.Domain/
    Aggregates/  Entities/  ValueObjects/  Events/  Repositories/
  TrackLeadFlow.Modules.<Modulo>.Application/
    Abstractions/  Commands/  Queries/  EventHandlers/
  TrackLeadFlow.Modules.<Modulo>.Infrastructure/
    Http/  Persistence/  Messaging/
    External/<Adaptador>/
  TrackLeadFlow.Modules.<Modulo>.Contracts/
    IntegrationEvents/  Commands/  Facades/
```

Adaptadores externos já separados:

- `WhatsApp` → `External/CloudApi`
- `Payments` → `External/Gateways`
- `Marketing` → `External/MetaCapi`

A pasta `Gateways` é a porta. O fornecedor concreto entra aí como adaptador.

## Responsabilidade

| Projeto | Guarda |
| --- | --- |
| SharedKernel | Tenant, evento e resultado. Sem regra de negócio. Não referencia módulo. |
| Infrastructure compartilhada | Outbox, barramento em processo, cofre de segredos, relógio. |
| Domain | Estado e eventos internos do módulo. |
| Application | Casos de uso e reação aos fatos externos. |
| Infrastructure do módulo | Banco, HTTP, webhook e adaptador externo. |
| Contracts | Fatos de integração e fachadas. Única superfície que outro módulo pode referenciar. |
| Host | Liga a implementação na fachada e sobe barramento e cofre. |

O endpoint e o webhook ficam em `Infrastructure/Http` do módulo dono. `Composition` no host só registra.

| Módulo | Dono de |
| --- | --- |
| Identity | Usuário, credencial, papéis Admin e Especialista. |
| WhatsApp | Sessão, mensagens, identificador de clique, conta da Cloud API, templates aprovados, webhook e envio. |
| Qualification | Critérios, respostas, nome, documento, status da triagem. |
| FollowUp | Sequência e agenda enquanto a triagem espera resposta. |
| Attendance | Fila, atribuição, notas, aceite comercial. |
| Payments | Cobrança, status financeiro, credencial do gateway, webhook assinado. |
| Marketing | Dataset do anúncio (`idmeta`), id da conta WhatsApp Business, token, atribuição do clique e log da Conversions API. |

## Regra de dependência

| Projeto | Referencia |
| --- | --- |
| Domain | SharedKernel |
| Contracts | SharedKernel |
| Application | próprio Domain, próprio Contracts, SharedKernel, Contracts alheios da matriz |
| Infrastructure do módulo | própria Application, Infrastructure compartilhada |
| Host | Infrastructure compartilhada e a Infrastructure de cada módulo |

| Application | Pode usar estes Contracts |
| --- | --- |
| Identity | — |
| WhatsApp | Identity |
| Operator | WhatsApp, Payments, Qualification, FollowUp, Marketing, Identity |
| Qualification | WhatsApp |
| FollowUp | WhatsApp, Qualification |
| Attendance | Qualification, WhatsApp, Payments, Identity |
| Payments | — |
| Marketing | WhatsApp, Qualification, Payments |

O sentido é sempre para o dono do fato. Payments não conhece Attendance. Marketing não conhece Attendance: o marco Comprou nasce em Payments.

## Comunicação

Dois canais, os dois definidos em `Contracts`.

**Fato.** Aconteceu. Vai para a outbox na mesma transação do estado. O despachante compartilhado publica no barramento em processo. O handler do outro módulo vive na Application de quem escuta. Evento de domínio interno não atravessa o módulo.

**Fachada.** Pedido com resposta imediata. A interface está no `Contracts` do dono; a implementação, na Infrastructure do dono; o registro, no host.

O painel lê cada módulo pelo endpoint daquele módulo. Não há repositório cruzado.

| Fato | Publica | Reage |
| --- | --- | --- |
| `ContactStarted` | WhatsApp | Marketing guarda o `ctwa_clid` e envia `LeadSubmitted` quando o clique existe |
| `InboundMessageReceived` | WhatsApp, só telefone de lead | Qualification retoma a triagem; FollowUp cancela a sequência |
| `OperatorMessageReceived` | WhatsApp, telefone de especialista | Operator atende o pedido. Qualification não vê essa mensagem |
| `LeadMarkedQualified` | Qualification | Marketing envia `QualifiedLead` uma vez. A fila não abre |
| `ReplyExpected` | Qualification | FollowUp arma a sequência. Só com o lead já qualificado e ainda fora da fila |
| `LeadQualified` | Qualification | Attendance abre a fila; FollowUp encerra; a IA para. Não há outro evento na Meta |
| `LeadDisqualified` | Qualification | FollowUp encerra. Não há evento na Meta |
| `FollowUpExhausted` | FollowUp | Consulta do painel. Fora da fila de vendas |
| `SaleAccepted` | Attendance | Registro do aceite. Não é compra para a Meta |
| `ChargeCreated` | Payments | Consulta do painel |
| `PaymentConfirmed` | Payments | Attendance dá baixa; Marketing → Comprou |
| `PaymentFailed` | Payments | Attendance mantém a cobrança em aberto |

Fachadas públicas:

- WhatsApp `SendOutboundMessage` — triagem, follow-up e atendimento. Leva texto e, quando houver, o nome do template. O adaptador escolhe texto dentro da janela de 24 horas e template fora dela.
- Payments `CreateCharge` — atendimento, no aceite de um item comercial. Várias cobranças por lead; o tipo é rótulo do tenant.
- Identity — especialista autenticado, para a atribuição da fila.

`ContactStarted` carrega o `ctwa_clid`. `LeadQualified` carrega conversa e lead. `PaymentConfirmed` carrega lead, cobrança, rótulo, valor e moeda. O Marketing copia o clique para `Attributions`. O `ctwa_clid` sai cru. Telefone ou e-mail, se forem enviados, saem em SHA-256. O log guarda marco, origem e código de resposta.

| Marco | Evento Meta padrão (configurável por tenant) |
| --- | --- |
| Contato realizado | `LeadSubmitted`, uma vez, se houver `ctwa_clid`. O nome `Contact` é recusado neste canal |
| Lead qualificado | `QualifiedLead`, uma vez, se houver `ctwa_clid` |
| Desqualificado | nenhum envio |
| Compra | `Purchase`, uma vez por cobrança confirmada, com valor e moeda |

Comprou acompanha `PaymentConfirmed`. O follow-up automático vale para a espera da triagem; a conversa com o especialista não entra nessa sequência.

## APIs externas

| Módulo | Pasta | API |
| --- | --- | --- |
| WhatsApp | `External/CloudApi` | WhatsApp Cloud API oficial: webhook, `/{phone-number-id}/messages` e templates da conta |
| Marketing | `External/MetaCapi` | Conversions API oficial: `/{dataset-id}/events` no dataset da conta WhatsApp Business |
| Payments | `External/Gateways` | Gateway de pagamento. Única integração que não é da Meta |

## Segredos

O cofre guarda o token da Cloud API, o segredo do app do webhook do WhatsApp, o token da Conversions API e a credencial do gateway. A leitura de API não devolve token. Os dois webhooks, o da Cloud API e o do gateway, só mudam estado com assinatura válida. O WABA ID da conta WhatsApp e o da conexão de anúncio são a mesma conta.

## Banco

O desenho das tabelas, um schema por módulo, está em [banco-de-dados.md](banco-de-dados.md).

## Testes

As pastas em `tests/` marcam o lugar. O projeto de teste de um módulo entra junto com o primeiro caso de uso, antes da implementação. `tests/Architecture` é o lugar da verificação da matriz de dependência.
