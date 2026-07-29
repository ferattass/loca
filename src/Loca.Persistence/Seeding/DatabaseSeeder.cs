using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Logging;
using Loca.Domain.Constants;
using Loca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loca.Persistence.Seeding;

/// <summary>
/// Uygulama acilisinda bekleyen migration'lari uygular ve ilk admin
/// hesabini olusturur.
/// </summary>
/// <remarks>
/// Migration'in burada uygulanmasi bilincli bir karar: sartname "tek komutla
/// ayaga kalkma" istiyor. Migration elle uygulansaydi <c>docker-compose up</c>
/// sonrasi API ayakta ama veritabani bos olurdu ve bir komut daha gerekirdi.
///
/// <para>
/// Islem idempotent: her aciliste calisir, ikinci kez bir sey degistirmez.
/// </para>
/// </remarks>
public sealed class DatabaseSeeder(
    LocaDbContext dbContext,
    IPasswordHasher passwordHasher,
    IOptions<AdminSeedOptions> options,
    ILogger<DatabaseSeeder> logger)
{
    private readonly AdminSeedOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
        await SeedAdminAsync(cancellationToken);
    }

    private async Task SeedAdminAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Email) || string.IsNullOrWhiteSpace(_options.Password))
        {
            // Sessizce gecmek yerine uyarilir: admin hesabi olmadan mekan ve
            // salon uclarinin hicbiri denenemez, sebebi de gorunmez olurdu.
            logger.LogWarning(
                "Admin tohumu atlandi: {Section}:Email ve {Section}:Password tanimli degil.",
                AdminSeedOptions.SectionName,
                AdminSeedOptions.SectionName);
            return;
        }

        var email = User.Normalize(_options.Email);

        if (await dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            logger.LogInformation("Admin hesabi zaten mevcut: {Email}", Masking.Email(email));
            return;
        }

        var adminRole = await dbContext.Roles
            .SingleOrDefaultAsync(role => role.Name == RoleNames.Admin, cancellationToken)
            ?? throw new InvalidOperationException(
                $"'{RoleNames.Admin}' rolu bulunamadi. Migration tohumu uygulanmamis olabilir.");

        var admin = new User(
            email,
            passwordHasher.Hash(_options.Password),
            _options.FullName);

        // Tohumlanan hesap dogrulama akisini beklemez; e-posta altyapisi
        // Gun 9'da geliyor ve o zamana kadar admin girisi kilitli kalmamali.
        admin.ConfirmEmail();
        admin.AssignRole(adminRole);

        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Admin hesabi olusturuldu: {Email}", Masking.Email(email));
    }
}
