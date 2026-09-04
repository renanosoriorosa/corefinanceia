using CoreFinance.Domain.Entities;
using CoreFinance.Domain.Interfaces.Repositories;
using CoreFinance.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace CoreFinance.Infra.Repositories;

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public async Task<User?> ObterPorEmailAsync(string email)
        => await _dbSet.FirstOrDefaultAsync(u => u.Email == email.Trim().ToLower());

    public async Task<bool> EmailExisteAsync(string email)
        => await _dbSet.AnyAsync(u => u.Email == email.Trim().ToLower());
}
