using FluentValidation;
using Loca.Application.Common.Interfaces;
using Loca.Application.Common.Models;
using Loca.Application.Features.StudentVerifications.Common;
using Loca.Domain.Entities;
using Loca.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Loca.Application.Features.StudentVerifications.SubmitStudentVerification;

/// <summary>
/// Ogrenci bilgisi girisi. Var olan kayit varsa gunceller.
/// </summary>
/// <remarks>
/// <b>Kimlik numarasi ZORUNLU DEGIL.</b> Yabanci uyruklu ogrencinin T.C.
/// kimlik numarasi olmaz; zorunlu yapilsaydi bu ogrenciler ogrenci bileti
/// alamazdi. Numara verilmediginde ogrenciyi tanimlayan deger ogrenci
/// numarasina duser ve kayit reddedilmez — tekillik kontrolu de bu yuzden
/// (okul, ogrenci no) ikilisi uzerinden yapiliyor.
/// </remarks>
public sealed record SubmitStudentVerificationCommand(
    string FullName,
    string InstitutionName,
    string StudentNumber,
    DateTime ValidUntilUtc,
    string? NationalIdentityNumber,
    Guid? DocumentFileId) : IRequest<Result<Guid>>;

public sealed class SubmitStudentVerificationCommandValidator
    : AbstractValidator<SubmitStudentVerificationCommand>
{
    public SubmitStudentVerificationCommandValidator()
    {
        RuleFor(command => command.FullName)
            .NotEmpty().WithMessage("Adi soyadi zorunludur.")
            .MaximumLength(200);

        RuleFor(command => command.InstitutionName)
            .NotEmpty().WithMessage("Okul adi zorunludur.")
            .MaximumLength(200);

        // Tek zorunlu kimlik alani: kimlik numarasi olmadiginda ogrenciyi
        // ayirt eden deger bu.
        RuleFor(command => command.StudentNumber)
            .NotEmpty().WithMessage("Ogrenci numarasi zorunludur.")
            .MaximumLength(50);

        // NotEmpty YOK — alan bos gelebilir. Yalnizca DOLDURULMUSSA bicim
        // kontrol ediliyor; When olmadan bos deger de 11 hane bekleyen
        // kurala takilir ve kayit reddedilirdi.
        RuleFor(command => command.NationalIdentityNumber)
            .Length(11).WithMessage("Kimlik numarasi 11 haneli olmalidir.")
            .Matches("^[0-9]+$").WithMessage("Kimlik numarasi yalnizca rakam icermelidir.")
            .When(command => !string.IsNullOrWhiteSpace(command.NationalIdentityNumber));

        RuleFor(command => command.ValidUntilUtc)
            .NotEmpty().WithMessage("Belgenin gecerlilik tarihi zorunludur.");
    }
}

internal sealed class SubmitStudentVerificationCommandHandler(
    IStudentVerificationRepository verifications,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ILogger<SubmitStudentVerificationCommandHandler> logger)
    : IRequestHandler<SubmitStudentVerificationCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        SubmitStudentVerificationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (currentUser.UserId is not { } userId)
            return Result.Failure<Guid>(StudentVerificationErrors.Unauthenticated);

        var mevcut = await verifications.GetByUserIdAsync(userId, cancellationToken);

        // Bos metin ile null ayrimi burada kapatiliyor: formdan dokunulmamis
        // bir alan bos metin olarak gelir ve oldugu gibi saklanirsa "kimlik
        // numarasi var" gibi davranip bos deger dondururdu.
        var kimlikNo = string.IsNullOrWhiteSpace(request.NationalIdentityNumber)
            ? null
            : request.NationalIdentityNumber.Trim();

        if (await verifications.StudentNumberTakenAsync(
                request.InstitutionName, request.StudentNumber, mevcut?.Id, cancellationToken))
        {
            return Result.Failure<Guid>(StudentVerificationErrors.StudentNumberTaken);
        }

        // Kimlik numarasi tekilligi YALNIZCA verilmisse kontrol ediliyor.
        // Kosulsuz kontrol edilseydi numarasi olmayan ikinci ogrenci
        // "ayni null" yuzunden reddedilirdi.
        if (kimlikNo is not null &&
            await verifications.NationalIdentityTakenAsync(
                kimlikNo, mevcut?.Id, cancellationToken))
        {
            return Result.Failure<Guid>(StudentVerificationErrors.NationalIdentityTaken);
        }

        if (mevcut is not null)
        {
            if (mevcut.Status == Domain.Enums.StudentVerificationStatus.Approved)
                return Result.Failure<Guid>(StudentVerificationErrors.AlreadyApproved);

            mevcut.Update(
                request.FullName,
                request.InstitutionName,
                request.StudentNumber,
                request.ValidUntilUtc,
                kimlikNo,
                request.DocumentFileId);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Ogrenci dogrulama kaydi guncellendi. KayitId: {KayitId}, " +
                "OgrenciNumarasiEsasAlindi: {OgrenciNoEsas}",
                mevcut.Id,
                mevcut.IdentifiedByStudentNumber);

            return Result.Success(mevcut.Id);
        }

        var verification = new StudentVerification(
            userId,
            request.FullName,
            request.InstitutionName,
            request.StudentNumber,
            request.ValidUntilUtc,
            kimlikNo,
            request.DocumentFileId);

        verifications.Add(verification);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Kimlik numarasinin kendisi LOGLANMIYOR: kisisel veri.
        // Loglanan yalnizca hangi alanin esas alindigi.
        logger.LogInformation(
            "Ogrenci dogrulama kaydi olusturuldu. KayitId: {KayitId}, " +
            "OgrenciNumarasiEsasAlindi: {OgrenciNoEsas}",
            verification.Id,
            verification.IdentifiedByStudentNumber);

        return Result.Success(verification.Id);
    }
}
