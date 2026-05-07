# Regras de Desenvolvimento

# Backend

- Usar .NET 8
- Usar async/await
- Nunca usar código síncrono para banco
- Toda entidade deve ter configuração EF separada
- Toda validação deve usar FluentValidation
- Controllers devem ser finos
- Não usar lógica de negócio na API
- Sempre usar injeção de dependência
- Sempre usar interfaces
- solid

---

# Frontend

- Usar Next.js App Router
- Usar Bootstrap
- Não usar CSS inline
- Componentes devem ser pequenos
- Separar:
  - UI
  - Services
  - Hooks
- Axios centralizado

---

# Código

- Priorizar simplicidade
- Evitar overengineering
- Nomear métodos claramente
- Métodos curtos
- Classes pequenas
- Alta legibilidade

---

# IA First

- Sempre gerar código completo
- Nunca gerar pseudocódigo
- Sempre incluir:
  - imports
  - namespaces
  - tipagem
  - validações
- Sempre explicar arquivos criados
- Sempre seguir Clean Architecture
- Sempre seguir Solid