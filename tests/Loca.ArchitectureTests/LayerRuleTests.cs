using System.Reflection;
using Loca.Application.Common.Models;
using Loca.Domain.Common;
using MediatR;
using NetArchTest.Rules;

namespace Loca.ArchitectureTests;

/// <summary>
/// Katman kurallarini derleme sonrasi assembly'ler uzerinden dogrular.
/// </summary>
/// <remarks>
/// Onion mimarisinin tek kurali var: bagimlilik oklari hep iceri bakar.
/// Bu kural yalnizca belgede yazarsa ilk yogun gunde delinir; burada
/// otomatik testle baglanmis durumda. Kural ihlali derlemeyi degil testi
/// kirar, dolayisiyla CI'da yakalanir.
/// </remarks>
public class LayerRuleTests
{
    private static readonly Assembly DomainAssembly = typeof(BaseEntity).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Result).Assembly;
    private static readonly Assembly WebApiAssembly = typeof(Program).Assembly;

    private const string DomainNamespace = "Loca.Domain";
    private const string ApplicationNamespace = "Loca.Application";
    private const string PersistenceNamespace = "Loca.Persistence";
    private const string InfrastructureNamespace = "Loca.Infrastructure";
    private const string WebApiNamespace = "Loca.WebApi";

    /// <summary>
    /// 1 · Cekirdek disariyi tanimaz.
    /// </summary>
    /// <remarks>
    /// Domain veritabanini, HTTP'yi veya Redis'i taniyorsa iki sey kaybedilir:
    /// is kurallari altyapi ayaga kaldirilmadan test edilemez, ve teknoloji
    /// degisikligi (ornegin EF Core'dan Dapper'a gecis) cekirdegi de etkiler.
    /// </remarks>
    [Fact]
    public void DomainShouldNotDependOnOuterLayers()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(PersistenceNamespace, InfrastructureNamespace, WebApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result, "Domain dis katmanlara bagimli olamaz."));
    }

    /// <summary>
    /// 2 · Uygulama katmani HTTP'yi bilmez.
    /// </summary>
    /// <remarks>
    /// Ayni handler bir konsol uygulamasindan, bir arka plan isinden veya
    /// testten cagrilabilmeli. Application, WebApi'yi tanirsa bu imkansizlasir.
    /// </remarks>
    [Fact]
    public void ApplicationShouldNotDependOnWebApi()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOn(WebApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result, "Application, WebApi'yi referans alamaz."));
    }

    /// <summary>
    /// 3 · Handler'lar ozellik klasoru altinda toplanir.
    /// </summary>
    /// <remarks>
    /// CQRS dosyalari teknik tipe gore degil is yetenegine gore gruplanir.
    /// Bir ozellige dokunurken tek klasor acilsin diye.
    /// Auth handler'lari yazilana kadar bu test eslesecek tip bulamaz;
    /// Gun 3'ten itibaren anlamli calisir.
    /// </remarks>
    [Fact]
    public void HandlersShouldResideInFeaturesFolder()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That()
            .ImplementInterface(typeof(IRequestHandler<,>))
            .Should()
            .ResideInNamespaceStartingWith($"{ApplicationNamespace}.Features")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result, "Handler'lar Features altinda olmali."));
    }

    /// <summary>
    /// 4 · Controller veritabanina dokunmaz.
    /// </summary>
    /// <remarks>
    /// Controller yalnizca cevirmendir: HTTP isteğini command/query'ye,
    /// Result'i HTTP cevabina cevirir. Icine DbContext girdigi anda is kurali
    /// da oraya sizar ve ayni sorgu baska bir endpoint'te tekrar yazilir.
    /// </remarks>
    [Fact]
    public void ControllersShouldNotUseDbContext()
    {
        var result = Types.InAssembly(WebApiAssembly)
            .That()
            .HaveNameEndingWith("Controller")
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", PersistenceNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result, "Controller DbContext kullanamaz."));
    }

    /// <summary>
    /// 5 · Disariya entity cikmaz.
    /// </summary>
    /// <remarks>
    /// Endpoint entity donerse veritabani semasi API sozlesmesi haline gelir:
    /// bir kolon adi degistiginde istemci kirilir, ayrica sifre ozeti gibi
    /// alanlar farkinda olmadan disari sizar. Cevaplarda yalnizca DTO bulunur.
    /// </remarks>
    [Fact]
    public void ControllersShouldNotReturnDomainEntities()
    {
        var result = Types.InAssembly(WebApiAssembly)
            .That()
            .HaveNameEndingWith("Controller")
            .Should()
            .NotHaveDependencyOn($"{DomainNamespace}.Entities")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result, "Controller domain entity donduremez."));
    }

    /// <summary>
    /// Basarisiz testin ciktisinda hangi tipin kurali deldigi gorunsun diye.
    /// Yalnizca "test kirildi" demek, sebebi aramak icin zaman kaybettirir.
    /// </summary>
    private static string Describe(TestResult result, string rule)
    {
        var offenders = result.FailingTypeNames ?? [];
        return $"{rule}{Environment.NewLine}Kurali delen tipler: {string.Join(", ", offenders)}";
    }
}
