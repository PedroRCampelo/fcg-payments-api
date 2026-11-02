# fcg-payments-api

Serviço de pagamentos da plataforma FIAP Cloud Games (FCG). Ele recebe os pedidos abertos pelo Catalog, decide se o pagamento é aprovado ou recusado, guarda o resultado e avisa os outros serviços. O pagamento é **simulado**: não existe gateway nem cartão, a decisão sai de uma regra simples de limite de preço.

Faz parte da Fase 2 do Tech Challenge, em que o monólito da Fase 1 foi dividido em quatro microsserviços. A visão geral da arquitetura, os fluxos de evento e as instruções para subir tudo juntos estão no [fcg-orchestration](https://github.com/PedroRCampelo/fcg-orchestration). Este README cobre só o que é específico deste serviço.

## O que o serviço faz

- Consome o `OrderPlacedEvent` e **aprova o pagamento se `Price` for menor ou igual ao limite configurado** (200 por padrão), ou recusa caso contrário. Exatamente no limite, aprova: 200,00 é aprovado e 200,01 é recusado.
- Grava um `Pagamento` com o resultado e publica o `PaymentProcessedEvent`.
- Oferece uma consulta dos pagamentos processados, restrita a administradores.

Banco: PostgreSQL, database `fcg_payments`. As migrações do EF Core são aplicadas sozinhas na subida.

## Eventos

| Direção | Evento | Por onde trafega | Quando |
|---|---|---|---|
| Consome | `OrderPlacedEvent` (`OrderId`, `UserId`, `GameId`, `Titulo`, `Price`) | fila `payments.order-placed` | Quando o Catalog abre um pedido |
| Publica | `PaymentProcessedEvent` (`OrderId`, `UserId`, `GameId`, `Titulo`, `Status`) | exchange `FCG.Contracts:PaymentProcessedEvent`, filas `catalog.payment-processed` e `notifications.payment-processed` | Depois de decidir sobre o pagamento |

O `Status` é `Approved` ou `Rejected` e vai como texto no JSON.

Na subida, o Payments declara no RabbitMQ as filas do Catalog e do Notifications e as liga à exchange do `PaymentProcessedEvent`. Assim o resultado de um pagamento não se perde se um dos dois ainda não tiver subido: a mensagem espera na fila.

**Idempotência pelo `OrderId`.** A entrega é "pelo menos uma vez", então o mesmo pedido pode chegar duas vezes. Se já existir um pagamento para o `OrderId`, a mensagem é descartada, sem reprocessar e sem publicar de novo, com um log de aviso explicando o motivo. O banco reforça isso com um índice único em `PedidoId`, que cobre o caso de duas mensagens do mesmo pedido chegando ao mesmo tempo.

**Outbox no consumidor.** Este serviço publica o resultado de dentro de um consumidor, então o outbox também está no consumidor (inbox e outbox do Entity Framework): o `Pagamento` e o `PaymentProcessedEvent` são gravados na mesma transação, e só depois de confirmada a transação o evento é entregue ao broker. Se a gravação falhar, nenhum evento sai. Foi verificado forçando uma falha no `INSERT` com um gatilho no banco: nada foi publicado, o pedido continuou `Pendente` e a mensagem foi para a fila `payments.order-placed_error`.

Eventos inválidos (campos vazios, preço negativo) não são repetidos: vão direto para a fila `payments.order-placed_error`.

## Endpoints

Rodando com `dotnet run`, o serviço escuta na porta 5103; dentro do container, na 8080. O Swagger está em `/swagger`.

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/pagamentos` | Administrador | Lista os pagamentos processados (pedido, usuário, jogo, preço, status e data). |
| GET | `/health` | Anônimo | Saúde completa, inclui a conexão com o RabbitMQ. Serve de readiness. |
| GET | `/health/live` | Anônimo | Só confirma que o processo responde. Serve de liveness. |

Não há endpoint para criar pagamento: todo pagamento nasce de um `OrderPlacedEvent`. O token é o emitido pelo fcg-users-api, e o papel `Administrador` vem da claim `role`.

## Variáveis de ambiente

No .NET, `__` (dois sublinhados) separa os níveis da configuração: `RabbitMq__Host` é a chave `RabbitMq:Host` do `appsettings.json`.

| Variável | Descrição | Obrigatória | Padrão |
|---|---|---|---|
| `ConnectionStrings__Default` | Connection string do PostgreSQL (database `fcg_payments`) | Sim¹ | Só em Development: `Host=localhost;Port=5435;Database=fcg_payments;Username=fcg;Password=fcg_password` |
| `JWT__SIGNINGKEY` | Chave com que o token é validado, mínimo de 32 bytes. **Tem que ser a mesma do fcg-users-api** | Sim | Nenhum, de propósito. Sem ela o serviço não sobe |
| `Jwt__Issuer` | Emissor esperado no token | Não | `fcg-users-api` |
| `Jwt__Audience` | Audiência esperada no token | Não | `fcg` |
| `Payments__LimiteDeAprovacao` | Limite de aprovação: aprova se `Price` for menor ou igual a ele. Número com **ponto** decimal (`200`, `199.90`) | Não | `200` |
| `RabbitMq__Host` | Host do RabbitMQ | Não | `localhost` |
| `RabbitMq__Port` | Porta AMQP | Não | `5672` |
| `RabbitMq__VirtualHost` | Virtual host | Não | `/` |
| `RabbitMq__Username` | Usuário do RabbitMQ | Sim¹ | Só em Development: `fcg` |
| `RabbitMq__Password` | Senha do RabbitMQ | Sim¹ | Só em Development: `fcg_password` |
| `RabbitMq__Queues__PaymentsOrderPlaced` | Fila de entrada, com o `OrderPlacedEvent` | Não | `payments.order-placed` |
| `RabbitMq__Queues__CatalogPaymentProcessed` | Fila do Catalog para o `PaymentProcessedEvent` (o Payments a declara). Igual à do Catalog | Não | `catalog.payment-processed` |
| `RabbitMq__Queues__NotificationsPaymentProcessed` | Fila do Notifications para o `PaymentProcessedEvent` (o Payments a declara). Igual à do Notifications | Não | `notifications.payment-processed` |
| `ASPNETCORE_ENVIRONMENT` | Em `Development` o serviço lê o `appsettings.Development.json` | Não | `Development` no `dotnet run`, `Production` na imagem |

¹ Obrigatória em qualquer ambiente que não seja o Development. Rodando com `dotnet run`, o `appsettings.Development.json` já preenche com os valores do PostgreSQL e do RabbitMQ do compose.

Um limite inválido (`abc`), negativo ou com vírgula (`200,50`) impede o serviço de subir, com a explicação no log e código de saída 1. A vírgula é recusada de propósito: ela seria lida como separador de milhar e viraria 20050, aprovando quase tudo.

## Como rodar isolado

Pré-requisitos: .NET 8 SDK e Docker.

O Payments precisa de um PostgreSQL e de um RabbitMQ. O jeito mais simples é subir só os dois com o compose do orquestrador (o [fcg-orchestration](https://github.com/PedroRCampelo/fcg-orchestration) deve estar clonado ao lado deste repositório):

```bash
cd ../fcg-orchestration
cp .env.example .env              # só na primeira vez; preencha JWT__SIGNINGKEY (openssl rand -hex 32)
docker compose up -d rabbitmq postgres
cd ../fcg-payments-api
```

Depois, com o .NET:

```bash
export JWT__SIGNINGKEY=<a mesma chave usada pelo fcg-users-api>
dotnet run --project src/FCG.Payments.Api
```

O serviço sobe em http://localhost:5103/swagger. Para trocar o limite sem editar arquivo:

```bash
Payments__LimiteDeAprovacao=300 dotnet run --project src/FCG.Payments.Api
```

Ou com a imagem Docker, sem precisar do SDK:

```bash
docker build -t fcg-payments-api:local .
docker run --rm -p 5103:8080 \
  -e JWT__SIGNINGKEY="<a mesma chave do users-api>" \
  -e ConnectionStrings__Default="Host=host.docker.internal;Port=5435;Database=fcg_payments;Username=fcg;Password=fcg_password" \
  -e RabbitMq__Host=host.docker.internal \
  -e RabbitMq__Username=fcg -e RabbitMq__Password=fcg_password \
  fcg-payments-api:local
```

`host.docker.internal` é o nome que o Docker Desktop dá à máquina hospedeira, onde o compose publicou o PostgreSQL (porta 5435) e o RabbitMQ (5672).

Sozinho, o Payments não tem o que processar: ele só reage a pedidos vindos do Catalog. Para vê-lo trabalhando, suba também o Catalog (e o Users, para obter o token) e faça uma compra; o passo a passo está no README do fcg-orchestration. Os `PaymentProcessedEvent` ficam retidos nas filas do Catalog e do Notifications até eles subirem.

Para rodar os testes (não precisam de banco nem de broker):

```bash
dotnet test
```

## Estrutura do código

```
src/
  FCG.Payments.Domain           Pagamento, PoliticaDeAprovacao (a regra do limite) e exceções de domínio
  FCG.Payments.Application      PagamentoService, DTOs, interfaces e os contratos de evento (Contracts/)
  FCG.Payments.Infrastructure   EF Core, migrações, MassTransit (consumidor, outbox e declaração das filas) e validação do JWT
  FCG.Payments.Api              endpoint de consulta, middleware de exceções, Swagger e Program.cs
tests/
  FCG.Payments.Tests            testes de unidade e de consumo/publicação com o harness do MassTransit
k8s/                            Deployment, Service, ConfigMap e Secret deste serviço
Dockerfile                      build multi-stage, compila para arm64 e amd64
```

## Decisões que afetam este serviço

As justificativas completas estão na seção [Decisões de projeto](https://github.com/PedroRCampelo/fcg-orchestration#decisões-de-projeto) do fcg-orchestration. As que mais pesam aqui:

- **Outbox no consumidor** e **idempotência pelo `OrderId`**: descritos acima.
- **Contrato de evento duplicado:** a pasta `Contracts/` é uma cópia idêntica à dos outros repositórios que usam o mesmo evento. O `PaymentStatus` tem valores fixos (`Approved = 1`, `Rejected = 2`) e vai como texto no JSON.
- **Nomes de fila repetidos** no `k8s/configmap.yaml`, iguais aos dos outros serviços, e o limite de aprovação configurável no mesmo arquivo.
- **EF Core 9 sobre .NET 8**, por exigência do MassTransit.EntityFrameworkCore 8.5.
