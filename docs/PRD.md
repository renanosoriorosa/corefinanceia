# CoreFinance - Product Requirements Document

## Visão Geral

CoreFinance é um sistema web simples para controle financeiro pessoal.

O objetivo é permitir o gerenciamento de contas fixas e não fixas, visualizando pagamentos mensais e dashboards financeiros.

O sistema deve possuir foco em:
- simplicidade
- performance
- boa experiência visual
- arquitetura limpa
- fácil manutenção
- IA First Development

---

# Stack Tecnológica

## Backend
- ASP.NET Core 8 Web API
- Entity Framework Core
- SQL Server
- LINQ
- Migrations
- Clean Architecture
- SOLID
- FluenteValidation

## Frontend
- React (última versão)
- Next.js
- Bootstrap
- Dark Theme
- Responsivo

---

# Funcionalidades

## 0. Autenticação e Multiusuário

O acesso ao sistema exige conta própria.

### Telas
- Login
- Criar conta

### Regras
- senha armazenada com hash (BCrypt)
- token JWT com validade configurável
- cada usuário enxerga somente os próprios dados
- rotas da API protegidas por padrão

---

## 1. Cadastro de Contas Fixas

O usuário pode cadastrar contas fixas.

Exemplos:
- Energia elétrica
- Água
- Condomínio
- Internet

### Campos
- Nome
- Descrição
- Ativo

---

## 2. Configuração de Contas Obrigatórias

O usuário pode definir quais contas são obrigatórias mensalmente.

Exemplo:
- Luz
- Água
- Condomínio

Todo mês essas contas devem aparecer como pendentes até serem pagas.

---

## 3. Painel do Mês Atual

Tela inicial contendo:
- contas obrigatórias pagas
- contas obrigatórias pendentes
- total pago no mês
- quantidade de contas pendentes

### Regras
- destacar visualmente pendências
- atualização em tempo real após pagamento

---

## 4. Inserir/Editar Conta Paga

O usuário pode registrar pagamentos.

### Campos
- Mês
- Ano
- Valor pago
- Tipo:
  - Conta fixa
  - Conta não fixa

Se conta fixa:
- selecionar conta fixa cadastrada

Se não fixa:
- informar descrição manual

---

## 5. Dashboard Financeiro

Tela com:
- filtro por ano
- filtro por conta fixa
- checkbox para contas não fixas

### Gráfico
Gráfico de barras contendo:
- meses do ano
- valor pago por mês

### Exemplo
Janeiro: R$350
Fevereiro: R$380
Março: R$340

---

## 6. Tema Escuro

O sistema deve possuir:
- dark theme
- persistência da preferência
- alternância dinâmica

---

# Requisitos Não Funcionais

- API RESTful
- Código limpo
- Alta legibilidade
- Componentização
- Responsividade
- Separação de responsabilidades
- DTOs
- Validações
- Tratamento global de erros
- Logs estruturados
- swagger
- rotas com um padrao de retorno
- seguir regras do http (status codes)

---

# Arquitetura Backend

## Camadas

### API
Responsável por:
- controllers
- autenticação
- middlewares

### Application
Responsável por:
- casos de uso
- services
- DTOs
- validações

### Domain
Responsável por:
- entidades
- enums
- regras de negócio

### Infra
Responsável por:
- EF Core
- repositories
- banco de dados

---

# Convenções

## Backend
- Commands/Queries (CQRS simples)
- Async/Await obrigatório
- Repository Pattern
- FluentValidation
- Result Pattern

## Frontend
- Componentes reutilizáveis
- Pages simples
- Hooks para regras
- Services separados
- Axios para API

---

# MVP

## Primeira versão
- CRUD contas fixas
- CRUD pagamentos
- Dashboard mensal
- Dark theme

---

# Futuras melhorias
- exportação PDF
- metas financeiras
- notificações
- gráficos avançados