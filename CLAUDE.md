# CoreFinance

Sistema financeiro pessoal desenvolvido com:
- ASP.NET Core 8
- React / Next.js
- SQL Server
- Clean Architecture

---

# Documentação principal

Leia obrigatoriamente antes de gerar código:

1. /docs/PRD.md
2. /docs/ARCHITECTURE.md
3. /docs/RULES.md

---

# Agentes disponíveis

- backend-architect
- frontend-architect
- reviewer

---

# Skills disponíveis

- create-endpoint
- create-react-page

---

# Regras obrigatórias

- seguir Clean Architecture
- seguir SOLID
- usar async/await
- usar FluentValidation
- usar DTOs
- nunca colocar regra de negócio em controllers
- frontend simples e elegante
- usar Bootstrap
- usar dark mode

---

# Estrutura esperada backend

src/
  CoreFinance.API
  CoreFinance.Application
  CoreFinance.Domain
  CoreFinance.Infra

---

# Estrutura esperada frontend

web/
  src/
    app/
    components/
    services/
    hooks/

---

# Objetivo

Sempre gerar código pronto para produção.
Nunca gerar pseudocódigo.
Sempre explicar os arquivos criados.