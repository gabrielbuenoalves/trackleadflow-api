# Banco de dados

Um SQL Server. Sete schemas, um por módulo. Identificador de outro módulo é `uniqueidentifier` copiado, sem chave estrangeira e sem view que junte schemas. O painel continua compondo cada dono pelo próprio endpoint.

O schema de identidade se chama `[identity]` porque `IDENTITY` é palavra reservada.

## Convenções

| Regra | Escolha |
| --- | --- |
| Chave primária | `Id uniqueidentifier`, gerado pela aplicação (UUID v7) |
| Tenant | `TenantId uniqueidentifier` em toda tabela de negócio |
| Instante | `datetimeoffset(7)` |
| Dinheiro | `decimal(19,4)` + `char(3)` com o código da moeda |
| Status | `nvarchar(32)` com os valores listados na tabela |
| Texto longo e JSON | `nvarchar(max)`; JSON válido quando a coluna é configuração |
| Concorrência | `rowversion` nos agregados que recebem webhook ou edição concorrente |
| Segredo | fora destas tabelas. Aqui fica só `SecretReference`, ponteiro do cofre |

Toda tabela de negócio mutável tem `CreatedAt` e `UpdatedAt`. Tabela de fato ocorrido (mensagem, resposta, nota, aceite, tentativa, entrega, recibo de webhook) tem `CreatedAt` ou o instante do ocorrido, sem `UpdatedAt`.

## Tabelas técnicas, repetidas no schema de quem precisa

### OutboxMessages

Em todos os schemas que publicam fato: `[identity]` não publica fato de funil; os outros seis publicam. Identity fica sem outbox até existir um fato público.

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK. Igual ao id do fato |
| TenantId | uniqueidentifier | |
| Type | nvarchar(200) | Nome do fato (`LeadQualified`, `PaymentConfirmed`, …) |
| Payload | nvarchar(max) | Corpo do fato, já com os dados de correspondência |
| OccurredAt | datetimeoffset | |
| ProcessedAt | datetimeoffset, nulo | Preenchido depois do despacho |
| Attempts | int | |
| Error | nvarchar(2000), nulo | |

Índice filtrado em `(OccurredAt)` onde `ProcessedAt` é nulo.

O insert do estado e o insert da outbox entram na mesma transação.

### InboxMessages

Em quem reage a fato de outro módulo: `qualification`, `followup`, `attendance`, `marketing`.

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| EventId | uniqueidentifier | Id do fato de origem |
| Type | nvarchar(200) | |
| ProcessedAt | datetimeoffset | |

Único `(EventId, Type)`. O handler grava a inbox na mesma transação da mudança local. Fato repetido encontra a linha e para.

### WebhookReceipts

Em `whatsapp` e `payments`.

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| Provider | nvarchar(64) | `WhatsAppCloud` ou o nome do gateway |
| ProviderEventId | nvarchar(128) | Id do evento no provedor |
| ReceivedAt | datetimeoffset | |

Único `(Provider, ProviderEventId)`.

## [identity]

Dono da organização, do usuário e do papel.

### Tenants

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK. Este valor é o `TenantId` do restante do banco |
| Name | nvarchar(200) | Nome da operação |
| Status | nvarchar(32) | `Active`, `Suspended` |
| CreatedAt, UpdatedAt | datetimeoffset | |
| RowVersion | rowversion | |

### Users

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK. Copiado como especialista nos outros módulos |
| TenantId | uniqueidentifier | FK para `Tenants` |
| Email | nvarchar(320) | |
| DisplayName | nvarchar(200) | |
| PasswordHash | nvarchar(500) | Hash. A senha em claro não é armazenada |
| Status | nvarchar(32) | `Active`, `Disabled` |
| CreatedAt, UpdatedAt | datetimeoffset | |
| RowVersion | rowversion | |

Único `(TenantId, Email)`.

### UserRoles

| Coluna | Tipo | Papel |
| --- | --- | --- |
| UserId | uniqueidentifier | FK para `Users`. PK composta |
| Role | nvarchar(32) | `Admin`, `Specialist`. PK composta |

## whatsapp

Dono da conta da Cloud API, da sessão, da mensagem, do template aprovado e do clique do anúncio. O adaptador em `External/CloudApi` é a única saída para o WhatsApp.

### ChannelAccounts

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| TenantId | uniqueidentifier | |
| PhoneNumberId | nvarchar(32) | Id do número na Cloud API. Entra na URL de envio |
| WabaId | nvarchar(32) | Conta WhatsApp Business. Serve à listagem de templates |
| DisplayPhone | nvarchar(20) | Número que o painel mostra |
| TokenSecretReference | nvarchar(200) | Ponteiro do token da Cloud API no cofre |
| AppSecretReference | nvarchar(200) | Ponteiro do segredo usado para validar a assinatura do webhook |
| IsActive | bit | |
| CreatedAt, UpdatedAt | datetimeoffset | |
| RowVersion | rowversion | |

Único filtrado `(TenantId)` onde `IsActive = 1`.

### MessageTemplates

Cópia dos modelos aprovados, lida da Cloud API, para o follow-up escolher um template quando a janela de 24 horas fechou.

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| TenantId | uniqueidentifier | |
| Name | nvarchar(200) | Nome do template na Meta |
| Language | nvarchar(16) | |
| Status | nvarchar(32) | `Approved`, `Pending`, `Rejected` |
| Body | nvarchar(max) | Texto exibido no painel |
| SyncedAt | datetimeoffset | |

Único `(TenantId, Name, Language)`.

### Conversations

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| TenantId | uniqueidentifier | |
| Phone | nvarchar(20) | E.164 |
| ContactDisplayName | nvarchar(200), nulo | Nome enviado no webhook da Cloud API |
| Status | nvarchar(32) | `Open`, `Closed` |
| ExternalThreadId | nvarchar(128), nulo | Identificador da conversa na Cloud API, quando existir |
| CtwaClid | nvarchar(256), nulo | Clique do anúncio para a Conversions API |
| Fbclid | nvarchar(256), nulo | Clique alternativo, quando o webhook da Cloud API trouxer este campo |
| SourceCampaign | nvarchar(200), nulo | |
| SourceAd | nvarchar(200), nulo | |
| StartedAt | datetimeoffset | |
| LastInboundAt | datetimeoffset, nulo | |
| ContactStartedAt | datetimeoffset, nulo | Preenchido uma vez, quando `ContactStarted` entra na outbox |
| CreatedAt, UpdatedAt | datetimeoffset | |
| RowVersion | rowversion | |

Único filtrado `(TenantId, Phone)` onde `Status = Open`. Índice `(TenantId, ExternalThreadId)`.

### Messages

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| TenantId | uniqueidentifier | |
| ConversationId | uniqueidentifier | FK para `Conversations` |
| Direction | nvarchar(16) | `Inbound`, `Outbound` |
| Body | nvarchar(max), nulo | Texto entregue ou recebido |
| TemplateKey | nvarchar(200), nulo | |
| ExternalMessageId | nvarchar(128), nulo | `wamid` da Cloud API |
| RequestedBy | nvarchar(32), nulo | `Qualification`, `FollowUp`, `Attendance`. Nulo na entrada |
| OccurredAt | datetimeoffset | |

Único filtrado `(TenantId, ExternalMessageId)` onde o id externo existe. Índice `(ConversationId, OccurredAt)`.

## qualification

Dono do roteiro, das respostas, do nome, do documento e do status da triagem.

Nome e documento não são colunas fixas de nicho. O roteiro declara o campo; o lead guarda o valor. `DisplayName` e `DocumentNumber` são a projeção dos papéis semânticos `Name` e `Document`, para a lista do painel.

### CriteriaSets

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| TenantId | uniqueidentifier | |
| Name | nvarchar(200) | |
| Version | int | Sobe quando o admin publica outro roteiro |
| IsActive | bit | |
| CreatedAt, UpdatedAt | datetimeoffset | |
| RowVersion | rowversion | |

Único filtrado `(TenantId)` onde `IsActive = 1`.

Um conjunto que já tem lead é imutável. Publicar mudança cria outra versão e desativa a anterior. O lead permanece na versão com que começou.

### Steps

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| CriteriaSetId | uniqueidentifier | FK para `CriteriaSets` |
| SortOrder | int | |
| Kind | nvarchar(32) | `Question`, `Capture`, `Decision` |
| Prompt | nvarchar(1000) | Texto da pergunta |
| ConfigJson | nvarchar(max) | Respostas que avançam, ramo de desqualificação, campo capturado |

Único `(CriteriaSetId, SortOrder)`.

`Decision` descreve o ramo no JSON: respostas aceitas, e a rejeição que desqualifica com um motivo. `Capture` aponta o `FieldKey`.

### CaptureFields

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| CriteriaSetId | uniqueidentifier | FK para `CriteriaSets` |
| FieldKey | nvarchar(64) | `name`, `document`, ou chave livre do nicho |
| Label | nvarchar(200) | Rótulo no painel |
| SemanticRole | nvarchar(32) | `Name`, `Document`, `None` |
| Required | bit | |
| SortOrder | int | |

Único `(CriteriaSetId, FieldKey)`. Único filtrado `(CriteriaSetId, SemanticRole)` onde o papel é `Name` ou `Document`.

### Leads

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| TenantId | uniqueidentifier | |
| ConversationId | uniqueidentifier | Copiado de `whatsapp`. Sem FK |
| CriteriaSetId | uniqueidentifier | FK para `CriteriaSets` |
| CurrentStepId | uniqueidentifier, nulo | FK para `Steps` |
| Status | nvarchar(32) | `InTriage`, `AwaitingReply`, `Qualified`, `Disqualified` |
| DisplayName | nvarchar(200), nulo | Projeção do campo com papel `Name` |
| DocumentNumber | nvarchar(64), nulo | Projeção do campo com papel `Document` |
| DisqualifyReason | nvarchar(500), nulo | |
| QualifiedAt | datetimeoffset, nulo | |
| DisqualifiedAt | datetimeoffset, nulo | |
| CreatedAt, UpdatedAt | datetimeoffset | |
| RowVersion | rowversion | |

Único `(TenantId, ConversationId)`. Índice `(TenantId, Status, CreatedAt)`.

`AwaitingReply` é o estado que publica `ReplyExpected`. `FollowUpExhausted` não muda esta linha: a triagem continua aguardando, e o painel lê o esgotamento em `followup`.

### LeadAnswers

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| LeadId | uniqueidentifier | FK para `Leads` |
| StepId | uniqueidentifier | FK para `Steps` |
| RawValue | nvarchar(max) | |
| AnsweredAt | datetimeoffset | |

Único `(LeadId, StepId)`. Nova resposta no mesmo passo atualiza a linha.

### LeadFieldValues

| Coluna | Tipo | Papel |
| --- | --- | --- |
| LeadId | uniqueidentifier | FK. PK composta |
| FieldKey | nvarchar(64) | PK composta |
| Value | nvarchar(500) | |

Na mesma transação, papel `Name` atualiza `Leads.DisplayName` e papel `Document` atualiza `Leads.DocumentNumber`.

## followup

Dono do modelo da sequência e da agenda da triagem que está esperando resposta.

### Sequences

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| TenantId | uniqueidentifier | |
| Name | nvarchar(200) | |
| IsActive | bit | |
| CreatedAt, UpdatedAt | datetimeoffset | |
| RowVersion | rowversion | |

Único filtrado `(TenantId)` onde `IsActive = 1`.

### SequenceSteps

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| SequenceId | uniqueidentifier | FK para `Sequences` |
| SortOrder | int | |
| DelayMinutes | int | Espera antes deste envio, contada do passo anterior. No primeiro, contada de `ReplyExpected` |
| Body | nvarchar(max), nulo | Texto de sessão, usado dentro da janela de 24 horas |
| TemplateName | nvarchar(200), nulo | Nome de um template aprovado na Cloud API. Obrigatório quando o passo pode cair fora da janela |

Único `(SequenceId, SortOrder)`.

No envio, `SendOutboundMessage` leva `Body` e `TemplateName`. O adaptador da Cloud API olha a última mensagem recebida daquela conversa. Dentro de 24 horas, envia o texto. Fora delas, envia o template. Fora delas e sem template, a tentativa fica `Skipped`.

### Cases

Uma linha por triagem que passou a esperar resposta.

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| TenantId | uniqueidentifier | |
| LeadId | uniqueidentifier | Copiado de `qualification` |
| ConversationId | uniqueidentifier | Copiado de `whatsapp` |
| SequenceId | uniqueidentifier | FK para `Sequences` |
| Status | nvarchar(32) | `Armed`, `Cancelled`, `Exhausted` |
| NextStepOrder | int | |
| NextRunAt | datetimeoffset, nulo | Nulo quando a agenda parou |
| CreatedAt, UpdatedAt | datetimeoffset | |
| RowVersion | rowversion | |

Único filtrado `(TenantId, LeadId)` onde `Status = Armed`. Índice filtrado `(NextRunAt)` onde `Status = Armed`.

Mensagem recebida, `LeadQualified` ou `LeadDisqualified` passam a linha para `Cancelled`. A agenda que chega ao fim sem resposta passa para `Exhausted` e publica `FollowUpExhausted`.

### Attempts

Foto dos passos no momento em que o caso é armado. Editar a sequência depois não altera um caso em andamento.

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| CaseId | uniqueidentifier | FK para `Cases` |
| SortOrder | int | |
| Body | nvarchar(max), nulo | Texto de sessão congelado |
| TemplateName | nvarchar(200), nulo | Template aprovado congelado |
| ScheduledAt | datetimeoffset | |
| SentAt | datetimeoffset, nulo | |
| Status | nvarchar(32) | `Pending`, `Sent`, `Skipped` |

Único `(CaseId, SortOrder)`.

## attendance

Dono da fila, da nota e do aceite comercial. A transcrição continua em `whatsapp`. A cobrança continua em `payments`.

### WorkItems

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| TenantId | uniqueidentifier | |
| LeadId | uniqueidentifier | Copiado. Um item por lead |
| ConversationId | uniqueidentifier | Copiado |
| Status | nvarchar(32) | `Queued`, `Assigned`, `Closed` |
| SpecialistId | uniqueidentifier, nulo | Copiado de `[identity].Users` |
| AssignedAt | datetimeoffset, nulo | |
| ClosedAt | datetimeoffset, nulo | |
| CreatedAt, UpdatedAt | datetimeoffset | |
| RowVersion | rowversion | |

Único `(TenantId, LeadId)`. Índice `(TenantId, Status, CreatedAt)`.

Nasce em `LeadQualified`, já em `Queued`.

### Notes

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| WorkItemId | uniqueidentifier | FK para `WorkItems` |
| AuthorId | uniqueidentifier | Copiado de `[identity].Users` |
| Body | nvarchar(max) | |
| CreatedAt | datetimeoffset | |

Índice `(WorkItemId, CreatedAt)`.

### Acceptances

Um aceite por item comercial. Análise e contrato são duas linhas, com rótulos definidos pelo tenant.

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| WorkItemId | uniqueidentifier | FK para `WorkItems` |
| SpecialistId | uniqueidentifier | |
| Label | nvarchar(200) | Rótulo do tenant. Viaja no fato de pagamento |
| Amount | decimal(19,4) | |
| Currency | char(3) | |
| AcceptedAt | datetimeoffset | Instante de `SaleAccepted` |
| ChargeId | uniqueidentifier, nulo | Preenchido com o retorno de `CreateCharge` |
| PaymentStatus | nvarchar(32) | `Pending`, `Paid`, `Failed` |

Índice `(ChargeId)`. `PaymentConfirmed` e `PaymentFailed` localizam a linha por esse id e atualizam `PaymentStatus`.

## payments

Dono da conta do gateway e da cobrança. Confirmação nasce do webhook com assinatura válida e recibo ainda não visto.

### GatewayAccounts

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| TenantId | uniqueidentifier | |
| Provider | nvarchar(64) | Nome do adaptador |
| SecretReference | nvarchar(200) | Ponteiro do cofre. A credencial não fica nesta coluna |
| IsActive | bit | |
| CreatedAt, UpdatedAt | datetimeoffset | |
| RowVersion | rowversion | |

Único filtrado `(TenantId)` onde `IsActive = 1`.

### Charges

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| TenantId | uniqueidentifier | |
| LeadId | uniqueidentifier | Copiado |
| Label | nvarchar(200) | Mesmo rótulo do aceite |
| Amount | decimal(19,4) | |
| Currency | char(3) | |
| Status | nvarchar(32) | `Pending`, `Paid`, `Failed`, `Expired` |
| Provider | nvarchar(64) | |
| ExternalChargeId | nvarchar(128), nulo | Id no gateway |
| CheckoutReference | nvarchar(500), nulo | Link ou copia-e-cola devolvido na criação. É o que o atendimento envia no canal |
| CreatedAt | datetimeoffset | |
| PaidAt | datetimeoffset, nulo | |
| UpdatedAt | datetimeoffset | |
| RowVersion | rowversion | |

Único filtrado `(Provider, ExternalChargeId)` onde o id externo existe. Índice `(TenantId, LeadId)` e `(TenantId, Status)`.

`Paid` só é gravado no webhook aceito. Essa transição publica `PaymentConfirmed`.

## marketing

Dono da conexão com a Meta e do log de envio. O log não guarda telefone, nome, documento nem token.

### Connections

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| TenantId | uniqueidentifier | |
| DatasetId | nvarchar(64) | `idmeta` do dataset ligado à conta WhatsApp Business, não o pixel do site. O painel pode reler este valor |
| WabaId | nvarchar(32) | Id numérico da conta WhatsApp Business. Vai em todo evento |
| TokenSecretReference | nvarchar(200) | Ponteiro do cofre. O token não volta para o painel |
| ContactEventName | nvarchar(64), nulo | Nulo por padrão: Fez Contato não é enviado. `Contact` a Meta recusa neste canal |
| QualifiedEventName | nvarchar(64) | Padrão `LeadSubmitted` |
| PurchaseEventName | nvarchar(64) | Padrão `Purchase` |
| PurchaseChargeLabel | nvarchar(200), nulo | Quando preenchido, só cobrança com este rótulo emite Comprou. Quando nulo, toda `PaymentConfirmed` emite |
| IsActive | bit | |
| CreatedAt, UpdatedAt | datetimeoffset | |
| RowVersion | rowversion | |

Único `(TenantId)`.

### Attributions

O clique do anúncio chega na conversa e a compra acontece dias depois, no módulo de pagamentos. Esta tabela guarda o `ctwa_clid` para o Marketing reutilizar sem ler o banco do WhatsApp.

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| TenantId | uniqueidentifier | |
| ConversationId | uniqueidentifier | Copiado |
| LeadId | uniqueidentifier, nulo | Preenchido quando `LeadQualified` chega, pelo `ConversationId` |
| CtwaClid | nvarchar(512) | Valor cru. A Meta rejeita clique inventado e rejeita este campo hasheado |
| SourceAd | nvarchar(200), nulo | Id do anúncio, quando o webhook trouxer |
| CreatedAt | datetimeoffset | |

Único `(TenantId, ConversationId)`.

### Deliveries

| Coluna | Tipo | Papel |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| TenantId | uniqueidentifier | |
| BusinessEvent | nvarchar(32) | `ContactStarted`, `LeadQualified`, `PaymentConfirmed` |
| SourceId | uniqueidentifier | Conversa, lead ou cobrança, conforme o fato |
| MetaEventName | nvarchar(64) | Nome efetivamente enviado |
| Status | nvarchar(32) | `Pending`, `Sent`, `Skipped`, `Failed` |
| SkipReason | nvarchar(200), nulo | Exemplo: clique do anúncio ausente |
| ResponseCode | int, nulo | Código HTTP da Meta |
| AttemptCount | int | |
| CreatedAt | datetimeoffset | |
| SentAt | datetimeoffset, nulo | |

Único `(TenantId, BusinessEvent, SourceId)`.

`ContactStarted` usa a conversa como `SourceId` e grava `Attributions`. O envio à Meta desse marco fica `Skipped`, com motivo de evento não usado neste canal. `LeadQualified` usa o lead e preenche `Attributions.LeadId`. `PaymentConfirmed` usa a cobrança, acha o clique pelo lead e envia `Purchase` com o valor e a moeda da cobrança. O `ctwa_clid` sai cru. Telefone e documento não entram no log. Se um dia o evento levar telefone ou e-mail, o hash fica só na chamada e não nesta tabela.

## Ids que atravessam a fronteira

| Id | Nasce em | Copiado para |
| --- | --- | --- |
| TenantId | `[identity].Tenants` | Todas as tabelas de negócio |
| ConversationId | `whatsapp.Conversations` | `qualification.Leads`, `followup.Cases`, `attendance.WorkItems`, `marketing.Attributions` |
| LeadId | `qualification.Leads` | `followup.Cases`, `attendance.WorkItems`, `payments.Charges`, `marketing.Attributions` |
| ChargeId | `payments.Charges` | `attendance.Acceptances` |
| SpecialistId / AuthorId | `[identity].Users` | `attendance.WorkItems`, `Notes`, `Acceptances` |

## Invariantes

1. Chave estrangeira só dentro do mesmo schema.
2. Uma conversa aberta por tenant e telefone.
3. Um lead por conversa.
4. Um caso de follow-up armado por lead.
5. Um item de fila por lead qualificado.
6. Um roteiro ativo e uma sequência ativa por tenant.
7. Roteiro com lead já criado não é editado; a mudança nasce como nova versão.
8. Passos do follow-up em andamento são a foto gravada em `Attempts`.
9. Recibo de webhook e entrega de marketing são únicos pelo id de origem.
10. Token da Cloud API, segredo do app, token da Conversions API, credencial do gateway e senha em claro ficam fora destas tabelas. `PasswordHash` guarda só o hash. `SecretReference` guarda só o ponteiro.
11. `whatsapp.ChannelAccounts.WabaId` e `marketing.Connections.WabaId` repetem a mesma conta WhatsApp Business. O clique e o evento de anúncio precisam nascer dela.
