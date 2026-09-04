# Arquitetura CoreFinance

# Backend

## Clean Architecture

API
↓
Application
↓
Domain
↓
Infra

---

# Padrões

- SOLID
- Repository Pattern
- Dependency Injection
- CQRS simples
- DTO Pattern

---

# Banco

## Tabelas

### Users
- Id
- Name
- Email (único)
- PasswordHash
- Active

### FixedAccounts
- Id
- UserId
- Name
- Description
- IsRequiredMonthly
- Active

### Payments
- Id
- UserId
- FixedAccountId nullable
- Description
- Amount
- Month
- Year
- IsFixedAccount
- CreatedAt

---

# Frontend

## Estrutura

web/
  src/
    components/
    pages/
    services/
    hooks/
    layouts/
    styles/

---

# UI

- Bootstrap
- Design minimalista
- Cards elegantes
- Sidebar simples
- Dark mode

---

# Multiusuário

- Autenticação com JWT (Bearer)
- Toda entidade do usuário herda de OwnedEntity (UserId)
- O isolamento é feito por global query filter no AppDbContext
- O UserId é preenchido automaticamente no SaveChangesAsync
- Services e repositories nunca filtram por usuário manualmente

---

# Regras importantes

- Nunca acessar banco diretamente da API
- Nunca colocar regra de negócio em controllers
- Toda regra deve ficar na Application ou Domain
- DTOs obrigatórios
- Queries separadas de Commands
- Codigo todo em Portugues claro
- Codigo simples e objetivos
- Siga o SOLID
