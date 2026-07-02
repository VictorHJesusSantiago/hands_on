# Segfy Policies

Aplicação completa para cadastro e gestão de apólices de seguro automóvel. A solução contém uma Web API REST em C#/.NET 8, persistência local em SQLite, interface web responsiva e testes automatizados.

## Requisitos atendidos

- CRUD completo de apólices;
- número automático e sequencial no padrão `SEG-YYYY-XXXX`;
- CPF/CNPJ com validação dos dígitos verificadores;
- placas brasileiras nos formatos antigo (`ABC1234`) e Mercosul (`ABC1D23`);
- prêmio mensal, vigência e status (`Ativa`, `Cancelada`, `Expirada`);
- persistência em SQLite, criada automaticamente na primeira execução;
- consulta SQL parametrizada das apólices ativas que vencem nos próximos 30 dias;
- atualização automática para `Expirada` quando a vigência termina;
- paginação, busca e filtro por status;
- respostas de erro no padrão Problem Details;
- interface web sem dependências de Node;
- testes unitários e de integração.

## Como executar

Pré-requisito: [.NET SDK 8 ou superior](https://dotnet.microsoft.com/download).

Na raiz do projeto:

```powershell
dotnet restore
dotnet run --project src/Segfy.Policies.Api
```

Abra `http://localhost:5080`. O banco será criado em `src/Segfy.Policies.Api/data/policies.db`.

Não é necessário instalar SQL Server, Node, Docker ou executar scripts de banco.

## Como testar

```powershell
dotnet test
```

Os testes cobrem validação de CPF/CNPJ, validação agregada dos campos, sequência anual, não reutilização de número após exclusão, consulta de vencimento, expiração automática e recurso inexistente.

## Endpoints

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/api/policies` | Cadastra uma apólice |
| `GET` | `/api/policies` | Lista com paginação, busca e filtro |
| `GET` | `/api/policies/{id}` | Consulta por ID |
| `PUT` | `/api/policies/{id}` | Atualiza todos os dados editáveis |
| `DELETE` | `/api/policies/{id}` | Exclui uma apólice |
| `GET` | `/api/policies/expiring?days=30` | Lista apólices ativas a vencer |
| `GET` | `/health` | Verifica a disponibilidade da aplicação |

Parâmetros da listagem: `page` (padrão 1), `pageSize` (padrão 20, máximo 100), `search` e `status`.

### Exemplo de cadastro

```powershell
Invoke-RestMethod -Method Post `
  -Uri http://localhost:5080/api/policies `
  -ContentType application/json `
  -Body '{
    "insuredDocument": "529.982.247-25",
    "vehiclePlate": "ABC1D23",
    "monthlyPremium": 249.90,
    "coverageStartDate": "2026-07-01",
    "coverageEndDate": "2027-07-01",
    "status": "Ativa"
  }'
```

## Consulta SQL de vencimento

A consulta pedida no desafio está em [`src/Segfy.Policies.Infrastructure/Sql/PoliciesExpiringIn30Days.sql`](src/Segfy.Policies.Infrastructure/Sql/PoliciesExpiringIn30Days.sql) e também é executada de forma parametrizada pelo repositório:

```sql
SELECT *
FROM policies
WHERE status = 1
  AND date(coverage_end_date) BETWEEN date(@today) AND date(@today, '+30 days')
ORDER BY date(coverage_end_date) ASC, policy_number ASC;
```

## Arquitetura

```text
Segfy.Policies.Domain          Entidades e regras centrais
        ↑
Segfy.Policies.Application     Casos de uso, contratos e validações
        ↑
Segfy.Policies.Infrastructure  SQLite, SQL e implementações técnicas
        ↑
Segfy.Policies.Api             HTTP, tratamento de erros e interface web
```

O domínio e a aplicação não dependem de banco ou de ASP.NET. A infraestrutura implementa as abstrações da aplicação, permitindo substituir o SQLite sem alterar os casos de uso.

### Decisões importantes

- A sequência fica em uma tabela própria e é incrementada na mesma transação do cadastro. Excluir uma apólice não libera seu número.
- O ano do número é o ano UTC do cadastro. Cada ano possui sua própria sequência.
- A consulta de vencimento retorna somente apólices ativas, incluindo as que vencem hoje e no 30º dia.
- Datas são persistidas como ISO 8601 (`YYYY-MM-DD`) e valores monetários usam `decimal`.
- Todos os valores da consulta são parâmetros; nenhum dado do usuário é concatenado ao SQL.
